using System.Drawing;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerChildOrderTests
{
    [Fact]
    public void ThreeTopDockedPanelsWithFillLabelsMatchPreviewAndRuntime()
    {
        var document = CreateThreePanelDocument(DockStyle.Top);

        AssertPreviewAndRuntimeMatch(document);
        Assert.Equal(new DesignBounds(0, 0, 600, 120), PreviewBounds(document, "panel1"));
        Assert.Equal(new DesignBounds(0, 120, 600, 120), PreviewBounds(document, "panel2"));
        Assert.Equal(new DesignBounds(0, 240, 600, 120), PreviewBounds(document, "panel21"));
        Assert.Equal(PreviewBounds(document, "panel1"), PreviewBounds(document, "label1"));
        Assert.Equal(PreviewBounds(document, "panel2"), PreviewBounds(document, "label2"));
        Assert.Equal(PreviewBounds(document, "panel21"), PreviewBounds(document, "label3"));
    }

    [Theory]
    [InlineData(DockStyle.Bottom)]
    [InlineData(DockStyle.Left)]
    [InlineData(DockStyle.Right)]
    public void RepeatedEdgeDockingMatchesPreviewAndRuntime(DockStyle dock)
    {
        var document = CreateThreePanelDocument(dock);

        AssertPreviewAndRuntimeMatch(document);

        var first = PreviewBounds(document, "panel1");
        var second = PreviewBounds(document, "panel2");
        var third = PreviewBounds(document, "panel21");
        if (dock == DockStyle.Bottom)
            Assert.True(first.Y > second.Y && second.Y > third.Y);
        else if (dock == DockStyle.Left)
            Assert.True(first.X < second.X && second.X < third.X);
        else
            Assert.True(first.X > second.X && second.X > third.X);
    }

    [Theory]
    [MemberData(nameof(MixedDockCases))]
    public void MixedDockingMatchesPreviewAndRuntime(DockStyle[] docks)
    {
        var document = CreateDocument(docks);

        AssertPreviewAndRuntimeMatch(document);
    }

    [Fact]
    public void DockedControlLocationIsIgnoredButAuthoredThicknessIsPreserved()
    {
        var document = CreateDocument([DockStyle.Top, DockStyle.Fill]);
        document.Controls[0].Bounds = new DesignBounds(777, 888, 321, 120);
        document.Controls[1].Bounds = new DesignBounds(-200, -300, 444, 555);

        AssertPreviewAndRuntimeMatch(document);
        Assert.Equal(new DesignBounds(0, 0, 600, 120), PreviewBounds(document, "panel1"));
        Assert.Equal(new DesignBounds(0, 120, 600, 480), PreviewBounds(document, "panel2"));
    }

    [Fact]
    public void NestedContainerUsesTheSameFrontToBackOrder()
    {
        var document = CreateDocument([DockStyle.Fill]);
        var container = document.Controls[0];
        container.Name = "container";
        container.Children.Clear();
        container.Children.Add(CreatePanel("nested1", DockStyle.Top, 120));
        container.Children.Add(CreatePanel("nested2", DockStyle.Top, 120));
        container.Children.Add(CreatePanel("nested3", DockStyle.Top, 120));

        AssertPreviewAndRuntimeMatch(document);
        Assert.Equal(new DesignBounds(0, 0, 600, 120), PreviewBounds(document, "nested1"));
        Assert.Equal(new DesignBounds(0, 120, 600, 120), PreviewBounds(document, "nested2"));
        Assert.Equal(new DesignBounds(0, 240, 600, 120), PreviewBounds(document, "nested3"));
    }

    [Fact]
    public void OverlappingControlsTreatTheFirstDocumentChildAsFrontMost()
    {
        var document = CreateDocument([DockStyle.None, DockStyle.None, DockStyle.None]);
        foreach (var child in document.Controls)
            child.Bounds = new DesignBounds(20, 20, 200, 100);

        var hit = new DesignerHost(document).HitTest(30, 30);

        Assert.Same(document.Controls[0], hit.Node);
    }

    [Fact]
    public void SaveReloadGenerateAndReverseSyncPreserveChildOrder()
    {
        var original = CreateThreePanelDocument(DockStyle.Top);
        var serializer = DesignDocumentSerializer.Default;
        var reloaded = serializer.Deserialize(serializer.Serialize(original));
        var generator = new CSharpDesignerGenerator();
        var firstGeneration = generator.Generate(reloaded);
        var secondGeneration = generator.Generate(reloaded);

        Assert.True(firstGeneration.Succeeded, string.Join(Environment.NewLine, firstGeneration.Validation.Errors));
        Assert.Equal(firstGeneration.Code, secondGeneration.Code);
        Assert.Equal(["panel1", "panel2", "panel21"], reloaded.Controls.Select(node => node.Name));
        AssertInOrder(
            firstGeneration.Code,
            "this.Controls.Add(this.panel21);",
            "this.Controls.Add(this.panel2);",
            "this.Controls.Add(this.panel1);");

        var parsed = new CSharpDesignerParser().Parse(firstGeneration.Code);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var reversed = Assert.IsType<DesignDocument>(parsed.Document);
        Assert.Equal(["panel1", "panel2", "panel21"], reversed.Controls.Select(node => node.Name));
        Assert.Equal(["label1"], reversed.Controls[0].Children.Select(node => node.Name));
        Assert.Equal(["label2"], reversed.Controls[1].Children.Select(node => node.Name));
        Assert.Equal(["label3"], reversed.Controls[2].Children.Select(node => node.Name));
    }

    [Fact]
    public void OutlineMoveUpAndDownChangesPreviewAndGeneratedRuntimeOrder()
    {
        var document = CreateThreePanelDocument(DockStyle.Top);
        var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(document.Controls[1]);

        Assert.True(session.MoveSelectedNodeUp());
        Assert.Equal(["panel2", "panel1", "panel21"], document.Controls.Select(node => node.Name));
        Assert.Equal(new DesignBounds(0, 0, 600, 120), PreviewBounds(document, "panel2"));
        AssertGeneratedRootOrder(document, "panel21", "panel1", "panel2");

        Assert.True(session.MoveSelectedNodeDown());
        Assert.Equal(["panel1", "panel2", "panel21"], document.Controls.Select(node => node.Name));
        Assert.Equal(new DesignBounds(0, 0, 600, 120), PreviewBounds(document, "panel1"));
        AssertGeneratedRootOrder(document, "panel21", "panel2", "panel1");
    }

    [Fact]
    public void FlowAndTableContainersKeepTheirAuthoredChildSequence()
    {
        foreach (var typeName in new[] { "FlowLayoutPanel", "TableLayoutPanel" })
        {
            var document = CreateDocument([DockStyle.Fill]);
            var container = document.Controls[0];
            container.TypeName = typeName;
            container.Children.Clear();
            container.Children.Add(CreatePanel("first", DockStyle.None, 40));
            container.Children.Add(CreatePanel("second", DockStyle.None, 40));
            container.Children.Add(CreatePanel("third", DockStyle.None, 40));

            var generated = new CSharpDesignerGenerator().Generate(document);
            Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
            AssertInOrder(
                generated.Code,
                "this.panel1.Controls.Add(this.first);",
                "this.panel1.Controls.Add(this.second);",
                "this.panel1.Controls.Add(this.third);");
            var parsed = new CSharpDesignerParser().Parse(generated.Code);
            Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var parsedContainer = Assert.Single(Assert.IsType<DesignDocument>(parsed.Document).Controls);

            Assert.Equal(["first", "second", "third"], parsedContainer.Children.Select(node => node.Name));
            Assert.Same(container.Children[2], new DesignerHost(document).HitTest(55, 65).Node);
        }
    }

    [Fact]
    public void RuntimeBringToFrontAndSendToBackUseLastCollectionIndexAsFront()
    {
        using var root = new Panel();
        var first = new Panel { Name = "first" };
        var second = new Panel { Name = "second" };
        var third = new Panel { Name = "third" };
        root.Controls.AddRange(first, second, third);

        first.BringToFront();
        Assert.Equal(["second", "third", "first"], root.Controls.Select(control => control.Name));

        first.SendToBack();
        Assert.Equal(["first", "second", "third"], root.Controls.Select(control => control.Name));
    }

    [Fact]
    public void FrontAndBackDocumentReorderingSurvivesSerializationAndReverseSync()
    {
        var document = CreateDocument([DockStyle.None, DockStyle.None, DockStyle.None]);
        var front = document.Controls[0];
        document.Controls.RemoveAt(0);
        document.Controls.Add(front);
        AssertRoundTripOrder(document, "panel2", "panel21", "panel1");

        document.Controls.RemoveAt(document.Controls.Count - 1);
        document.Controls.Insert(0, front);
        AssertRoundTripOrder(document, "panel1", "panel2", "panel21");
    }

    public static TheoryData<DockStyle[]> MixedDockCases => new()
    {
        new[] { DockStyle.Top, DockStyle.Fill },
        new[] { DockStyle.Top, DockStyle.Bottom, DockStyle.Fill }
    };

    private static DesignDocument CreateThreePanelDocument(DockStyle dock)
        => CreateDocument([dock, dock, dock]);

    private static DesignDocument CreateDocument(IReadOnlyList<DockStyle> docks)
    {
        var document = new DesignDocument
        {
            Namespace = "DockOrderReproduction",
            ClassName = "MainForm",
            FormName = "MainForm",
            Size = new DesignSize(600, 600)
        };
        var names = new[] { "panel1", "panel2", "panel21" };

        for (var index = 0; index < docks.Count; index++)
        {
            var panel = CreatePanel(names[index], docks[index], 120);
            panel.Bounds = new DesignBounds(50 + (index * 20), 60 + (index * 20), 120, 120);
            panel.Children.Add(new DesignControlNode
            {
                TypeName = "Label",
                Name = $"label{index + 1}",
                Bounds = new DesignBounds(19, 23, 75, 23),
                Properties =
                {
                    ["Dock"] = DesignPropertyValue.FromObject(DockStyle.Fill),
                    ["Text"] = DesignPropertyValue.FromString($"TestPanel{index + 1}")
                }
            });
            document.Controls.Add(panel);
        }

        return document;
    }

    private static DesignControlNode CreatePanel(string name, DockStyle dock, int thickness)
        => new()
        {
            TypeName = "Panel",
            Name = name,
            Bounds = new DesignBounds(0, 0, thickness, thickness),
            Properties =
            {
                ["Dock"] = DesignPropertyValue.FromObject(dock)
            }
        };

    private static void AssertPreviewAndRuntimeMatch(DesignDocument document)
    {
        var preview = new DesignerLayoutEngine().Layout(document);
        using var root = BuildRuntimeTree(document, out var runtimeControls);

        foreach (var node in Enumerate(document.Controls))
        {
            var expected = preview.GetEffectiveBounds(node);
            var actual = GetAbsoluteBounds(runtimeControls[node.Name]);
            Assert.Equal(new Rectangle(expected.X, expected.Y, expected.Width, expected.Height), actual);
        }
    }

    private static Panel BuildRuntimeTree(
        DesignDocument document,
        out Dictionary<string, Control> controls)
    {
        controls = new Dictionary<string, Control>(StringComparer.Ordinal);
        var root = new Panel { Size = new Size(document.Size.Width, document.Size.Height) };
        root.SuspendLayout();
        for (var index = document.Controls.Count - 1; index >= 0; index--)
        {
            var node = document.Controls[index];
            root.Controls.Add(CreateRuntimeControl(node, controls));
        }
        root.ResumeLayout(true);
        PerformLayoutRecursively(root);
        return root;
    }

    private static Control CreateRuntimeControl(
        DesignControlNode node,
        IDictionary<string, Control> controls)
    {
        Control control = node.TypeName switch
        {
            "Label" => new Label(),
            "FlowLayoutPanel" => new FlowLayoutPanel(),
            "TableLayoutPanel" => new TableLayoutPanel(),
            _ => new Panel()
        };
        control.Name = node.Name;
        control.Bounds = new Rectangle(node.Bounds.X, node.Bounds.Y, node.Bounds.Width, node.Bounds.Height);
        control.Dock = GetDock(node);
        controls.Add(node.Name, control);
        control.SuspendLayout();
        if (PreservesSequentialChildOrder(node))
        {
            foreach (var child in node.Children)
                control.Controls.Add(CreateRuntimeControl(child, controls));
        }
        else
        {
            for (var index = node.Children.Count - 1; index >= 0; index--)
                control.Controls.Add(CreateRuntimeControl(node.Children[index], controls));
        }
        control.ResumeLayout(true);
        return control;
    }

    private static bool PreservesSequentialChildOrder(DesignControlNode node)
        => node.TypeName is "FlowLayoutPanel" or "TableLayoutPanel" or "TabControl";

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

    private static DockStyle GetDock(DesignControlNode node)
        => node.Properties.TryGetValue("Dock", out var value)
            && Enum.TryParse(value.GetString(), out DockStyle dock)
                ? dock
                : DockStyle.None;

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

    private static void AssertGeneratedRootOrder(DesignDocument document, params string[] names)
    {
        var result = new CSharpDesignerGenerator().Generate(document);
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Validation.Errors));
        AssertInOrder(result.Code, names.Select(name => $"this.Controls.Add(this.{name});").ToArray());
    }

    private static void AssertRoundTripOrder(DesignDocument document, params string[] names)
    {
        var serializer = DesignDocumentSerializer.Default;
        var reloaded = serializer.Deserialize(serializer.Serialize(document));
        Assert.Equal(names, reloaded.Controls.Select(node => node.Name));

        var generated = new CSharpDesignerGenerator().Generate(reloaded);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        var parsed = new CSharpDesignerParser().Parse(generated.Code);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(names, Assert.IsType<DesignDocument>(parsed.Document).Controls.Select(node => node.Name));
    }

    private static void AssertInOrder(string text, params string[] values)
    {
        var previous = -1;
        foreach (var value in values)
        {
            var current = text.IndexOf(value, StringComparison.Ordinal);
            Assert.True(current > previous, $"'{value}' was not emitted after the previous child add.");
            previous = current;
        }
    }
}
