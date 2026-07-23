using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class VisualStateTransitionTests
{
    [Fact]
    public void StatePriorityCoversNormalFocusedHoverPressedAndDisabled()
    {
        var control = new TestButton();

        Assert.Equal(VisualState.Normal, control.VisualState);
        control.FocusForTest();
        Assert.Equal(VisualState.Focused, control.VisualState);
        control.EnterForTest();
        Assert.Equal(VisualState.Hover, control.VisualState);
        control.DownForTest();
        Assert.Equal(VisualState.Pressed, control.VisualState);
        control.Enabled = false;
        Assert.Equal(VisualState.Disabled, control.VisualState);
    }

    [Fact]
    public void TransitionInterpolatesColorsBrushesAndTransformsWithoutLayout()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = CreateTransitionButton(harness);
        control.Style.BackgroundColor = SKColors.Red;
        control.Style.ForegroundColor = SKColors.Black;
        control.Style.Border.Color = SKColors.Green;
        control.Style.BackgroundBrush = new SolidColorBrush(SKColors.Red);
        control.StyleHover.BackgroundColor = SKColors.Blue;
        control.StyleHover.ForegroundColor = SKColors.White;
        control.StyleHover.Border.Color = SKColors.Yellow;
        control.StyleHover.BackgroundBrush = new SolidColorBrush(SKColors.Blue);
        control.StyleHover.Opacity = 0.5f;
        control.StyleHover.ScaleX = 1.2f;
        control.StyleHover.TranslationX = 8f;
        control.StyleHover.Rotation = 10f;
        control.StyleHover.Border.Width = 8;
        int layouts = 0;
        control.Layout += (_, _) => layouts++;

        control.EnterForTest();
        Assert.Equal(8, control.CurrentStyle.Border.GetWidth());
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(new SKColor(128, 0, 128), control.CurrentStyle.GetBackgroundColor());
        Assert.Equal(new SKColor(128, 128, 128), control.CurrentStyle.GetForegroundColor());
        Assert.Equal(new SKColor(128, 192, 0), control.CurrentStyle.Border.GetColor());
        var brush = Assert.IsType<SolidColorBrush>(control.EffectiveBackgroundBrush);
        Assert.Equal(new SKColor(128, 0, 128), brush.Color);
        Assert.Equal(0.75f, control.EffectiveOpacity, 3);
        Assert.Equal(1.1f, control.EffectiveScaleX, 3);
        Assert.Equal(4f, control.EffectiveTranslationX, 3);
        Assert.Equal(5f, control.EffectiveRotation, 3);
        Assert.Equal(0, layouts);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Same(control.StyleHover, control.CurrentStyle);
        Assert.Equal(SKColors.Blue, control.CurrentStyle.GetBackgroundColor());
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void RapidStateChangesUseLatestStateAndCancelStaleTransition()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = CreateTransitionButton(harness);
        control.Style.BackgroundColor = SKColors.Black;
        control.StyleHover.BackgroundColor = SKColors.Blue;
        control.StylePressed.BackgroundColor = SKColors.Red;
        AddTransition(control, VisualState.Hover, VisualState.Pressed);

        control.EnterForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(30));
        control.DownForTest();

        Assert.Equal(VisualState.Pressed, control.VisualState);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Same(control.StylePressed, control.CurrentStyle);
        Assert.Equal(SKColors.Red, control.CurrentStyle.GetBackgroundColor());
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().CanceledCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void ReducedMotionAppliesTargetOnceWithoutStartingTicks()
    {
        using var harness = new AnimationSchedulerTestHarness();
        harness.Policy.ReducedMotion = true;
        var control = CreateTransitionButton(harness);
        control.StyleHover.BackgroundColor = SKColors.CornflowerBlue;

        control.EnterForTest();

        Assert.Same(control.StyleHover, control.CurrentStyle);
        Assert.Equal(SKColors.CornflowerBlue, control.CurrentStyle.GetBackgroundColor());
        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void ThemeRefreshCancelsTransitionAndResolvesCurrentStateAgain()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = CreateTransitionButton(harness);
        control.StyleHover.BackgroundColor = SKColors.Blue;
        control.EnterForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(30));

        control.StyleHover.BackgroundColor = SKColors.Purple;
        control.ThemeChangedForTest();

        Assert.Equal(VisualState.Hover, control.VisualState);
        Assert.Same(control.StyleHover, control.CurrentStyle);
        Assert.Equal(SKColors.Purple, control.CurrentStyle.GetBackgroundColor());
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void FocusedPressedAndDisabledTransitionsRefreshTheirActiveStyles()
    {
        using var harness = new AnimationSchedulerTestHarness();

        var focused = new TestButton { AnimationSchedulerOverride = harness.Scheduler };
        focused.Style.BackgroundColor = SKColors.Black;
        focused.StyleFocused.BackgroundColor = SKColors.Green;
        AddTransition(focused, VisualState.Normal, VisualState.Focused);
        focused.FocusForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(VisualState.Focused, focused.VisualState);
        Assert.Same(focused.StyleFocused, focused.CurrentStyle);
        Assert.Equal(SKColors.Green, focused.CurrentStyle.GetBackgroundColor());

        var pressed = new TestButton { AnimationSchedulerOverride = harness.Scheduler };
        pressed.Style.BackgroundColor = SKColors.Black;
        pressed.StylePressed.BackgroundColor = SKColors.Red;
        AddTransition(pressed, VisualState.Normal, VisualState.Pressed);
        pressed.DownForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(VisualState.Pressed, pressed.VisualState);
        Assert.Same(pressed.StylePressed, pressed.CurrentStyle);
        Assert.Equal(SKColors.Red, pressed.CurrentStyle.GetBackgroundColor());

        var disabled = new TestButton { AnimationSchedulerOverride = harness.Scheduler };
        disabled.Style.BackgroundColor = SKColors.Black;
        disabled.StyleDisabled.BackgroundColor = SKColors.Gray;
        AddTransition(disabled, VisualState.Normal, VisualState.Disabled);
        disabled.Enabled = false;
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(VisualState.Disabled, disabled.VisualState);
        Assert.Same(disabled.StyleDisabled, disabled.CurrentStyle);
        Assert.Equal(SKColors.Gray, disabled.CurrentStyle.GetBackgroundColor());

        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void StateStylesInheritBrushesAndTransformsTheyDoNotOverride()
    {
        var control = new TestButton();
        var background = new SolidColorBrush(SKColors.Orange);
        var foreground = new SolidColorBrush(SKColors.White);
        var border = new SolidColorBrush(SKColors.Black);
        control.Style.BackgroundBrush = background;
        control.Style.ForegroundBrush = foreground;
        control.Style.BorderBrush = border;
        control.Style.Opacity = 0.8f;
        control.Style.ScaleX = 1.1f;
        control.Style.TranslationX = 4f;

        control.FocusForTest();

        Assert.Equal(VisualState.Focused, control.VisualState);
        Assert.Same(background, control.EffectiveBackgroundBrush);
        Assert.Same(foreground, control.EffectiveTextBrush);
        Assert.Same(border, control.EffectiveBorderBrush);
        Assert.Equal(0.8f, control.EffectiveOpacity, 3);
        Assert.Equal(1.1f, control.EffectiveScaleX, 3);
        Assert.Equal(4f, control.EffectiveTranslationX, 3);
    }

    [Fact]
    public void ActiveStateBrushMutationInvalidatesWithoutAnotherPointerEvent()
    {
        using var control = new TestButton();
        using var surface = new SkiaControlSurface(control);
        var hoverBrush = new SolidColorBrush(SKColors.Blue);
        control.StyleHover.BackgroundBrush = hoverBrush;
        control.EnterForTest();

        Assert.Same(hoverBrush, control.EffectiveBackgroundBrush);
        control.ResetInvalidations();
        hoverBrush.Color = SKColors.Purple;

        Assert.Equal(1, control.InvalidationCount);
        Assert.Equal(VisualState.Hover, control.VisualState);
        Assert.Same(control.StyleHover, control.CurrentStyle);
    }

    [Fact]
    public void DisposingControlCancelsActiveStateTransitionBeforeAnotherRepaint()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = CreateTransitionButton(harness);
        control.EnterForTest();
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);

        control.Dispose();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    private static TestButton CreateTransitionButton(AnimationSchedulerTestHarness harness)
    {
        var control = new TestButton { AnimationSchedulerOverride = harness.Scheduler };
        AddTransition(control, VisualState.Normal, VisualState.Hover);
        return control;
    }

    private static void AddTransition(TestButton control, VisualState from, VisualState to)
    {
        control.StyleTransitions.Add(
            from,
            to,
            new VisualStateTransition
            {
                Duration = TimeSpan.FromMilliseconds(100),
                Easing = Easings.Linear
            });
    }

    private sealed class TestButton : Button
    {
        public int InvalidationCount { get; private set; }

        public void ResetInvalidations() => InvalidationCount = 0;

        public void EnterForTest()
            => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, System.Drawing.Point.Empty));

        public void DownForTest()
            => OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, System.Drawing.Point.Empty));

        public void FocusForTest() => OnGotFocus(EventArgs.Empty);

        public void ThemeChangedForTest() => OnThemeChanged(EventArgs.Empty);

        protected override void OnInvalidated(EventArgs<System.Drawing.Rectangle> e)
        {
            InvalidationCount++;
            base.OnInvalidated(e);
        }
    }
}
