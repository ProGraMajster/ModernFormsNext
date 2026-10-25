using ModernFormsNext.Animations;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed partial class DesignerRuntimeLayoutParityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LayoutPolicyRoundTripsThroughDocumentCodeAndRuntime(bool scope)
    {
        var document = BasicDocument();
        var node = Node("container", nameof(Panel), 20, 20, 180, 80);
        node.Properties[LayoutTransitionDesignValue.PropertyName] =
            LayoutTransitionDesignValue.Create(true, 210d, "Linear", scope);
        node.Children.Add(Node("child", nameof(Panel), 5, 5, 40, 30));
        document.Controls.Add(node);
        var restored = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));
        var generated = new CSharpDesignerGenerator().Generate(restored);
        Assert.True(generated.Succeeded);
        Assert.Equal(scope, generated.Code.Contains("ApplyToDescendants = true", StringComparison.Ordinal));
        var parsed = new CSharpDesignerParser().Parse(generated.Code);
        Assert.True(parsed.Success);
        var value = Assert.Single(parsed.Document!.Controls).Properties[LayoutTransitionDesignValue.PropertyName];
        Assert.True(LayoutTransitionDesignValue.TryRead(value, out bool enabled, out double duration,
            out string easing, out bool parsedScope, out string? error), error);
        Assert.True(enabled);
        Assert.Equal(210d, duration);
        Assert.Equal("Linear", easing);
        Assert.Equal(scope, parsedScope);
        Assert.Equal(scope, value.ObjectProperties!.ContainsKey("ApplyToDescendants"));
        using var runtime = CompileAndCreateGeneratedRoot(parsed.Document);
        var children = runtime.Instance switch
        {
            Form form => form.Controls,
            UserControl control => control.Controls,
            _ => throw new InvalidOperationException("Generated root must be a Form or UserControl.")
        };
        var container = Assert.IsType<Panel>(Assert.Single(children));
        Assert.Equal(scope, container.LayoutTransition!.ApplyToDescendants);
        Assert.Null(Assert.Single(container.Controls).LayoutTransition);
    }

    [Fact]
    public void LayoutPolicyEditorPreservesScopeOnTimingEditAndResetsToInheritedDefault()
    {
        var model = new DesignerLayoutTransitionEditorModel(LayoutTransitionDesignValue.Create(true, 100, "Linear", true));
        Assert.True(model.ApplyToDescendants);
        Assert.True(model.TrySet(false, 200, "EaseOut", out string? error), error);
        Assert.True(model.TryCreateValue(out var value, out error), error);
        Assert.True(LayoutTransitionDesignValue.TryRead(value, out bool enabled, out _, out _, out bool scope, out error), error);
        Assert.False(enabled);
        Assert.True(scope);
        Assert.True(model.TrySet(true, 100, "Linear", false, out error), error);
        Assert.False(model.ApplyToDescendants);
        model.Reset();
        Assert.False(model.ApplyToDescendants);
        Assert.True(model.TryCreateValue(out value, out error), error);
        Assert.Null(value);
    }

    [Theory]
    [InlineData("Enabled")]
    [InlineData("ApplyToDescendants")]
    [InlineData("DurationMilliseconds")]
    [InlineData("Easing")]
    public void NullLayoutPolicyMembersFailClosed(string member)
    {
        var properties = LayoutTransitionDesignValue.Create(true, 100, "Linear", true).ObjectProperties!
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        properties[member] = null!;
        var invalid = DesignPropertyValue.FromStructuredObject(LayoutTransitionDesignValue.TypeName, properties);
        Assert.False(LayoutTransitionDesignValue.TryRead(invalid, out _, out _, out _, out _, out string? error));
        Assert.NotNull(error);
        var model = new DesignerLayoutTransitionEditorModel(invalid);
        Assert.NotNull(model.LoadError);
        Assert.False(model.TryCreateValue(out _, out _));
    }

    [Fact]
    public void LayoutScopeRequiresBooleanAndUnknownMembersRemainUnsupported()
    {
        foreach (var properties in new[]
        {
            new Dictionary<string, DesignPropertyValue> { ["ApplyToDescendants"] = DesignPropertyValue.FromString("true") },
            new Dictionary<string, DesignPropertyValue> { ["DescendantMode"] = DesignPropertyValue.FromBoolean(true) }
        })
        {
            var invalid = DesignPropertyValue.FromStructuredObject(LayoutTransitionDesignValue.TypeName, properties);
            Assert.False(LayoutTransitionDesignValue.TryRead(invalid, out _, out _, out _, out _, out _));
            var document = BasicDocument();
            document.Properties[LayoutTransitionDesignValue.PropertyName] = invalid;
            var result = new CSharpDesignerGenerator().Generate(document);
            Assert.DoesNotContain(".LayoutTransition =", result.Code, StringComparison.Ordinal);
            Assert.NotEmpty(result.Validation.Warnings);
        }
    }

    [Fact]
    public void StaticDesignerLayoutIgnoresInheritedTransitionTimingAndDoesNotScheduleFrames()
    {
        var document = BasicDocument();
        var node = Node("container", nameof(Panel), 20, 20, 180, 80);
        node.Children.Add(Node("child", nameof(Panel), 0, 0, 40, 30, DockStyle.Fill));
        document.Controls.Add(node);
        var before = DesignerRuntimeLayoutParityHarness.CaptureDesigner("policy", document, document.Size);
        int activeBefore = AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount;
        node.Properties[LayoutTransitionDesignValue.PropertyName] = LayoutTransitionDesignValue.Create(true, 500, "Linear", true);
        var after = DesignerRuntimeLayoutParityHarness.CaptureDesigner("policy", document, document.Size);
        foreach (var pair in before.Nodes)
            Assert.Equal(pair.Value, after.Nodes[pair.Key]);
        Assert.Equal(activeBefore, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void StaticProductionPagePreviewValidatesScopeWithoutInstallingRuntimePolicy()
    {
        var page = Node("page", nameof(ContentPage), 0, 0, 220, 160);
        page.Properties[LayoutTransitionDesignValue.PropertyName] = LayoutTransitionDesignValue.Create(true, 500, "Linear", true);
        page.Children.Add(Node("body", nameof(Panel), 0, 0, 100, 80));
        int before = AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount;
        using var preview = DesignerPagePreview.TryCreate(page, new System.Drawing.Size(220, 160), out string? error);
        Assert.NotNull(preview);
        Assert.Null(error);
        Assert.Null(preview.Root.LayoutTransition);
        Assert.All(preview.Controls.Values, control => Assert.Null(control.LayoutTransition));
        Assert.Equal(before, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }
}
