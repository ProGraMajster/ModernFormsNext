using System.Drawing;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerPaddingLayoutTests
{
    [Fact]
    public void FillUsesParentsAsymmetricPaddedDisplayRectangle()
    {
        var document = CreateDocument(new Padding(10, 20, 30, 40), DockStyle.Fill);

        AssertPreviewAndRuntimeMatch(document);
        Assert.Equal(new DesignBounds(10, 20, 260, 140), PreviewBounds(document, "child1"));
    }

    [Theory]
    [InlineData(DockStyle.Top)]
    [InlineData(DockStyle.Bottom)]
    [InlineData(DockStyle.Left)]
    [InlineData(DockStyle.Right)]
    public void RepeatedEdgeDockingStartsInsidePaddingAndMatchesRuntime(DockStyle dock)
    {
        var document = CreateDocument(new Padding(10, 20, 30, 40), dock, dock, dock);

        AssertPreviewAndRuntimeMatch(document);

        var first = PreviewBounds(document, "child1");
        Assert.Equal(dock == DockStyle.Right ? 240 : 10, first.X);
        Assert.Equal(dock == DockStyle.Bottom ? 130 : 20, first.Y);
    }

    [Theory]
    [MemberData(nameof(MixedDockCases))]
    public void MixedDockingConsumesOnlyThePaddedRectangle(DockStyle[] docks)
    {
        var document = CreateDocument(new Padding(10, 20, 30, 40), docks);

        AssertPreviewAndRuntimeMatch(document);

        foreach (var child in document.Controls[0].Children)
        {
            var bounds = PreviewBounds(document, child.Name);
            Assert.True(bounds.X >= 10);
            Assert.True(bounds.Y >= 20);
            Assert.True(bounds.Right <= 270);
            Assert.True(bounds.Bottom <= 160);
        }
    }

    [Theory]
    [InlineData("Panel")]
    [InlineData("UserControl")]
    public void NestedContainersUseOnlyTheirImmediateParentsPadding(string innerTypeName)
    {
        var document = CreateDocument(new Padding(10), DockStyle.Fill);
        var outer = document.Controls[0];
        var inner = outer.Children[0];
        inner.TypeName = innerTypeName;
        inner.Name = "innerPanel";
        inner.Properties["Padding"] = PaddingValue(new Padding(5, 10, 15, 20));
        inner.Children.Add(CreateDockedNode("textBox1", "TextBox", DockStyle.Fill, 40));

        AssertPreviewAndRuntimeMatch(document);
        Assert.Equal(new DesignBounds(10, 10, 280, 180), PreviewBounds(document, "innerPanel"));
        Assert.Equal(new DesignBounds(15, 20, 260, 150), PreviewBounds(document, "textBox1"));
    }

    [Fact]
    public void AnchorKeepsRuntimeCoordinatesWhenParentHasPadding()
    {
        var document = CreateDocument(new Padding(10, 20, 30, 40), DockStyle.None);
        var child = document.Controls[0].Children[0];
        child.Bounds = new DesignBounds(25, 35, 50, 40);
        child.Properties["Anchor"] = DesignPropertyValue.FromEnum(
            typeof(AnchorStyles).FullName!,
            $"{nameof(AnchorStyles.Right)}, {nameof(AnchorStyles.Bottom)}");

        AssertPreviewAndRuntimeMatch(document);
        Assert.Equal(new DesignBounds(25, 35, 50, 40), PreviewBounds(document, child.Name));
    }

    [Theory]
    [InlineData(DockStyle.Fill)]
    [InlineData(DockStyle.Top)]
    [InlineData(DockStyle.Left)]
    public void DockKeepsRuntimeMarginSemantics(DockStyle dock)
    {
        var document = CreateDocument(new Padding(10, 20, 30, 40), dock);
        document.Controls[0].Children[0].Properties["Margin"] = PaddingValue(new Padding(7, 11, 13, 17));

        AssertPreviewAndRuntimeMatch(document);

        var bounds = PreviewBounds(document, "child1");
        Assert.Equal(10, bounds.X);
        Assert.Equal(20, bounds.Y);
    }

    [Theory]
    [MemberData(nameof(PaddingCases))]
    public void ZeroUniformVerticalAndAsymmetricPaddingMatchRuntime(Padding padding, DesignBounds expected)
    {
        var document = CreateDocument(padding, DockStyle.Fill);

        AssertPreviewAndRuntimeMatch(document);
        Assert.Equal(expected, PreviewBounds(document, "child1"));
    }

    [Fact]
    public void NegativePaddingUsesRuntimeNormalization()
    {
        var document = CreateDocument(new Padding(-5, 10, -15, -20), DockStyle.Fill);

        AssertPreviewAndRuntimeMatch(document);
        Assert.Equal(new DesignBounds(0, 10, 300, 190), PreviewBounds(document, "child1"));
    }

    [Fact]
    public void OversizedPaddingDoesNotProduceNegativeDesignerBounds()
    {
        var document = CreateDocument(new Padding(200, 150, 200, 150), DockStyle.Fill);

        var exception = Record.Exception(() => new DesignerLayoutEngine().Layout(document));
        var bounds = PreviewBounds(document, "child1");
        Control? runtimeRoot = null;
        var runtimeException = Record.Exception(() => runtimeRoot = BuildRuntimeTree(document, out _));

        Assert.Null(exception);
        Assert.Null(runtimeException);
        Assert.Equal(new DesignBounds(200, 150, 0, 0), bounds);
        runtimeRoot?.Dispose();
    }

    [Fact]
    public void PropertyGridPaddingEditsRelayoutImmediatelyAndResetWithoutStaleGeometry()
    {
        var document = CreateDocument(Padding.Empty, DockStyle.Fill);
        var parent = document.Controls[0];
        var child = parent.Children[0];
        var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(parent);
        var propertyGrid = new DesignerPropertyGridState(session);
        var documentChanges = 0;
        session.DocumentChanged += (_, _) => documentChanges++;

        Assert.Equal(new DesignBounds(0, 0, 300, 200), PreviewBounds(document, child.Name));

        CommitPadding(propertyGrid, "10, 10, 10, 10");
        Assert.Equal(new DesignBounds(10, 10, 280, 180), PreviewBounds(document, child.Name));

        CommitPadding(propertyGrid, "5, 10, 15, 20");
        Assert.Equal(new DesignBounds(5, 10, 280, 170), PreviewBounds(document, child.Name));

        var hitTest = new DesignerHitTestService(new DesignerCoordinateMapper());
        Assert.Same(parent, hitTest.HitTestControl(session, new DesignPoint(2, 2)).Node);
        Assert.Same(child, hitTest.HitTestControl(session, new DesignPoint(6, 11)).Node);
        session.SelectAt(2, 2);
        Assert.Same(parent, session.SelectedNode);
        session.SelectAt(6, 11);
        Assert.Same(child, session.SelectedNode);
        session.SelectNode(parent);

        CommitPadding(propertyGrid, "0, 0, 0, 0");
        Assert.Equal(new DesignBounds(0, 0, 300, 200), PreviewBounds(document, child.Name));
        Assert.Equal(3, documentChanges);
    }

    [Fact]
    public void ResizeHandleHitTestingUsesRelayoutBoundsAfterPaddingChange()
    {
        var document = CreateDocument(Padding.Empty, DockStyle.Top);
        var parent = document.Controls[0];
        var child = parent.Children[0];
        var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(child);
        const int surfaceWidth = 900;
        const int surfaceHeight = 700;
        var mapper = new DesignerCoordinateMapper();
        var hitTest = new DesignerHitTestService(mapper);
        var oldBounds = mapper.ToSurfaceBounds(
            new DesignerLayoutEngine().Layout(document).GetEffectiveBounds(child),
            mapper.GetView(session, surfaceWidth, surfaceHeight));
        var oldHandle = DesignerHitTestService.GetHandleBounds(oldBounds, DesignerResizeHandle.Bottom);

        parent.Properties["Padding"] = PaddingValue(new Padding(10, 20, 30, 40));
        session.NotifyDocumentChanged();
        var view = mapper.GetView(session, surfaceWidth, surfaceHeight);
        var newBounds = mapper.ToSurfaceBounds(new DesignerLayoutEngine().Layout(document).GetEffectiveBounds(child), view);
        var newHandle = DesignerHitTestService.GetHandleBounds(newBounds, DesignerResizeHandle.Bottom);

        Assert.Equal(
            DesignerResizeHandle.None,
            hitTest.HitTestResizeHandle(session, surfaceWidth, surfaceHeight, oldHandle.Left + oldHandle.Width / 2f, oldHandle.Top + oldHandle.Height / 2f));
        Assert.Equal(
            DesignerResizeHandle.Bottom,
            hitTest.HitTestResizeHandle(session, surfaceWidth, surfaceHeight, newHandle.Left + newHandle.Width / 2f, newHandle.Top + newHandle.Height / 2f));
    }

    [Fact]
    public void ParentResizeKeepsPaddedFillBoundsWithoutDrift()
    {
        var document = CreateDocument(new Padding(10, 20, 30, 40), DockStyle.Fill);
        var parent = document.Controls[0];

        foreach (var size in new[] { new DesignSize(360, 240), new DesignSize(500, 320), new DesignSize(300, 200) })
        {
            parent.Bounds = new DesignBounds(0, 0, size.Width, size.Height);
            document.Size = size;
            AssertPreviewAndRuntimeMatch(document);
            Assert.Equal(
                new DesignBounds(10, 20, size.Width - 40, size.Height - 60),
                PreviewBounds(document, "child1"));
        }
    }

    [Fact]
    public void UserControlRootAndFormRootKeepTheirRuntimePaddingSemantics()
    {
        var userControl = CreateRootDocument(DesignRootKind.UserControl, new Padding(10, 20, 30, 40));
        var form = CreateRootDocument(DesignRootKind.Form, new Padding(10, 20, 30, 40));

        AssertPreviewAndRuntimeMatch(userControl);
        Assert.Equal(new DesignBounds(10, 20, 260, 140), PreviewBounds(userControl, "rootChild"));
        Assert.Equal(new DesignBounds(0, 0, 300, 200), PreviewBounds(form, "rootChild"));
    }

    [Fact]
    public void SerializationGenerationAndReverseParsingPreservePaddingMarginAndHierarchy()
    {
        var document = CreateDocument(new Padding(5, 10, 15, 20), DockStyle.Fill);
        var child = document.Controls[0].Children[0];
        child.Properties["Margin"] = PaddingValue(new Padding(1, 2, 3, 4));
        var serializer = DesignDocumentSerializer.Default;
        var reopened = serializer.Deserialize(serializer.Serialize(document));
        var generated = new CSharpDesignerGenerator().Generate(reopened);

        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Contains("this.panel1.Padding = new Padding(5, 10, 15, 20);", generated.Code, StringComparison.Ordinal);
        Assert.Contains("this.child1.Margin = new Padding(1, 2, 3, 4);", generated.Code, StringComparison.Ordinal);

        var parsed = new CSharpDesignerParser().Parse(generated.Code);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var parsedDocument = Assert.IsType<DesignDocument>(parsed.Document);
        var parsedParent = Assert.Single(parsedDocument.Controls);
        var parsedChild = Assert.Single(parsedParent.Children);

        Assert.Equal("panel1", parsedParent.Name);
        Assert.Equal("child1", parsedChild.Name);
        Assert.Equal(new Padding(5, 10, 15, 20), ReadPadding(parsedParent, "Padding"));
        Assert.Equal(new Padding(1, 2, 3, 4), ReadPadding(parsedChild, "Margin"));
        Assert.Equal(DockStyle.Fill, DesignerLayoutProperties.GetDock(parsedChild));
    }

    public static TheoryData<DockStyle[]> MixedDockCases => new()
    {
        new[] { DockStyle.Top, DockStyle.Fill },
        new[] { DockStyle.Bottom, DockStyle.Fill },
        new[] { DockStyle.Left, DockStyle.Fill },
        new[] { DockStyle.Right, DockStyle.Fill },
        new[] { DockStyle.Top, DockStyle.Bottom, DockStyle.Fill },
        new[] { DockStyle.Left, DockStyle.Right, DockStyle.Fill }
    };

    public static TheoryData<Padding, DesignBounds> PaddingCases => new()
    {
        { Padding.Empty, new DesignBounds(0, 0, 300, 200) },
        { new Padding(10), new DesignBounds(10, 10, 280, 180) },
        { new Padding(0, 12, 0, 12), new DesignBounds(0, 12, 300, 176) },
        { new Padding(5, 10, 15, 20), new DesignBounds(5, 10, 280, 170) }
    };

    private static DesignDocument CreateDocument(Padding padding, params DockStyle[] docks)
    {
        var document = new DesignDocument
        {
            Namespace = "PaddingLayoutReproduction",
            ClassName = "MainForm",
            FormName = "MainForm",
            Size = new DesignSize(300, 200)
        };
        var parent = CreateDockedNode("panel1", "Panel", DockStyle.Fill, 200);
        parent.Bounds = new DesignBounds(0, 0, 300, 200);
        parent.Properties["Padding"] = PaddingValue(padding);

        for (var index = 0; index < docks.Length; index++)
            parent.Children.Add(CreateDockedNode($"child{index + 1}", index == 0 && docks.Length == 1 ? "TextBox" : "Panel", docks[index], 30 + (index * 5)));

        document.Controls.Add(parent);
        return document;
    }

    private static DesignDocument CreateRootDocument(DesignRootKind rootKind, Padding padding)
    {
        var document = new DesignDocument
        {
            Namespace = "PaddingLayoutReproduction",
            ClassName = rootKind == DesignRootKind.UserControl ? "RootControl" : "MainForm",
            FormName = rootKind == DesignRootKind.UserControl ? "RootControl" : "MainForm",
            RootKind = rootKind,
            Size = new DesignSize(300, 200)
        };
        document.Properties["Padding"] = PaddingValue(padding);
        document.Controls.Add(CreateDockedNode("rootChild", "Panel", DockStyle.Fill, 30));
        return document;
    }

    private static DesignControlNode CreateDockedNode(string name, string typeName, DockStyle dock, int thickness)
        => new()
        {
            TypeName = typeName,
            Name = name,
            Bounds = new DesignBounds(0, 0, thickness, thickness),
            Properties =
            {
                ["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, dock.ToString())
            }
        };

    private static DesignPropertyValue PaddingValue(Padding padding)
        => DesignerPropertyValueEditor.ToDesignPropertyValue(padding, typeof(Padding));

    private static Padding ReadPadding(DesignControlNode node, string propertyName)
        => Assert.IsType<Padding>(DesignerPropertyValueEditor.FromDesignPropertyValue(node.Properties[propertyName], typeof(Padding)));

    private static void CommitPadding(DesignerPropertyGridState propertyGrid, string value)
    {
        var property = Assert.Single(propertyGrid.Properties, candidate => candidate.Name == "Padding");
        propertyGrid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.True(propertyGrid.CommitSelectedValue(value));
    }

    private static void AssertPreviewAndRuntimeMatch(DesignDocument document)
    {
        var preview = new DesignerLayoutEngine().Layout(document);
        using var root = BuildRuntimeTree(document, out var runtimeControls);

        foreach (var node in Enumerate(document.Controls))
        {
            var expected = preview.GetEffectiveBounds(node);
            var actual = GetAbsoluteBounds(runtimeControls[node]);
            Assert.Equal(new Rectangle(expected.X, expected.Y, expected.Width, expected.Height), actual);
        }
    }

    private static Control BuildRuntimeTree(
        DesignDocument document,
        out Dictionary<DesignControlNode, Control> controls)
    {
        controls = new Dictionary<DesignControlNode, Control>();
        Control root = document.RootKind == DesignRootKind.UserControl ? new UserControl() : new Panel();
        root.Size = new Size(document.Size.Width, document.Size.Height);
        root.SuspendLayout();

        for (var index = document.Controls.Count - 1; index >= 0; index--)
            root.Controls.Add(CreateRuntimeControl(document.Controls[index], controls));

        if (document.RootKind == DesignRootKind.UserControl)
            root.Padding = DesignerLayoutProperties.GetPadding(document.Properties);

        root.ResumeLayout(true);
        PerformLayoutRecursively(root);
        return root;
    }

    private static Control CreateRuntimeControl(
        DesignControlNode node,
        IDictionary<DesignControlNode, Control> controls)
    {
        Control control = node.TypeName switch
        {
            "TextBox" => new TextBox(),
            "Label" => new Label(),
            "UserControl" => new UserControl(),
            _ => new Panel()
        };
        control.Name = node.Name;
        control.Bounds = new Rectangle(node.Bounds.X, node.Bounds.Y, node.Bounds.Width, node.Bounds.Height);

        if (node.Properties.ContainsKey("Anchor"))
            control.Anchor = DesignerLayoutProperties.GetAnchor(node);
        control.Dock = DesignerLayoutProperties.GetDock(node);
        control.Padding = DesignerLayoutProperties.GetPadding(node);
        if (node.Properties.TryGetValue("Margin", out var margin))
            control.Margin = Assert.IsType<Padding>(DesignerPropertyValueEditor.FromDesignPropertyValue(margin, typeof(Padding)));

        controls.Add(node, control);
        control.SuspendLayout();
        for (var index = node.Children.Count - 1; index >= 0; index--)
            control.Controls.Add(CreateRuntimeControl(node.Children[index], controls));
        control.ResumeLayout(true);
        return control;
    }

    private static void PerformLayoutRecursively(Control control)
    {
        control.PerformLayout();
        foreach (var child in control.Controls)
            PerformLayoutRecursively(child);
    }

    private static Rectangle GetAbsoluteBounds(Control control)
    {
        var bounds = control.Bounds;
        for (var parent = control.Parent; parent?.Parent is not null; parent = parent.Parent)
            bounds.Offset(parent.Left, parent.Top);
        return bounds;
    }

    private static DesignBounds PreviewBounds(DesignDocument document, string name)
    {
        var node = Enumerate(document.Controls).Single(candidate => candidate.Name == name);
        return new DesignerLayoutEngine().Layout(document).GetEffectiveBounds(node);
    }

    private static IEnumerable<DesignControlNode> Enumerate(IEnumerable<DesignControlNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Enumerate(node.Children))
                yield return child;
        }
    }
}
