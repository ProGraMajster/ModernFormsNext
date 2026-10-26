using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed partial class DesignerRuntimeLayoutParityTests
{
    [Theory]
    [InlineData(nameof(Control.Style))]
    [InlineData(nameof(Control.StyleHover))]
    [InlineData(nameof(Control.StylePressed))]
    [InlineData(nameof(Control.StyleFocused))]
    [InlineData(nameof(Control.StyleDisabled))]
    public void StateTypographyEditsUseKnownMetadataAndRoundTrip(string state)
    {
        var document = BasicDocument();
        var node = Node("caption", nameof(Label), 20, 20, 180, 80);
        document.Controls.Add(node);
        using var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        CommitStateMetric74(grid, state + ".FontSize", "20");
        CommitStateMetric74(grid, state + ".LineHeight", "1.5");
        CommitStateMetric74(grid, state + ".LetterSpacing", "0.75");
        CommitStateMetric74(grid, state + ".Border.Radius", "12");

        var restored = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));
        var generated = new CSharpDesignerGenerator().Generate(restored);
        Assert.True(generated.Succeeded);
        var parsed = new CSharpDesignerParser().Parse(generated.Code);
        Assert.True(parsed.Success);
        using var runtime = CompileAndCreateGeneratedRoot(parsed.Document!);
        var children = runtime.Instance switch
        {
            Form form => form.Controls,
            UserControl control => control.Controls,
            _ => throw new InvalidOperationException("Expected a known generated root.")
        };
        var caption = Assert.IsType<Label>(Assert.Single(children));
        var style = state switch
        {
            nameof(Control.Style) => caption.Style,
            nameof(Control.StyleHover) => caption.StyleHover,
            nameof(Control.StylePressed) => caption.StylePressed,
            nameof(Control.StyleFocused) => caption.StyleFocused,
            nameof(Control.StyleDisabled) => caption.StyleDisabled,
            _ => throw new InvalidOperationException("Unknown test state.")
        };
        Assert.Equal(20, style.FontSize);
        Assert.Equal(1.5d, style.LineHeight);
        Assert.Equal(0.75d, style.LetterSpacing);
        Assert.Equal(12, style.Border.Radius);
    }

    [Theory]
    [InlineData("LineHeight", "0")]
    [InlineData("LineHeight", "17")]
    [InlineData("LineHeight", "NaN")]
    [InlineData("LineHeight", "Infinity")]
    [InlineData("LetterSpacing", "1025")]
    [InlineData("LetterSpacing", "-1025")]
    [InlineData("LetterSpacing", "not-a-number")]
    public void InvalidStateTypographyEditDoesNotPublishDataOrUndo(string member, string text)
    {
        var document = BasicDocument();
        var node = Node("caption", nameof(Label), 20, 20, 180, 80);
        document.Controls.Add(node);
        using var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        string path = nameof(Control.StyleHover) + "." + member;
        var property = Assert.Single(StateMetricProperties74(grid.Properties), item => item.Path == path);
        grid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.False(grid.CommitSelectedValue(text));
        Assert.False(node.Properties.ContainsKey(path));
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void StateTypographyNullPreservesInheritanceAndUndo()
    {
        var document = BasicDocument();
        var node = Node("caption", nameof(Label), 20, 20, 180, 80);
        document.Controls.Add(node);
        using var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        CommitStateMetric74(grid, "StyleHover.LineHeight", "1.5");
        CommitStateMetric74(grid, "StyleHover.LineHeight", "null");
        Assert.Equal(DesignPropertyValueKind.Null, node.Properties["StyleHover.LineHeight"].Kind);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(1.5d, node.Properties["StyleHover.LineHeight"].Value);
    }

    [Theory]
    [InlineData("LineHeight", "Boolean")]
    [InlineData("LineHeight", "String")]
    [InlineData("LineHeight", "NaN")]
    [InlineData("LineHeight", "Infinity")]
    [InlineData("LetterSpacing", "Boolean")]
    [InlineData("LetterSpacing", "String")]
    [InlineData("LetterSpacing", "NaN")]
    [InlineData("LetterSpacing", "Infinity")]
    public void StateTypographyAdmissionRejectsWrongKindsAndNonFiniteScalars(string member, string shape)
    {
        var value = shape switch
        {
            "Boolean" => DesignPropertyValue.FromBoolean(true),
            "String" => DesignPropertyValue.FromString("1.5"),
            "NaN" => DesignPropertyValue.FromDouble(double.NaN),
            "Infinity" => DesignPropertyValue.FromDouble(double.PositiveInfinity),
            _ => throw new InvalidOperationException("Unknown safe test representation.")
        };
        Assert.False(DesignerTypographyPropertyValidator.TryValidateMetricValue(member, value, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void StaticStateMetricPreviewMatchesRuntimeWithoutSchedulingMotion()
    {
        var document = BasicDocument();
        var node = Node("caption", nameof(Label), 20, 20, 180, 80);
        node.Properties["Text"] = DesignPropertyValue.FromString("Typography preview");
        node.Properties["Style.FontSize"] = DesignPropertyValue.FromInt32(12);
        node.Properties["Style.LineHeight"] = DesignPropertyValue.FromDouble(1);
        node.Properties["Style.LetterSpacing"] = DesignPropertyValue.FromDouble(0);
        node.Properties["StyleHover.FontSize"] = DesignPropertyValue.FromInt32(20);
        node.Properties["StyleHover.LineHeight"] = DesignPropertyValue.FromDouble(1.5);
        node.Properties["StyleHover.LetterSpacing"] = DesignPropertyValue.FromDouble(0.75);
        node.Properties["StyleHover.Border.Radius"] = DesignPropertyValue.FromInt32(14);
        node.Properties[VisualStateTransitionDesignValue.PropertyName] = VisualStateTransitionDesignValue.Create(
            [new DesignVisualStateTransition("Normal", "Hover", 1000, "Linear")]);
        document.Controls.Add(node);
        int active = ModernFormsNext.Animations.AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount;
        DesignerRuntimeLayoutParityHarness.AssertParity("state-metric-static", document, document);
        Assert.Equal(active, ModernFormsNext.Animations.AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    private static IEnumerable<DesignerPropertyDescriptor> StateMetricProperties74(IEnumerable<DesignerPropertyDescriptor> properties)
    {
        foreach (var property in properties)
        {
            yield return property;
            foreach (var child in StateMetricProperties74(property.Children)) yield return child;
        }
    }

    private static void CommitStateMetric74(DesignerPropertyGridState grid, string path, string text)
    {
        var property = Assert.Single(StateMetricProperties74(grid.Properties), item => item.Path == path);
        grid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.True(grid.CommitSelectedValue(text));
    }
}
