using System.Drawing;
using System.Reflection;
using ModernFormsNext.Animations;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed record ParityScenario(
    string Name,
    Func<DesignDocument> CreateDocument,
    DesignSize? LayoutSize = null)
{
    public override string ToString() => Name;
}

internal sealed record LayoutNodeSnapshot(
    string Path,
    string TypeName,
    Rectangle Bounds,
    Rectangle? ClientRectangle,
    Rectangle? DisplayRectangle,
    Rectangle VisibleBounds,
    Size? ChildAvailableSize);

internal sealed record LayoutSnapshot(
    string Scenario,
    IReadOnlyDictionary<string, LayoutNodeSnapshot> Nodes);

/// <summary>
/// Builds equivalent Designer and runtime control trees and compares normalized semantic geometry.
/// </summary>
/// <remarks>
/// This helper deliberately delegates all placement to <see cref="DesignerLayoutEngine"/> and the
/// production runtime <see cref="Control.LayoutEngine"/>. It only translates document properties,
/// collects public geometry, and normalizes absolute paths and ancestor clipping for diagnostics.
/// </remarks>
internal static class DesignerRuntimeLayoutParityHarness
{
    public static void AssertParity(ParityScenario scenario)
    {
        var document = scenario.CreateDocument();
        AssertParity(scenario.Name, document, document, scenario.LayoutSize);
    }

    public static void AssertParity(
        string scenario,
        DesignDocument designerDocument,
        DesignDocument runtimeDocument,
        DesignSize? layoutSize = null)
    {
        var targetSize = layoutSize ?? runtimeDocument.Size;
        var designer = CaptureDesigner(scenario, designerDocument, targetSize);
        using var runtimeTree = RuntimeLayoutTree.Build(runtimeDocument);
        runtimeTree.Resize(targetSize);
        var runtime = runtimeTree.Capture(scenario);

        AssertSnapshotsEqual(designer, runtime);
    }

    public static void AssertParity(
        string scenario,
        DesignDocument designerDocument,
        object initializedRuntimeRoot)
    {
        var designer = CaptureDesigner(scenario, designerDocument, designerDocument.Size);
        using var runtimeTree = RuntimeLayoutTree.Attach(designerDocument, initializedRuntimeRoot);
        runtimeTree.Resize(designerDocument.Size);
        var runtime = runtimeTree.Capture(scenario);

        AssertSnapshotsEqual(designer, runtime);
    }

    public static void AssertDpiParity(ParityScenario scenario, double dpiScale)
    {
        var document = scenario.CreateDocument();
        var targetSize = scenario.LayoutSize ?? document.Size;
        var designer = CaptureDesigner(scenario.Name, document, targetSize);
        using var runtimeTree = RuntimeLayoutTree.Build(document);
        runtimeTree.Resize(targetSize);
        var runtime = runtimeTree.Capture(scenario.Name);
        var differences = new List<string>();

        AssertSnapshotsEqual(designer, runtime);

        foreach (var path in designer.Nodes.Keys.OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!runtime.Nodes.TryGetValue(path, out var runtimeNode))
                continue;

            var designerBounds = DesignerDpiCoordinateConverter.LogicalToDevice(designer.Nodes[path].Bounds, dpiScale);
            var runtimeBounds = DesignerDpiCoordinateConverter.LogicalToDevice(runtimeNode.Bounds, dpiScale);
            AddDifference(differences, scenario.Name, path, $"Bounds@{dpiScale:0.##}x", runtimeBounds, designerBounds);
        }

