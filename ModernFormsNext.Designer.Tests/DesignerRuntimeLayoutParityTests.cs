using System.Drawing;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerRuntimeLayoutParityTests
{
    private static int nextArtifactId;

    [Theory]
    [MemberData(nameof(CoreParityScenarios))]
    public void CoreScenarioMatchesProductionRuntimeLayout(ParityScenario scenario)
        => DesignerRuntimeLayoutParityHarness.AssertParity(scenario);

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void LogicalParityKeepsIdenticalDeviceEdgesAtSupportedDpiScales(double dpiScale)
    {
        var scenario = new ParityScenario(
            "dpi-asymmetric-padding-top-fill",
            () => DockDocument([DockStyle.Top, DockStyle.Fill], new Padding(7, 11, 13, 17)));

        DesignerRuntimeLayoutParityHarness.AssertDpiParity(scenario, dpiScale);
    }

    [Theory]
    [MemberData(nameof(PropertyGridLiveEdits))]
    public void PropertyGridLiveEditRelayoutsLikeRuntime(string propertyName, string value, DesignSize? layoutSize)
    {
        var document = BasicDocument();
        var child = Node("child", "Panel", 20, 30, 80, 50);
        document.Controls.Add(child);
        var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(child);
        var propertyGrid = new DesignerPropertyGridState(session);
        var property = Assert.Single(propertyGrid.Properties, candidate => candidate.Name == propertyName);
        propertyGrid.SelectRow(new DesignerPropertyGridRow(property));

        Assert.True(propertyGrid.CommitSelectedValue(value));
        DesignerRuntimeLayoutParityHarness.AssertParity(
            $"property-grid-{propertyName}",
            document,
            document,
            layoutSize);
    }

    [Fact]
    public void RootPropertyGridResizePersistsAnchoredGeometryWithoutDrift()
    {
        var document = BasicDocument();
        var child = Node("anchored", "Panel", 200, 130, 80, 50);
        child.Properties["Anchor"] = EnumValue(AnchorStyles.Right | AnchorStyles.Bottom);
        document.Controls.Add(child);
        var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(null);
        var propertyGrid = new DesignerPropertyGridState(session);

        Commit(propertyGrid, "Width", "420");
        Commit(propertyGrid, "Height", "310");
        DesignerRuntimeLayoutParityHarness.AssertParity("root-resize-grow", document, document);

        Commit(propertyGrid, "Width", "300");
        Commit(propertyGrid, "Height", "200");
        DesignerRuntimeLayoutParityHarness.AssertParity("root-resize-return", document, document);

        Assert.Equal(new DesignBounds(200, 130, 80, 50), child.Bounds);
    }

    [Theory]
    [MemberData(nameof(RoundTripScenarios))]
    public void SaveAndReopenMfdesignPreservesParity(ParityScenario scenario)
    {
        var original = scenario.CreateDocument();
        var artifactId = Interlocked.Increment(ref nextArtifactId);
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"ModernFormsNext-layout-parity-{Environment.ProcessId}-{artifactId}.mfdesign");

        try
        {
            DesignDocumentSerializer.Default.Save(path, original);
            var reopened = DesignDocumentSerializer.Default.Load(path);
            DesignerRuntimeLayoutParityHarness.AssertParity(
                $"save-reopen-{scenario.Name}",
                reopened,
                original,
                scenario.LayoutSize);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [MemberData(nameof(CodeGenerationScenarios))]
    public void GeneratedDesignerCodeBuildsAndRunsWithParity(ParityScenario scenario)
    {
        var document = scenario.CreateDocument();
        using var generatedRoot = CompileAndCreateGeneratedRoot(document);

        DesignerRuntimeLayoutParityHarness.AssertParity(
            $"codegen-{scenario.Name}",
            document,
            generatedRoot.Instance);
    }

    [Theory]
    [MemberData(nameof(ReverseParserScenarios))]
    public void ReverseParserReconstructsDesignerGeometryEquivalentToRuntime(ParityScenario scenario)
    {
        var runtimeDocument = scenario.CreateDocument();
        var generated = new CSharpDesignerGenerator().Generate(runtimeDocument);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        var parsed = new CSharpDesignerParser().Parse(
            generated.Code,
            new CSharpDesignerParseOptions
            {
                RootKind = runtimeDocument.RootKind,
                NamespaceOverride = runtimeDocument.Namespace,
                ClassNameOverride = runtimeDocument.ClassName
            });

        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(diagnostic => diagnostic.Message)));
        DesignerRuntimeLayoutParityHarness.AssertParity(
            $"reverse-parser-{scenario.Name}",
            Assert.IsType<DesignDocument>(parsed.Document),
            runtimeDocument,
            scenario.LayoutSize);
    }

    [Fact]
    public void HitTestingUsesFinalVisibleClippedParityGeometry()
    {
        var document = BasicDocument();
        var panel = Node("panel", "Panel", 20, 20, 140, 100);
        panel.Properties["Padding"] = PaddingValue(new Padding(10));
        var child = Node("child", "Panel", 0, 0, 40, 30, DockStyle.Fill);
        panel.Children.Add(child);
        document.Controls.Add(panel);
        var session = new DesignerSession();
        session.LoadDocument(document);
        var hitTest = new DesignerHitTestService(new DesignerCoordinateMapper());

        Assert.Same(panel, hitTest.HitTestControl(session, new DesignPoint(25, 25)).Node);
        Assert.Same(child, hitTest.HitTestControl(session, new DesignPoint(31, 31)).Node);
        Assert.Same(panel, hitTest.HitTestControl(session, new DesignPoint(159, 119)).Node);
        Assert.Null(hitTest.HitTestControl(session, new DesignPoint(160, 120)).Node);

        child.Properties["Visible"] = DesignPropertyValue.FromBoolean(false);
        session.NotifyDocumentChanged();
        Assert.Same(panel, hitTest.HitTestControl(session, new DesignPoint(31, 31)).Node);
    }

    [Fact]
    public void ChildReorderRemoveAndAddStayEquivalentToRuntimeOrder()
    {
        var document = DockDocument([DockStyle.Top, DockStyle.Top, DockStyle.Fill]);
        DesignerRuntimeLayoutParityHarness.AssertParity("order-initial", document, document);

        var moved = document.Controls[0];
        document.Controls.RemoveAt(0);
        document.Controls.Insert(1, moved);
        DesignerRuntimeLayoutParityHarness.AssertParity("order-reordered", document, document);

        var removed = document.Controls[0];
        document.Controls.RemoveAt(0);
        DesignerRuntimeLayoutParityHarness.AssertParity("order-removed", document, document);
        document.Controls.Add(removed);
        DesignerRuntimeLayoutParityHarness.AssertParity("order-added", document, document);
    }

    public static IEnumerable<object[]> CoreParityScenarios()
    {
        yield return Scenario("ordinary-child", () => WithChildren(Node("child", "Panel", 17, 23, 91, 47)));
        yield return Scenario("dock-fill", () => DockDocument([DockStyle.Fill]));
        yield return Scenario("dock-top", () => DockDocument([DockStyle.Top]));
        yield return Scenario("dock-bottom", () => DockDocument([DockStyle.Bottom]));
        yield return Scenario("dock-left", () => DockDocument([DockStyle.Left]));
        yield return Scenario("dock-right", () => DockDocument([DockStyle.Right]));
        yield return Scenario("dock-top-fill", () => DockDocument([DockStyle.Top, DockStyle.Fill]));
        yield return Scenario("dock-left-fill", () => DockDocument([DockStyle.Left, DockStyle.Fill]));
        yield return Scenario("dock-top-bottom-fill", () => DockDocument([DockStyle.Top, DockStyle.Bottom, DockStyle.Fill]));
        yield return Scenario("dock-left-right-fill", () => DockDocument([DockStyle.Left, DockStyle.Right, DockStyle.Fill]));
        yield return Scenario("dock-repeated-top", () => DockDocument([DockStyle.Top, DockStyle.Top, DockStyle.Top, DockStyle.Fill]));
        yield return Scenario("dock-mixed-sequence", () => DockDocument([DockStyle.Top, DockStyle.Left, DockStyle.Bottom, DockStyle.Right, DockStyle.Fill]));
        yield return Scenario("padding-zero", () => DockDocument([DockStyle.Fill], Padding.Empty));
        yield return Scenario("padding-uniform", () => DockDocument([DockStyle.Fill], new Padding(12)));
        yield return Scenario("padding-asymmetric-issue-31", () => DockDocument([DockStyle.Fill], new Padding(10, 20, 30, 40)));
        yield return Scenario("padding-negative-normalized", () => DockDocument([DockStyle.Fill], new Padding(-5, 10, -15, -20)));
        yield return Scenario("padding-margin-dock", PaddingAndMarginDocument);
        yield return Scenario("nested-padding", NestedPaddingDocument);
        yield return Scenario("usercontrol-root-padding", UserControlRootPaddingDocument);
        yield return AnchorScenario("anchor-left-top", AnchorStyles.Left | AnchorStyles.Top, new DesignSize(420, 310));
        yield return AnchorScenario("anchor-right-top", AnchorStyles.Right | AnchorStyles.Top, new DesignSize(420, 310));
        yield return AnchorScenario("anchor-left-right-top", AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, new DesignSize(420, 310));
        yield return AnchorScenario("anchor-left-top-bottom", AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom, new DesignSize(420, 310));
        yield return AnchorScenario("anchor-all", AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom, new DesignSize(420, 310));
        yield return AnchorScenario("anchor-none", AnchorStyles.None, new DesignSize(420, 310));
        yield return AnchorScenario("anchor-resize-wider", AnchorStyles.Right | AnchorStyles.Top, new DesignSize(420, 200));
        yield return AnchorScenario("anchor-resize-taller", AnchorStyles.Left | AnchorStyles.Bottom, new DesignSize(300, 310));
        yield return ConstraintScenario("minimum-size", minimum: new Size(120, 80), maximum: Size.Empty, DockStyle.None);
        yield return ConstraintScenario("maximum-size", minimum: Size.Empty, maximum: new Size(60, 35), DockStyle.None);
        yield return ConstraintScenario("minimum-maximum", minimum: new Size(70, 40), maximum: new Size(100, 60), DockStyle.None);
        yield return ConstraintScenario("dock-fill-minimum", minimum: new Size(340, 240), maximum: Size.Empty, DockStyle.Fill);
        yield return ConstraintScenario("dock-fill-maximum", minimum: Size.Empty, maximum: new Size(240, 150), DockStyle.Fill);
        yield return Scenario("nested-panel-chain", NestedPanelDocument);
        yield return Scenario("nested-usercontrol-chain", NestedUserControlDocument);
        yield return Scenario("nested-form-panel-chain", NestedFormPanelDocument);
        yield return Scenario("nested-form-usercontrol-chain", NestedFormUserControlDocument);
        yield return Scenario("nested-usercontrol-resize", NestedUserControlDocument, new DesignSize(420, 310));
        yield return Scenario("usercontrol-child", UserControlChildDocument);
        yield return Scenario("form-usercontrol-child", FormUserControlChildDocument);
        yield return Scenario("form-root-fill", () => FormDocument([DockStyle.Fill]));
        yield return Scenario("form-root-top-fill", () => FormDocument([DockStyle.Top, DockStyle.Fill]));
        yield return Scenario("form-root-padded-container", FormPaddedContainerDocument);
        yield return Scenario("form-root-anchor-resize", FormAnchorDocument, new DesignSize(420, 310));
        yield return Scenario("flow-left-to-right", () => FlowDocument());
        yield return Scenario("flow-top-down", () => FlowDocument(FlowDirection.TopDown));
        yield return Scenario("flow-wrap", FlowWrapDocument);
        yield return Scenario("table-two-by-two", TableDocument);
        yield return Scenario("hidden-dock-child", HiddenDockDocument);
        yield return Scenario("hidden-dock-middle", HiddenDockMiddleDocument);
        yield return Scenario("clipped-nested-child", ClippedChildDocument);
        yield return Scenario("shape-logical-animation-bounds", ShapeAnimationDocument);
    }

    public static IEnumerable<object[]> PropertyGridLiveEdits()
    {
        yield return ["Dock", "Fill", null!];
        yield return ["Anchor", "Top, Right", new DesignSize(420, 310)];
        yield return ["Margin", "1, 2, 3, 4", null!];
        yield return ["Padding", "5, 10, 15, 20", null!];
        yield return ["MinimumSize", "100, 70", null!];
        yield return ["MaximumSize", "60, 35", null!];
        yield return ["Visible", "false", null!];
        yield return ["X", "35", null!];
        yield return ["Y", "45", null!];
        yield return ["Width", "125", null!];
        yield return ["Height", "95", null!];
    }

    public static IEnumerable<object[]> RoundTripScenarios()
    {
        yield return Scenario("padding-dock", () => DockDocument([DockStyle.Top, DockStyle.Fill], new Padding(5, 10, 15, 20)));
        yield return Scenario("nested-usercontrol", NestedUserControlDocument);
        yield return Scenario("flow", () => FlowDocument());
    }

    public static IEnumerable<object[]> CodeGenerationScenarios()
    {
        yield return Scenario("usercontrol-padding-dock", () => DockDocument([DockStyle.Top, DockStyle.Fill], new Padding(5, 10, 15, 20)));
        yield return Scenario("usercontrol-nested-anchor", NestedUserControlDocument);
        yield return Scenario("form-client-area", () => FormDocument([DockStyle.Top, DockStyle.Fill]));
    }

    public static IEnumerable<object[]> ReverseParserScenarios()
    {
        yield return Scenario("padding-dock", () => DockDocument([DockStyle.Top, DockStyle.Fill], new Padding(5, 10, 15, 20)));
        yield return AnchorScenario("anchor", AnchorStyles.Right | AnchorStyles.Bottom, new DesignSize(420, 310));
        yield return Scenario("nested", NestedPanelDocument);
    }

    private static object[] Scenario(string name, Func<DesignDocument> factory, DesignSize? size = null)
        => [new ParityScenario(name, factory, size)];

    private static object[] AnchorScenario(string name, AnchorStyles anchor, DesignSize size)
        => Scenario(
            name,
            () =>
            {
                var document = BasicDocument();
                var child = Node("anchored", "Panel", 40, 50, 100, 60);
                child.Properties["Anchor"] = EnumValue(anchor);
                document.Controls.Add(child);
                return document;
            },
            size);

    private static object[] ConstraintScenario(string name, Size minimum, Size maximum, DockStyle dock)
        => Scenario(
            name,
            () =>
            {
                var document = BasicDocument();
                var child = Node("constrained", "Panel", 20, 30, 80, 50, dock);
                if (!minimum.IsEmpty)
                    child.Properties["MinimumSize"] = SizeValue(minimum);
                if (!maximum.IsEmpty)
                    child.Properties["MaximumSize"] = SizeValue(maximum);
                document.Controls.Add(child);
                return document;
            });

    private static DesignDocument BasicDocument(DesignRootKind rootKind = DesignRootKind.UserControl)
        => new()
        {
            Namespace = "ModernFormsNext.LayoutParity.Generated",
            ClassName = rootKind == DesignRootKind.Form ? "ParityForm" : "ParityControl",
            FormName = rootKind == DesignRootKind.Form ? "ParityForm" : "ParityControl",
            RootKind = rootKind,
            Size = new DesignSize(300, 200)
        };

    private static DesignDocument WithChildren(params DesignControlNode[] children)
    {
        var document = BasicDocument();
        foreach (var child in children)
            document.Controls.Add(child);
        return document;
    }

    private static DesignDocument DockDocument(IReadOnlyList<DockStyle> docks, Padding? padding = null)
    {
        var document = BasicDocument();
        if (padding is { } rootPadding)
            document.Properties["Padding"] = PaddingValue(rootPadding);

        for (var index = 0; index < docks.Count; index++)
        {
            var thickness = 24 + (index * 3);
            document.Controls.Add(Node($"child{index + 1}", "Panel", 11 + index, 17 + index, thickness, thickness, docks[index]));
        }
        return document;
    }

    private static DesignDocument FormDocument(IReadOnlyList<DockStyle> docks)
    {
        var document = BasicDocument(DesignRootKind.Form);
        for (var index = 0; index < docks.Count; index++)
            document.Controls.Add(Node($"formChild{index + 1}", "Panel", 0, 0, 30 + index, 30 + index, docks[index]));
        return document;
    }

    private static DesignDocument PaddingAndMarginDocument()
    {
        var document = DockDocument([DockStyle.Fill], new Padding(5, 10, 15, 20));
        document.Controls[0].Properties["Margin"] = PaddingValue(new Padding(1, 2, 3, 4));
        return document;
    }

    private static DesignDocument NestedPaddingDocument()
    {
        var document = BasicDocument();
        document.Properties["Padding"] = PaddingValue(new Padding(4));
        var outer = Node("outer", "Panel", 0, 0, 100, 100, DockStyle.Fill);
        outer.Properties["Padding"] = PaddingValue(new Padding(5, 10, 15, 20));
        var inner = Node("inner", "Panel", 0, 0, 80, 80, DockStyle.Fill);
        inner.Properties["Padding"] = PaddingValue(new Padding(3, 6, 9, 12));
        inner.Children.Add(Node("leaf", "Panel", 0, 0, 20, 20, DockStyle.Fill));
        outer.Children.Add(inner);
        document.Controls.Add(outer);
        return document;
    }

    private static DesignDocument UserControlRootPaddingDocument()
    {
        var document = BasicDocument();
        document.Properties["Padding"] = PaddingValue(new Padding(10, 20, 30, 40));
        document.Controls.Add(Node("fill", "Panel", 0, 0, 20, 20, DockStyle.Fill));
        return document;
    }

    private static DesignDocument NestedPanelDocument()
        => NestedPanelDocument(DesignRootKind.UserControl);

    private static DesignDocument NestedFormPanelDocument()
        => NestedPanelDocument(DesignRootKind.Form);

    private static DesignDocument NestedPanelDocument(DesignRootKind rootKind)
    {
        var document = BasicDocument(rootKind);
        var outer = Node("outer", "Panel", 10, 10, 280, 180);
        var inner = Node("inner", "Panel", 15, 20, 220, 120);
        inner.Children.Add(Node("leaf", "Button", 25, 30, 90, 32));
        outer.Children.Add(inner);
        document.Controls.Add(outer);
        return document;
    }

    private static DesignDocument NestedUserControlDocument()
        => NestedUserControlDocument(DesignRootKind.UserControl);

    private static DesignDocument NestedFormUserControlDocument()
        => NestedUserControlDocument(DesignRootKind.Form);

    private static DesignDocument NestedUserControlDocument(DesignRootKind rootKind)
    {
        var document = BasicDocument(rootKind);
        var userControl = Node("card", "UserControl", 10, 10, 280, 180);
        userControl.Properties["Padding"] = PaddingValue(new Padding(8));
        var panel = Node("content", "Panel", 0, 0, 100, 100, DockStyle.Fill);
        panel.Children.Add(Node("anchored", "Button", 150, 100, 90, 32));
        panel.Children[0].Properties["Anchor"] = EnumValue(AnchorStyles.Right | AnchorStyles.Bottom);
        userControl.Children.Add(panel);
        document.Controls.Add(userControl);
        return document;
    }

    private static DesignDocument UserControlChildDocument()
        => UserControlChildDocument(DesignRootKind.UserControl);

    private static DesignDocument FormUserControlChildDocument()
        => UserControlChildDocument(DesignRootKind.Form);

    private static DesignDocument UserControlChildDocument(DesignRootKind rootKind)
    {
        var document = BasicDocument(rootKind);
        var child = Node("childUserControl", "UserControl", 20, 25, 200, 120);
        child.Properties["Padding"] = PaddingValue(new Padding(7));
        child.Children.Add(Node("fill", "Panel", 0, 0, 20, 20, DockStyle.Fill));
        document.Controls.Add(child);
        return document;
    }

    private static DesignDocument FormPaddedContainerDocument()
    {
        var document = BasicDocument(DesignRootKind.Form);
        var panel = Node("content", "Panel", 0, 0, 20, 20, DockStyle.Fill);
        panel.Properties["Padding"] = PaddingValue(new Padding(10, 20, 30, 40));
        panel.Children.Add(Node("fill", "Panel", 0, 0, 20, 20, DockStyle.Fill));
        document.Controls.Add(panel);
        return document;
    }

    private static DesignDocument FormAnchorDocument()
    {
        var document = BasicDocument(DesignRootKind.Form);
        var child = Node("anchored", "Panel", 200, 130, 80, 50);
        child.Properties["Anchor"] = EnumValue(AnchorStyles.Right | AnchorStyles.Bottom);
        document.Controls.Add(child);
        return document;
    }

    private static DesignDocument FlowDocument(FlowDirection direction = FlowDirection.LeftToRight)
    {
        var document = BasicDocument();
        var flow = Node("flow", "FlowLayoutPanel", 0, 0, 20, 20, DockStyle.Fill);
        flow.Properties["FlowDirection"] = EnumValue(direction);
        flow.Children.Add(Node("first", "Panel", 0, 0, 40, 20));
        flow.Children.Add(Node("second", "Panel", 0, 0, 50, 30));
        flow.Children.Add(Node("third", "Panel", 0, 0, 30, 25));
        document.Controls.Add(flow);
        return document;
    }

    private static DesignDocument FlowWrapDocument()
    {
        var document = BasicDocument();
        document.Size = new DesignSize(120, 100);
        var flow = Node("flow", "FlowLayoutPanel", 0, 0, 20, 20, DockStyle.Fill);
        flow.Children.Add(Node("first", "Panel", 0, 0, 70, 20));
        flow.Children.Add(Node("second", "Panel", 0, 0, 70, 25));
        document.Controls.Add(flow);
        return document;
    }

    private static DesignDocument TableDocument()
    {
        var document = BasicDocument();
        var table = Node("table", "TableLayoutPanel", 0, 0, 20, 20, DockStyle.Fill);
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(2);
        table.Properties["RowCount"] = DesignPropertyValue.FromInt32(2);
        table.Children.Add(TableChild("topLeft", 0, 0));
        table.Children.Add(TableChild("topRight", 1, 0));
        table.Children.Add(TableChild("bottomLeft", 0, 1));
        table.Children.Add(TableChild("bottomRight", 1, 1));
        document.Controls.Add(table);
        return document;
    }

    private static DesignControlNode TableChild(string name, int column, int row)
    {
        var child = Node(name, "Panel", 0, 0, 40, 30);
        child.Properties["TableColumn"] = DesignPropertyValue.FromInt32(column);
        child.Properties["TableRow"] = DesignPropertyValue.FromInt32(row);
        return child;
    }

    private static DesignDocument HiddenDockDocument()
    {
        var document = DockDocument([DockStyle.Top, DockStyle.Fill]);
        document.Controls[0].Properties["Visible"] = DesignPropertyValue.FromBoolean(false);
        return document;
    }

    private static DesignDocument HiddenDockMiddleDocument()
    {
        var document = DockDocument([DockStyle.Top, DockStyle.Top, DockStyle.Fill]);
        document.Controls[1].Properties["Visible"] = DesignPropertyValue.FromBoolean(false);
        return document;
    }

    private static DesignDocument ClippedChildDocument()
    {
        var document = BasicDocument();
        var parent = Node("clipParent", "Panel", 20, 25, 100, 80);
        parent.Children.Add(Node("overflow", "Panel", 70, 55, 60, 50));
        document.Controls.Add(parent);
        return document;
    }

    private static DesignDocument ShapeAnimationDocument()
    {
        var document = BasicDocument();
        var shape = Node("shape", "Ellipse", 0, 0, 40, 40, DockStyle.Fill);
        shape.Properties[LayoutTransitionDesignValue.PropertyName] =
            LayoutTransitionDesignValue.Create(enabled: true, durationMilliseconds: 0d, easing: "EaseOut");
        document.Controls.Add(shape);
        return document;
    }

    private static DesignControlNode Node(
        string name,
        string typeName,
        int x,
        int y,
        int width,
        int height,
        DockStyle dock = DockStyle.None)
    {
        var node = new DesignControlNode
        {
            Name = name,
            TypeName = typeName,
            Bounds = new DesignBounds(x, y, width, height)
        };
        node.Properties["Dock"] = EnumValue(dock);
        return node;
    }

    private static DesignPropertyValue EnumValue<T>(T value) where T : struct, Enum
        => DesignPropertyValue.FromEnum(typeof(T).FullName!, value.ToString());

    private static DesignPropertyValue PaddingValue(Padding value)
        => DesignerPropertyValueEditor.ToDesignPropertyValue(value, typeof(Padding));

    private static DesignPropertyValue SizeValue(Size value)
        => DesignerPropertyValueEditor.ToDesignPropertyValue(value, typeof(Size));

    private static void Commit(DesignerPropertyGridState propertyGrid, string propertyName, string value)
    {
        var property = Assert.Single(propertyGrid.Properties, candidate => candidate.Name == propertyName);
        propertyGrid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.True(propertyGrid.CommitSelectedValue(value));
    }

    private static GeneratedRoot CompileAndCreateGeneratedRoot(DesignDocument document)
    {
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        var baseType = document.RootKind == DesignRootKind.Form
            ? "ModernFormsNext.Form"
            : "ModernFormsNext.UserControl";
        var userCode = $$"""
            namespace {{document.Namespace}};
            public partial class {{document.ClassName}} : {{baseType}}
            {
                public {{document.ClassName}}() => InitializeComponent();
            }
            """;
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? [];
        var references = trustedAssemblies
            .Concat(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                .Select(assembly => assembly.Location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var artifactId = Interlocked.Increment(ref nextArtifactId);
        var assemblyName = $"ModernFormsNext.LayoutParity.Generated.P{Environment.ProcessId}.A{artifactId}";
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(generated.Code), CSharpSyntaxTree.ParseText(userCode)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(
            emit.Success,
            string.Join(Environment.NewLine, emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        stream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        var rootType = assembly.GetType($"{document.Namespace}.{document.ClassName}");
        Assert.NotNull(rootType);
        var instance = Activator.CreateInstance(rootType);
        Assert.NotNull(instance);
        return new GeneratedRoot(instance);
    }

    private sealed class GeneratedRoot(object instance) : IDisposable
    {
        public object Instance { get; } = instance;

        public void Dispose()
        {
            if (Instance is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
