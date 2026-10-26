using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed partial class ExpandedVisualStateMetricTests
{
    [Fact]
    public void ImmediateFontMetricCallbackCanDisposeBeforeSchedulerRegistration()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.StyleHover.TextFont = new Font(Theme.UIFont.FamilyName, 20, FontStyle.Bold);
        control.StyleHover.FontSize = 20;
        control.MetricsChanged = control.Dispose;
        int layouts = 0;
        control.Layout += (_, _) => layouts++;

        control.Enter();

        Assert.True(control.IsDisposed);
        Assert.Equal(0, layouts);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void ImmediateFontMetricCallbackCanReparentBeforeSchedulerRegistration()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var first = new Panel();
        using var second = new Panel();
        using var control = CreatePanel(harness);
        first.Controls.Add(control);
        control.StyleHover.TextFont = new Font(Theme.UIFont.FamilyName, 20, FontStyle.Bold);
        control.StyleHover.FontSize = 20;
        control.MetricsChanged = () => second.Controls.Add(control);

        control.Enter();

        Assert.Same(second, control.Parent);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
        harness.AdvanceAndTick(Duration);
        Assert.Equal(20, control.CurrentStyle.GetFontSize());
    }

    [Fact]
    public void ImmediateMetricCallbackCannotRegisterAStateThatItReplaced()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.StyleHover.TextFont = new Font(Theme.UIFont.FamilyName, 20, FontStyle.Bold);
        control.StyleHover.FontSize = 20;
        control.StyleDisabled.FontSize = 24;
        control.StyleDisabled.Opacity = 0.25f;
        control.MetricsChanged = () => control.Enabled = false;

        control.Enter();

        Assert.Equal(VisualState.Disabled, control.VisualState);
        Assert.Same(control.StyleDisabled, control.CurrentStyle);
        Assert.Equal(24, control.CurrentStyle.GetFontSize());
        Assert.Equal(0.25f, control.EffectiveOpacity);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void FailingInitialMetricCallbackRetiresThePriorPresentationRun()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.StyleHover.FontSize = 20;
        control.StylePressed.TextFont = new Font(Theme.UIFont.FamilyName, 24, FontStyle.Bold);
        control.StylePressed.FontSize = 24;
        AddTransition(control, VisualState.Hover, VisualState.Pressed);
        control.Enter();
        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        bool failOnce = true;
        control.MetricsChanged = () =>
        {
            if (!failOnce) return;
            failOnce = false;
            throw new InvalidOperationException("metric callback probe");
        };

        var error = Assert.Throws<InvalidOperationException>(control.Down);

        Assert.Equal("metric callback probe", error.Message);
        Assert.Same(control.StylePressed, control.CurrentStyle);
        Assert.Equal(24, control.CurrentStyle.GetFontSize());
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void WarmTextBlockCacheRetainsItsIdentityWithoutAllocatingForFontKeyReads()
    {
        using var control = new MetricTextBox { Width = 300, Text = "Stable borrowed font cache" };
        var block = control.document.GetTextBlock();
        for (int i = 0; i < 1000; i++) _ = control.document.GetTextBlock();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) _ = control.document.GetTextBlock();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(block, control.document.GetTextBlock());
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ActiveMetricSnapshotRetainsItsFontUntilTheTargetStyleIsReleased()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.StyleHover.FontSize = 20;
        control.StyleHover.LineHeight = 2;
        control.Enter();
        control.StyleHover.FontSize = 24;
        control.StyleHover.LineHeight = 3;
        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(16, control.CurrentStyle.GetFontSize());
        Assert.Equal(1.5, control.CurrentStyle.GetLineHeight());
        harness.AdvanceAndTick(Duration / 2);
        Assert.Same(control.StyleHover, control.CurrentStyle);
        Assert.Equal(24, control.CurrentStyle.GetFontSize());
        Assert.Equal(3, control.CurrentStyle.GetLineHeight());
    }
}
