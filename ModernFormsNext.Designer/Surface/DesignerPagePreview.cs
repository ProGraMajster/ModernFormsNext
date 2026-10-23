using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

// A short-lived adapter to the production Control tree, not a second page/layout model.
// It accepts only already-loaded framework types and authored data. No application assembly,
// route factory, binding, converter, event handler or lifecycle binding is installed.
internal sealed class DesignerPagePreview : IDisposable
{
    internal const int MaximumNodes = 256;
    internal const int MaximumDepth = 16;
    private readonly List<Control> owned = [];
    internal Dictionary<DesignControlNode, Control> Controls { get; } = [];
    internal Control Root { get; private set; } = null!;

    internal static bool IsPageContainer(DesignControlNode node)
        => DesignPageContainers.IsPage(node.TypeName) || DesignPageContainers.IsType(node.TypeName, "PageHost");

    internal static DesignerPagePreview? TryCreate(DesignControlNode node, Size size, out string? error)
    {
        error = null;
        var document = new DesignDocument();
        document.Controls.Add(node); // Borrowed data; never normalized or modified.
        var validation = new DesignDocumentValidator().ValidateStructure(document);
        if (!validation.IsValid) { error = string.Join(" ", validation.Errors); return null; }
        var pending = new Stack<(DesignControlNode Node, int Depth)>();
        pending.Push((node, 0));
        int count = 0;
        while (pending.TryPop(out var item))
        {
            if (++count > MaximumNodes || item.Depth > MaximumDepth)
            { error = $"Page preview supports at most {MaximumNodes} authored nodes and depth {MaximumDepth}."; return null; }
            var type = DesignerSurfaceRenderer.ResolveFrameworkControlType(item.Node.TypeName);
            if (type is null || type.IsAbstract || !typeof(Control).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null)
            { error = $"Page preview requires a known framework control: {item.Node.TypeName}."; return null; }
            foreach (var child in item.Node.Children) pending.Push((child, item.Depth + 1));
        }
        var preview = new DesignerPagePreview();
        try
        {
            var viewport = new PreviewViewport { Bounds = new Rectangle(Point.Empty, size) };
            viewport.Site = new PreviewSite(viewport);
            preview.owned.Add(viewport);
            preview.Root = preview.Create(node);
            preview.Initialize(node);
            preview.Root.Bounds = new Rectangle(Point.Empty, size);
            viewport.Controls.Add(preview.Root);
            preview.Attach(node);
            Layout(viewport);
            return preview;
        }
        catch (Exception exception)
        {
            preview.Dispose();
            error = $"Page preview: {exception.GetType().Name}: {exception.Message}";
            return null;
        }
    }

    private Control Create(DesignControlNode node, Control? existing = null)
    {
        Control? navigationRoot = DesignPageContainers.IsType(node.TypeName, "NavigationPage") && node.Children.Count == 1
            ? Create(node.Children[0]) : null;
        var control = existing ?? (navigationRoot is Page page ? new NavigationPage(page)
            : (Control)Activator.CreateInstance(DesignerSurfaceRenderer.ResolveFrameworkControlType(node.TypeName)!)!);
        if (existing is null) owned.Add(control);
        Controls.Add(node, control);
        control.Site = new PreviewSite(control);
        if (control is SearchBar search) search.SuppressSuggestionRequests = true;
        foreach (var child in node.Children)
        {
            if (navigationRoot is not null) continue;
            Control? implicitPanel = control is SplitContainer split && DesignerSpecialContainers.IsSpecialGeneratedPart(child)
                ? DesignerSpecialContainers.IsSplitPanel1(child) ? split.Panel1 : split.Panel2 : null;
            Create(child, implicitPanel);
        }
        return control;
    }

    private void Initialize(DesignControlNode node)
    {
        var control = Controls[node];
        control.Name = node.Name;
        control.Bounds = new Rectangle(node.Bounds.X, node.Bounds.Y, node.Bounds.Width, node.Bounds.Height);
        if (!node.Properties.ContainsKey("Dock")) control.Dock = DockStyle.None;
        var errors = new List<string>();
        DesignerSurfaceRenderer.ApplyPreviewProperties(control, node, errors, includeLayout: true);
        if (errors.Count != 0) throw new InvalidOperationException(string.Join("; ", errors));
        foreach (var child in node.Children) Initialize(child);
    }

