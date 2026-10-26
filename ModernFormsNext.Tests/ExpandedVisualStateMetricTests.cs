using System.Drawing;
using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed partial class ExpandedVisualStateMetricTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(100);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompatibleTextMetricsUseOneProgressWithoutMutatingPersistentValues(bool textEffects)
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        if (textEffects)
        {
            control.Style.TextFont = new Font(Theme.UIFont.FamilyName, 12, FontStyle.Underline | FontStyle.Strikeout);
            control.StyleHover.TextFont = new Font(Theme.UIFont.FamilyName, 20, FontStyle.Underline | FontStyle.Strikeout);
        }
        control.StyleHover.FontSize = 20;
        control.StyleHover.LineHeight = 2;
        control.StyleHover.LetterSpacing = 2;

        control.Enter();
        Assert.Equal(12, control.CurrentStyle.GetFontSize());
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(16, control.CurrentStyle.GetFontSize());
        Assert.Equal(1.5d, control.CurrentStyle.GetLineHeight());
        Assert.Equal(1d, control.CurrentStyle.GetLetterSpacing());
        Assert.Equal(textEffects ? FontStyle.Underline | FontStyle.Strikeout : FontStyle.Regular,
            control.CurrentStyle.GetFontStyle());
        Assert.Equal(12, control.Font.Size);
        Assert.Equal(1d, control.LineHeight);
        Assert.Equal(0d, control.LetterSpacing);
        Assert.Equal(20, control.StyleHover.FontSize);
        Assert.Equal(2d, control.StyleHover.LineHeight);
        Assert.Equal(2d, control.StyleHover.LetterSpacing);

        harness.AdvanceAndTick(Duration / 2);
        Assert.Same(control.StyleHover, control.CurrentStyle);
        Assert.Equal(20, control.CurrentStyle.GetFontSize());
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void CornerRadiusInterpolatesWithoutRequestingContentLayout()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.Style.Border.Radius = 6;
        control.StyleHover.Border.Radius = 18;
        int layouts = 0;
        control.Layout += (_, _) => layouts++;

        control.Enter();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(12, control.CurrentStyle.Border.GetRadius());
        Assert.Equal(0, layouts);
        Assert.Equal(0, control.MetricNotifications);
        Assert.Equal(6, control.Style.Border.Radius);
        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(18, control.CurrentStyle.Border.GetRadius());
        Assert.Equal(0, control.MetricNotifications);
    }

    [Fact]
    public void UnchangedIntegerFontSizeDoesNotRemeasureWhileRadiusMoves()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.StyleHover.FontSize = 13;
        control.StyleHover.Border.Radius = 100;

        control.Enter();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(10));
        Assert.Equal(12, control.CurrentStyle.GetFontSize());
        Assert.Equal(10, control.CurrentStyle.Border.GetRadius());
        Assert.Equal(0, control.MetricNotifications);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(40));
        Assert.Equal(13, control.CurrentStyle.GetFontSize());
        Assert.Equal(1, control.MetricNotifications);
    }

    [Fact]
    public void FontSizeFramesInvalidateTheExistingTextBoxCache()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = new MetricTextBox { Width = 400, Text = "Shared font measurement", AnimationSchedulerOverride = harness.Scheduler };
        control.Style.FontSize = 12;
        control.StyleHover.FontSize = 20;
        AddTransition(control, VisualState.Normal, VisualState.Hover);
        var before = control.document.GetTextBlock();

        control.Enter();
        Assert.Same(before, control.document.GetTextBlock());
        harness.AdvanceAndTick(Duration / 2);
        var intermediate = control.document.GetTextBlock();
        Assert.NotSame(before, intermediate);
        Assert.Equal(16, control.CurrentStyle.GetFontSize());
        Assert.True(intermediate.MeasuredWidth > before.MeasuredWidth);
        Assert.Same(intermediate, control.document.GetTextBlock());
        harness.AdvanceAndTick(Duration / 2);
        Assert.True(control.document.GetTextBlock().MeasuredWidth > intermediate.MeasuredWidth);
    }

    [Fact]
    public void IncompatibleFontSwitchesDiscretelyAndInvalidatesCachedTextImmediately()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = new MetricTextBox { Width = 400, Text = "Discrete bold font", AnimationSchedulerOverride = harness.Scheduler };
        control.Style.TextFont = new Font(Theme.UIFont.FamilyName, 12);
        control.StyleHover.TextFont = new Font(Theme.UIFont.FamilyName, 20, FontStyle.Bold);
        AddTransition(control, VisualState.Normal, VisualState.Hover);
        var before = control.document.GetTextBlock();

        control.Enter();

        Assert.Equal(20, control.CurrentStyle.GetFontSize());
        Assert.Equal(FontStyle.Bold, control.CurrentStyle.GetFontStyle());
        Assert.NotSame(before, control.document.GetTextBlock());
        Assert.True(control.document.GetTextBlock().MeasuredWidth > before.MeasuredWidth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4097)]
    [InlineData(int.MaxValue)]
    public void UnsupportedFontSizeUsesOneDiscreteMetricChange(int target)
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.StyleHover.FontSize = target;
        control.Enter();
        Assert.Equal(target, control.CurrentStyle.GetFontSize());
        Assert.Equal(1, control.MetricNotifications);
        for (int i = 0; i < 9; i++) harness.AdvanceAndTick(TimeSpan.FromMilliseconds(10));
        Assert.Equal(1, control.MetricNotifications);
        Assert.Equal(target, control.CurrentStyle.GetFontSize());
    }

    [Theory]
    [InlineData(-1f, 12, 1d, 0d, 2)]
    [InlineData(2f, 20, 2d, 2d, 18)]
    public void NewMetricsClampOvershootWithoutReleasingTheRawTimeline(float eased, int fontSize,
        double lineHeight, double spacing, int radius)
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.Style.Border.Radius = 2;
        control.StyleHover.Border.Radius = 18;
        control.StyleHover.FontSize = 20;
        control.StyleHover.LineHeight = 2;
        control.StyleHover.LetterSpacing = 2;
        control.StyleTransitions.Clear();
        AddTransition(control, VisualState.Normal, VisualState.Hover, easing: _ => eased);

        control.Enter();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(fontSize, control.CurrentStyle.GetFontSize());
        Assert.Equal(lineHeight, control.CurrentStyle.GetLineHeight());
        Assert.Equal(spacing, control.CurrentStyle.GetLetterSpacing());
        Assert.Equal(radius, control.CurrentStyle.Border.GetRadius());
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        harness.AdvanceAndTick(Duration / 2);
        Assert.Same(control.StyleHover, control.CurrentStyle);
        Assert.Equal(20, control.CurrentStyle.GetFontSize());
    }

    [Fact]
    public void RetargetStartsFromPresentedTextAndCornerMetrics()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.Style.Border.Radius = 2;
        control.StyleHover.FontSize = 20;
        control.StyleHover.LineHeight = 2;
        control.StyleHover.LetterSpacing = 2;
        control.StyleHover.Border.Radius = 10;
        control.StylePressed.FontSize = 24;
        control.StylePressed.LineHeight = 3;
        control.StylePressed.LetterSpacing = 4;
        control.StylePressed.Border.Radius = 18;
        AddTransition(control, VisualState.Hover, VisualState.Pressed);

        control.Enter();
        harness.AdvanceAndTick(Duration / 2);
        control.Down();
        Assert.Equal(16, control.CurrentStyle.GetFontSize());
        Assert.Equal(1.5d, control.CurrentStyle.GetLineHeight());
        Assert.Equal(1d, control.CurrentStyle.GetLetterSpacing());
        Assert.Equal(6, control.CurrentStyle.Border.GetRadius());
        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(20, control.CurrentStyle.GetFontSize());
        Assert.Equal(2.25d, control.CurrentStyle.GetLineHeight());
        Assert.Equal(2.5d, control.CurrentStyle.GetLetterSpacing());
        Assert.Equal(12, control.CurrentStyle.Border.GetRadius());
    }

    [Fact]
    public void EasingThatChangesStateCannotPublishOldTransformsOrMetrics()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.StyleHover.FontSize = 20;
        control.StyleHover.Opacity = 0.5f;
        control.StyleDisabled.FontSize = 24;
        control.StyleDisabled.Opacity = 0.25f;
        control.StyleTransitions.Clear();
        AddTransition(control, VisualState.Normal, VisualState.Hover, easing: progress =>
        {
            control.Enabled = false;
            return progress;
        });
        control.Enter();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(VisualState.Disabled, control.VisualState);
        Assert.Same(control.StyleDisabled, control.CurrentStyle);
        Assert.Equal(24, control.CurrentStyle.GetFontSize());
        Assert.Equal(0.25f, control.EffectiveOpacity);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ImmediatePolicyPublishesExactTextAndCornerTargets(int policy)
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.StyleHover.FontSize = 20;
        control.StyleHover.LineHeight = 1.3;
        control.StyleHover.LetterSpacing = -0.35;
        control.StyleHover.Border.Radius = 18;
        if (policy == 0) harness.Policy.AnimationsEnabled = false;
        if (policy == 1) harness.Policy.ReducedMotion = true;
        if (policy == 2) harness.Policy.DurationScale = 0;
        if (policy == 3)
        {
            control.StyleTransitions.Clear();
            AddTransition(control, VisualState.Normal, VisualState.Hover, TimeSpan.Zero);
        }
        control.Enter();
        Assert.Same(control.StyleHover, control.CurrentStyle);
        Assert.Equal(20, control.CurrentStyle.GetFontSize());
        Assert.Equal(1.3, control.CurrentStyle.GetLineHeight());
        Assert.Equal(-0.35, control.CurrentStyle.GetLetterSpacing());
        Assert.Equal(18, control.CurrentStyle.Border.GetRadius());
        Assert.False(harness.TickSource.IsRunning);
    }

    private static MetricPanel CreatePanel(AnimationSchedulerTestHarness harness)
    {
        var control = new MetricPanel { AnimationSchedulerOverride = harness.Scheduler };
        control.Style.FontSize = 12;
        control.Style.LineHeight = 1;
        control.Style.LetterSpacing = 0;
        // State styles have their own default-style chain. Keep non-target metrics equal
        // explicitly so a radius-only scenario does not also animate the default font size.
        control.StyleHover.FontSize = 12;
        control.StyleHover.LineHeight = 1;
        control.StyleHover.LetterSpacing = 0;
        AddTransition(control, VisualState.Normal, VisualState.Hover);
        return control;
    }

    private static void AddTransition(Control control, VisualState from, VisualState to,
        TimeSpan? duration = null, Func<float, float>? easing = null)
        => control.StyleTransitions.Add(from, to, new VisualStateTransition
        { Duration = duration ?? Duration, Easing = easing ?? Easings.Linear });

    private sealed class MetricPanel : Panel
    {
        public MetricPanel() => SetControlBehavior(ControlBehaviors.Hoverable);
        public int MetricNotifications { get; private set; }
        public Action? MetricsChanged { get; set; }
        internal override void OnPresentationContentMetricsChanged()
        {
            MetricNotifications++;
            base.OnPresentationContentMetricsChanged();
            MetricsChanged?.Invoke();
        }
        public void Enter() => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));
        public void Down() => RaiseMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, Point.Empty));
    }

    private sealed class MetricTextBox : TextBox
    {
        public MetricTextBox() => SetControlBehavior(ControlBehaviors.Hoverable);
        public int MetricNotifications { get; private set; }
        internal override void OnPresentationContentMetricsChanged()
        {
            MetricNotifications++;
            base.OnPresentationContentMetricsChanged();
        }
        public void Enter() => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));
        public void Leave() => OnMouseLeave(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));
    }
}
