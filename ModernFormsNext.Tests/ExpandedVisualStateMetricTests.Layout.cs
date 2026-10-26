using System.Drawing;
using System.Reflection;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed partial class ExpandedVisualStateMetricTests
{
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ButtonTextImageBoundsFollowPresentationShapingAndKeepGlyphsVisible(double progress)
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var button = new Button { Size = new Size(300, 112), Text = "Shared typography",
            ForeColor = SkiaSharp.SKColors.Black, AnimationSchedulerOverride = harness.Scheduler };
        button.Style.FontSize = 12;
        button.Style.LineHeight = 1;
        button.Style.LetterSpacing = 0;
        button.StyleDisabled.FontSize = 20;
        button.StyleDisabled.LineHeight = 1.5;
        button.StyleDisabled.LetterSpacing = 0.75;
        AddTransition(button, VisualState.Normal, VisualState.Disabled);
        button.Enabled = false;
        harness.AdvanceAndTick(Duration * progress);

        var layout = ModernFormsNext.Layout.TextImageLayoutEngine.Layout(button);
        var measured = TextMeasurer.MeasureText(button.Text, button, new Size(1000, 1000));
        Assert.Equal((int)measured.Width, layout.TextBounds.Width);
        Assert.Equal((int)measured.Height + 1, layout.TextBounds.Height);
        using var bitmap = new SkiaSharp.SKBitmap(button.Width, button.Height);
        bitmap.Erase(SkiaSharp.SKColors.Transparent);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
            canvas.DrawText(button.Text, layout.TextBounds, button, button.TextAlign, maxLines: 1);
        Assert.Contains(bitmap.Bytes, value => value != 0);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void PresentationTextMetricsAreScaledOnceAndMatchSharedShaping(double scale)
    {
        using var harness = new AnimationSchedulerTestHarness();
        var implementation = DispatchProxy.Create<IWindowBaseImpl, MetricDpiProxy>();
        ((MetricDpiProxy)(object)implementation).Scale = scale;
        using var window = new MetricDpiWindow(implementation);
        using var control = new MetricTextBox { Width = 400, Text = "Spacing", AnimationSchedulerOverride = harness.Scheduler };
        control.Style.FontSize = 12;
        control.Style.LineHeight = 1;
        control.Style.LetterSpacing = 0;
        control.StyleHover.FontSize = 20;
        control.StyleHover.LineHeight = 2;
        control.StyleHover.LetterSpacing = 2;
        AddTransition(control, VisualState.Normal, VisualState.Hover);
        window.Controls.Add(control);
        Assert.Equal(scale, control.Scaling);

        control.Enter();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(16, control.CurrentStyle.GetFontSize());
        Assert.Equal(control.LogicalToDeviceUnits(16), control.CurrentFontSize);
        var expected = TextMeasurer.CreateTextBlock(control.Text, control.CurrentStyle.GetFont(),
            control.LogicalToDeviceUnits(16), new Size(control.PaddedClientRectangle.Width, int.MaxValue),
            maxLines: 1, fontStyle: control.CurrentStyle.GetFontStyle(), lineHeight: 1.5, letterSpacing: scale);
        var actual = control.document.GetTextBlock();
        Assert.Equal(expected.MeasuredWidth, actual.MeasuredWidth);
        Assert.Equal(expected.MeasuredHeight, actual.MeasuredHeight);
        window.adapter.Dispose();
    }

    [Fact]
    public void NestedAutoSizeCaptionMovesItsSiblingWithoutStartingBoundsAnimations()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(400, 200) };
        using var flow = new FlowLayoutPanel { Bounds = new Rectangle(10, 10, 300, 150), WrapContents = false };
        // GroupBox has a production caption-based preferred-size implementation. Label's
        // existing AutoSize contract does not measure its text and is outside this planner change.
        using var text = new MetricGroupBox { Text = "Growing", AutoSize = true, Margin = Padding.Empty,
            AnimationSchedulerOverride = harness.Scheduler };
        text.Style.FontSize = 12;
        text.Style.LineHeight = 1;
        text.Style.LetterSpacing = 0;
        text.StyleHover.FontSize = 20;
        text.StyleHover.LineHeight = 1.5;
        text.StyleHover.LetterSpacing = 1;
        AddTransition(text, VisualState.Normal, VisualState.Hover);
        using var sibling = new Panel { Size = new Size(40, 30), Margin = Padding.Empty,
            AnimationSchedulerOverride = harness.Scheduler };
        flow.Controls.AddRange(text, sibling);
        root.Controls.Add(flow);
        using var surface = new SkiaControlSurface(root);
        flow.PerformLayout();
        Size initial = text.Size;
        Assert.Equal(text.Right, sibling.Left);
        root.LayoutTransition = new LayoutTransition { ApplyToDescendants = true, Duration = Duration, Easing = Easings.Linear };

        text.Enter();
        harness.AdvanceAndTick(Duration / 2);
        Size middle = text.Size;
        Assert.True(middle.Width > initial.Width);
        Assert.True(middle.Height > initial.Height);
        Assert.Equal(text.Right, sibling.Left);
        Assert.False(text.HasActiveLayoutTransition);
        Assert.False(sibling.HasActiveLayoutTransition);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        harness.AdvanceAndTick(Duration / 2);
        Assert.True(text.Width > middle.Width);
        Assert.True(text.Height > middle.Height);
        Assert.Equal(text.Right, sibling.Left);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void ExplicitCaptionFontSizeRemainsAuthoritativeDuringStateFontFrames()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = new MetricGroupBox { Text = "Caption override", CaptionFontSize = 14,
            AnimationSchedulerOverride = harness.Scheduler };
        control.Style.FontSize = 12;
        control.StyleHover.FontSize = 20;
        AddTransition(control, VisualState.Normal, VisualState.Hover);
        int height = control.CaptionHeight;
        control.Enter();
        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(16, control.CurrentStyle.GetFontSize());
        Assert.Equal(14, control.EffectiveCaptionFontSize);
        Assert.Equal(height, control.CaptionHeight);
    }

    private sealed class MetricGroupBox : GroupBox
    {
        public MetricGroupBox() => SetControlBehavior(ControlBehaviors.Hoverable);
        public void Enter() => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));
    }

    private sealed class MetricDpiWindow(IWindowBaseImpl implementation) : WindowBase(implementation);

    public class MetricDpiProxy : DispatchProxy
    {
        public double Scale { get; set; } = 1;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name is "get_RenderScaling" or "get_DesktopScaling") return Scale;
            if (targetMethod?.Name == "PointToScreen" && args?[0] is WindowKit.Point point)
                return new WindowKit.PixelPoint((int)Math.Round(point.X * Scale), (int)Math.Round(point.Y * Scale));
            Type result = targetMethod?.ReturnType ?? typeof(void);
            return result == typeof(void) || !result.IsValueType ? null : Activator.CreateInstance(result);
        }
    }
}