        Assert.True(differences.Count == 0, FormatDifferences(differences));
    }

    public static LayoutSnapshot CaptureDesigner(string scenario, DesignDocument document, DesignSize size)
    {
        var layout = new DesignerLayoutEngine().Layout(document, size);
        var nodes = new Dictionary<string, LayoutNodeSnapshot>(StringComparer.Ordinal);
        var rootPath = RootPath(document);
        var rootBounds = new DesignBounds(0, 0, size.Width, size.Height);
        var rootDisplay = document.RootKind == DesignRootKind.UserControl
            ? DesignerLayoutProperties.GetPaddedContentBounds(
                rootBounds,
                DesignerLayoutProperties.GetPadding(document.Properties))
            : rootBounds;

        nodes.Add(
            rootPath,
            new LayoutNodeSnapshot(
                rootPath,
                document.RootKind.ToString(),
                ToRectangle(rootBounds),
                ToRectangle(rootBounds),
                ToRectangle(rootDisplay),
                ToRectangle(rootBounds),
                new Size(rootDisplay.Width, rootDisplay.Height)));

        CaptureDesignerChildren(document.Controls, rootPath, rootBounds, layout, nodes);
        return new LayoutSnapshot(scenario, nodes);
    }

    private static void CaptureDesignerChildren(
        IEnumerable<DesignControlNode> children,
        string parentPath,
        DesignBounds parentClip,
        DesignerLayoutResult layout,
        IDictionary<string, LayoutNodeSnapshot> nodes)
    {
        foreach (var child in children)
        {
            var path = $"{parentPath}.{child.Name}";
            var bounds = layout.GetEffectiveBounds(child);
            var visibleBounds = IntersectRectangles(ToRectangle(bounds), ToRectangle(parentClip));
            var isContainer = IsContainer(child.TypeName);
            var client = isContainer ? bounds : (DesignBounds?)null;
            var display = isContainer
                ? DesignerLayoutProperties.GetContainerContentBounds(child, bounds)
                : (DesignBounds?)null;

            nodes.Add(
                path,
                new LayoutNodeSnapshot(
                    path,
                    NormalizeTypeName(child.TypeName),
                    ToRectangle(bounds),
                    client is { } clientBounds ? ToRectangle(clientBounds) : null,
                    display is { } displayBounds ? ToRectangle(displayBounds) : null,
                    visibleBounds,
                    display is { } available ? new Size(available.Width, available.Height) : null));

            CaptureDesignerChildren(
                child.Children,
                path,
                new DesignBounds(visibleBounds.X, visibleBounds.Y, visibleBounds.Width, visibleBounds.Height),
                layout,
                nodes);
        }
    }

    private static void AssertSnapshotsEqual(LayoutSnapshot designer, LayoutSnapshot runtime)
    {
        var differences = new List<string>();
        var allPaths = designer.Nodes.Keys
            .Concat(runtime.Nodes.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (var path in allPaths)
        {
            if (!runtime.Nodes.TryGetValue(path, out var runtimeNode))
            {
                differences.Add($"scenario={designer.Scenario}; node={path}; property=Hierarchy; runtime=<missing>; designer=present");
                continue;
            }

            if (!designer.Nodes.TryGetValue(path, out var designerNode))
            {
                differences.Add($"scenario={designer.Scenario}; node={path}; property=Hierarchy; runtime=present; designer=<missing>");
                continue;
            }

            AddDifference(differences, designer.Scenario, path, "Type", runtimeNode.TypeName, designerNode.TypeName);
            AddDifference(differences, designer.Scenario, path, "Bounds", runtimeNode.Bounds, designerNode.Bounds);
            AddDifference(differences, designer.Scenario, path, "ClientRectangle", runtimeNode.ClientRectangle, designerNode.ClientRectangle);
            AddDifference(differences, designer.Scenario, path, "DisplayRectangle", runtimeNode.DisplayRectangle, designerNode.DisplayRectangle);
            AddDifference(differences, designer.Scenario, path, "VisibleBounds", runtimeNode.VisibleBounds, designerNode.VisibleBounds);
            AddDifference(differences, designer.Scenario, path, "ChildAvailableSize", runtimeNode.ChildAvailableSize, designerNode.ChildAvailableSize);
        }

        Assert.True(differences.Count == 0, FormatDifferences(differences));
    }

    private static void AddDifference<T>(
        ICollection<string> differences,
        string scenario,
        string path,
        string property,
        T runtime,
        T designer)
    {
        if (!EqualityComparer<T>.Default.Equals(runtime, designer))
        {
            differences.Add(
                $"scenario={scenario}; node={path}; property={property}; " +
                $"runtime={FormatValue(runtime)}; designer={FormatValue(designer)}");
        }
    }

    private static string FormatDifferences(IReadOnlyCollection<string> differences)
        => differences.Count == 0
            ? string.Empty
            : "Designer/runtime layout parity failed:" + Environment.NewLine + string.Join(Environment.NewLine, differences);

    private static string FormatValue<T>(T value)
        => value switch
        {
            null => "<null>",
            Rectangle rectangle => $"X={rectangle.X},Y={rectangle.Y},Width={rectangle.Width},Height={rectangle.Height}",
            Size size => $"Width={size.Width},Height={size.Height}",
            _ => value.ToString() ?? "<null>"
        };

    private static bool IsContainer(string typeName)
    {
        var normalized = NormalizeTypeName(typeName);
        return normalized is "Panel" or "UserControl" or "ScrollableControl" or "FlowLayoutPanel" or "TableLayoutPanel";
    }

    private static string NormalizeTypeName(string typeName)
        => DesignerProjectUserControlDiscovery.NormalizeTypeName(typeName).Split('.').Last();

    private static string RootPath(DesignDocument document)
        => string.IsNullOrWhiteSpace(document.FormName) ? document.ClassName : document.FormName;

    private static Rectangle ToRectangle(DesignBounds bounds)
        => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private static Rectangle IntersectRectangles(Rectangle left, Rectangle right)
    {
        var x = Math.Max(left.Left, right.Left);
        var y = Math.Max(left.Top, right.Top);
        var width = Math.Max(0, Math.Min(left.Right, right.Right) - x);
        var height = Math.Max(0, Math.Min(left.Bottom, right.Bottom) - y);
        return new Rectangle(x, y, width, height);
    }

    private sealed class RuntimeLayoutTree : IDisposable
    {
        private readonly DesignDocument document;
        private readonly Dictionary<DesignControlNode, Control> controls;
        private readonly Control? controlRoot;
        private readonly Form? formRoot;
        private readonly bool ownsRoot;

        private RuntimeLayoutTree(
            DesignDocument document,
            Dictionary<DesignControlNode, Control> controls,
            Control? controlRoot,
            Form? formRoot,
            bool ownsRoot)
        {
            this.document = document;
            this.controls = controls;
            this.controlRoot = controlRoot;
            this.formRoot = formRoot;
            this.ownsRoot = ownsRoot;
        }

        public static RuntimeLayoutTree Build(DesignDocument document)
        {
            var controls = new Dictionary<DesignControlNode, Control>();

            if (document.RootKind == DesignRootKind.Form)
            {
                var form = new Form();
                ApplyRootProperties(form, document);
                form.ClientSize = ToSize(document.Size);
                CreateControls(document.Controls, controls);
                AddChildren(form.Controls, document.Controls, controls, sequential: false);
                var directChild = document.Controls.Select(node => controls[node]).FirstOrDefault();
                directChild?.Parent?.PerformLayout();
                foreach (var child in document.Controls)
                    PerformLayoutRecursively(controls[child]);
                return new RuntimeLayoutTree(document, controls, controlRoot: null, form, ownsRoot: true);
            }

            var root = new UserControl();
            root.Size = ToSize(document.Size);
            ApplyRootProperties(root, document);
            root.SuspendLayout();
            CreateControls(document.Controls, controls);
            AddChildren(root.Controls, document.Controls, controls, sequential: false);
            root.ResumeLayout(true);
            PerformLayoutRecursively(root);
            return new RuntimeLayoutTree(document, controls, root, formRoot: null, ownsRoot: true);
        }

        public static RuntimeLayoutTree Attach(DesignDocument document, object initializedRoot)
        {
            var controls = new Dictionary<DesignControlNode, Control>();

            switch (initializedRoot)
            {
                case Form form:
                    AttachChildren(document.Controls, form.Controls, controls);
                    return new RuntimeLayoutTree(document, controls, controlRoot: null, form, ownsRoot: false);
                case UserControl control:
                    AttachChildren(document.Controls, control.Controls, controls);
                    return new RuntimeLayoutTree(document, controls, control, formRoot: null, ownsRoot: false);
                default:
                    throw new InvalidOperationException($"Generated root type '{initializedRoot.GetType().FullName}' is not a Form or UserControl.");
            }
        }

        public void Resize(DesignSize size)
        {
            if (controlRoot is not null)
            {
                controlRoot.Size = ToSize(size);
                PerformLayoutRecursively(controlRoot);
                return;
            }

            formRoot!.ClientSize = ToSize(size);
            var directChild = document.Controls.Select(node => controls[node]).FirstOrDefault();
            directChild?.Parent?.PerformLayout();
            foreach (var child in document.Controls)
                PerformLayoutRecursively(controls[child]);
        }

        public LayoutSnapshot Capture(string scenario)
        {
            var nodes = new Dictionary<string, LayoutNodeSnapshot>(StringComparer.Ordinal);
            var rootPath = RootPath(document);
            var rootSize = controlRoot?.Size ?? formRoot!.ClientSize;
            var rootBounds = new Rectangle(Point.Empty, rootSize);
            var rootDisplay = controlRoot?.DisplayRectangle ?? rootBounds;

            nodes.Add(
                rootPath,
                new LayoutNodeSnapshot(
                    rootPath,
                    document.RootKind.ToString(),
                    rootBounds,
                    rootBounds,
                    rootDisplay,
                    rootBounds,
                    rootDisplay.Size));

            CaptureRuntimeChildren(document.Controls, rootPath, Point.Empty, rootBounds, nodes);
            return new LayoutSnapshot(scenario, nodes);
        }

        public void Dispose()
        {
            if (!ownsRoot)
                return;

            controlRoot?.Dispose();
            formRoot?.Dispose();
        }

        private void CaptureRuntimeChildren(
            IEnumerable<DesignControlNode> children,
            string parentPath,
            Point parentOffset,
            Rectangle parentClip,
            IDictionary<string, LayoutNodeSnapshot> snapshots)
        {
            foreach (var node in children)
            {
                var control = controls[node];
                var path = $"{parentPath}.{node.Name}";
                var bounds = control.Bounds;
                bounds.Offset(parentOffset);
                var visible = Intersect(bounds, parentClip);
                var isContainer = IsContainer(node.TypeName);
                Rectangle? client = null;
                Rectangle? display = null;

                if (isContainer)
                {
                    client = Offset(control.ClientRectangle, bounds.Location);
                    display = Offset(control.DisplayRectangle, bounds.Location);
                }

                snapshots.Add(
                    path,
                    new LayoutNodeSnapshot(
                        path,
                        control.GetType().Name,
                        bounds,
                        client,
                        display,
                        visible,
                        display?.Size));

                CaptureRuntimeChildren(node.Children, path, bounds.Location, visible, snapshots);
            }
        }

        private static void AddChildren(
            Control.ControlCollection runtimeChildren,
            IReadOnlyList<DesignControlNode> designChildren,
            IDictionary<DesignControlNode, Control> controls,
            bool sequential)
        {
            var ordered = sequential
                ? designChildren
                : designChildren.Reverse().ToArray();

            foreach (var child in ordered)
            {
                var control = controls[child];
                runtimeChildren.Add(control);
                AddChildren(
                    control.Controls,
                    child.Children,
                    controls,
                    sequential: control is FlowLayoutPanel or TableLayoutPanel);
            }

            if (runtimeChildren.Owner is TableLayoutPanel table)
            {
                foreach (var child in designChildren)
                    ApplyTablePlacement(table, controls[child], child);
            }
        }

        private static void CreateControls(
            IEnumerable<DesignControlNode> nodes,
            IDictionary<DesignControlNode, Control> controls)
        {
            foreach (var node in nodes)
            {
                Control control = NormalizeTypeName(node.TypeName) switch
                {
                    "Button" => new Button(),
                    "Ellipse" => new Ellipse(),
                    "FlowLayoutPanel" => new FlowLayoutPanel(),
                    "Label" => new Label(),
                    "ScrollableControl" => new ScrollableControl(),
                    "TableLayoutPanel" => new TableLayoutPanel(),
                    "TextBox" => new TextBox(),
                    "UserControl" => new UserControl(),
                    _ => new Panel()
                };

                control.Name = node.Name;
                control.Bounds = ToRectangle(node.Bounds);
                ApplyProperties(control, node.Properties);
                controls.Add(node, control);
                CreateControls(node.Children, controls);
            }
        }

        private static void ApplyRootProperties(object root, DesignDocument document)
            => ApplyProperties(root, document.Properties);

        private static void ApplyProperties(
            object target,
            IReadOnlyDictionary<string, DesignPropertyValue> properties)
        {
            foreach (var property in properties)
            {
                if (property.Key is "TableColumn" or "TableRow" or "TableColumnSpan" or "TableRowSpan")
                    continue;

                if (property.Key == LayoutTransitionDesignValue.PropertyName
                    && target is Control control
                    && LayoutTransitionDesignValue.TryRead(
                        property.Value,
                        out var enabled,
                        out var durationMilliseconds,
                        out _,
                        out _))
                {
                    control.LayoutTransition = new LayoutTransition
                    {
                        Enabled = enabled,
                        Duration = TimeSpan.FromMilliseconds(durationMilliseconds)
                    };
                    continue;
                }

                var runtimeProperty = target.GetType().GetProperty(
                    property.Key,
                    BindingFlags.Instance | BindingFlags.Public);
                if (runtimeProperty?.SetMethod is not { IsPublic: true })
                    continue;

                var value = DesignerPropertyValueEditor.FromDesignPropertyValue(property.Value, runtimeProperty.PropertyType);
                runtimeProperty.SetValue(target, value);
            }
        }

        private static void ApplyTablePlacement(TableLayoutPanel table, Control control, DesignControlNode node)
        {
            if (TryGetInt(node, "TableColumn", out var column))
                table.SetColumn(control, column);
            if (TryGetInt(node, "TableRow", out var row))
                table.SetRow(control, row);
            if (TryGetInt(node, "TableColumnSpan", out var columnSpan))
                table.SetColumnSpan(control, columnSpan);
            if (TryGetInt(node, "TableRowSpan", out var rowSpan))
                table.SetRowSpan(control, rowSpan);
        }

        private static bool TryGetInt(DesignControlNode node, string name, out int value)
        {
            value = 0;
            if (!node.Properties.TryGetValue(name, out var property))
                return false;

            value = Convert.ToInt32(property.Value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        private static void AttachChildren(
            IReadOnlyList<DesignControlNode> designChildren,
            Control.ControlCollection runtimeChildren,
            IDictionary<DesignControlNode, Control> controls)
        {
            foreach (var node in designChildren)
            {
                var control = runtimeChildren.Single(candidate => string.Equals(candidate.Name, node.Name, StringComparison.Ordinal));
                controls.Add(node, control);
                AttachChildren(node.Children, control.Controls, controls);
            }
        }

        private static void PerformLayoutRecursively(Control control)
        {
            control.PerformLayout();
            foreach (var child in control.Controls)
                PerformLayoutRecursively(child);
        }

        private static Rectangle Offset(Rectangle rectangle, Point offset)
        {
            rectangle.Offset(offset);
            return rectangle;
        }

        private static Rectangle Intersect(Rectangle first, Rectangle second)
        {
            var left = Math.Max(first.Left, second.Left);
            var top = Math.Max(first.Top, second.Top);
            var right = Math.Min(first.Right, second.Right);
            var bottom = Math.Min(first.Bottom, second.Bottom);
            return right <= left || bottom <= top
                ? new Rectangle(left, top, 0, 0)
                : Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static Size ToSize(DesignSize size)
            => new(size.Width, size.Height);
    }
}