    private void Attach(DesignControlNode node)
    {
        // Match the generated initialization's parent-before-descendant admission.
        // Attaching a fully built subtree bottom-up changes retained inactive frame
        // geometry and can make preview disagree with the actual compiled document.
        var control = Controls[node];
        var children = node.Children.AsEnumerable();
        if (control is not (ContentPage or PageHost or NavigationPage or TabbedPage or FlyoutPage or FlowLayoutPanel or TableLayoutPanel or TabControl))
            children = children.Reverse();
        foreach (var child in children)
        {
            var childControl = Controls[child];
            if (control is not NavigationPage && !(control is SplitContainer && DesignerSpecialContainers.IsSpecialGeneratedPart(child)))
                switch (control)
            {
                case ContentPage content: content.Content = childControl; break;
                case PageHost host: host.Page = (Page)childControl; break;
                case TabbedPage tabs: tabs.Pages.Add((Page)childControl); break;
                case FlyoutPage flyout when DesignPageContainers.GetChildMember(node, child) == "Flyout": flyout.Flyout = (Page)childControl; break;
                case FlyoutPage flyout: flyout.Detail = (Page)childControl; break;
                case TabControl tabs when childControl is TabPage tab: tabs.TabPages.Add(tab); break;
                default: control.Controls.Add(childControl); break;
            }
            if (control is TableLayoutPanel table)
            {
                if (child.Properties.TryGetValue("TableColumn", out var column)) table.SetColumn(childControl, (int)column.Value!);
                if (child.Properties.TryGetValue("TableRow", out var row)) table.SetRow(childControl, (int)row.Value!);
                if (child.Properties.TryGetValue("TableColumnSpan", out var columnSpan)) table.SetColumnSpan(childControl, (int)columnSpan.Value!);
                if (child.Properties.TryGetValue("TableRowSpan", out var rowSpan)) table.SetRowSpan(childControl, (int)rowSpan.Value!);
            }
            Attach(child);
        }
        if (control is TabbedPage tabbed && node.Properties.TryGetValue("SelectedIndex", out var selected))
            tabbed.SelectedIndex = (int)selected.Value!;
    }

    private static void Layout(Control control)
    {
        control.PerformLayout();
        foreach (var child in control.Controls.GetAllControls()) Layout(child);
    }

    internal void Capture(Point origin, IDictionary<DesignControlNode, DesignBounds> bounds,
        IDictionary<DesignControlNode, DesignBounds> visibleBounds)
    {
        foreach (var pair in Controls)
        {
            var rectangle = pair.Value.Bounds;
            rectangle.Location = Point.Empty;
            for (Control? current = pair.Value; current is not null && !ReferenceEquals(current, Root); current = current.Parent)
                rectangle.Offset(current.Location);
            rectangle.Offset(origin);
            bounds[pair.Key] = ToBounds(rectangle);
            var visible = rectangle;
            for (Control? current = pair.Value; current is not null; current = current.Parent)
            {
                if (!current.Visible) { visible = Rectangle.Empty; break; }
                var clip = current.ClientRectangle;
                for (var ancestor = current; ancestor is not null && !ReferenceEquals(ancestor, Root); ancestor = ancestor.Parent)
                    clip.Offset(ancestor.Location);
                clip.Offset(origin);
                visible.Intersect(clip);
                if (ReferenceEquals(current, Root)) break;
            }
            visibleBounds[pair.Key] = ToBounds(visible);
        }
    }

    private static DesignBounds ToBounds(Rectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    public void Dispose()
    {
        // Ownership may include hidden retained pages and unattached controls after a failed
        // construction. Control.Dispose is idempotent; retire all of our allocations.
        for (int index = owned.Count - 1; index >= 0; index--) owned[index].Dispose();
        owned.Clear();
    }

    private sealed class PreviewSite(IComponent component) : ISite
    {
        public IComponent Component => component;
        public IContainer? Container => null;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type serviceType) => null;
    }

    // Detached ordinary controls inherit Visible=false from the absence of a parent.
    // This inert design viewport supplies the visible root contract without a native window,
    // command-routing root, application callback or production page lifecycle activation.
    private sealed class PreviewViewport : Control
    {
        public override bool Visible { get => true; set { } }
    }
}
