using ModernFormsNext.Animations;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class AnimationOptInDefaultsTests
{
    [Fact]
    public void NewControlsHaveIndependentEmptyEffectAndTransitionCollections()
    {
        Control[] controls = [new Control(), new Button(), new TextBox(), new Switch()];
        try
        {
            foreach (Control control in controls)
            {
                Assert.Empty(control.InteractionEffects);
                Assert.Equal(0, control.StyleTransitions.Count);
                Assert.Null(control.Ripple);
                Assert.Null(control.PressEffect);
            }

            Assert.All(
                controls.Skip(1),
                control => Assert.NotSame(controls[0].InteractionEffects, control.InteractionEffects));
            Assert.All(
                controls.Skip(1),
                control => Assert.NotSame(controls[0].StyleTransitions, control.StyleTransitions));
        }
        finally
        {
            foreach (Control control in controls)
                control.Dispose();
        }
    }

    [Fact]
    public void ExplicitConfigurationOnOneControlDoesNotAffectAnother()
    {
        using var first = new Button();
        using var second = new Button();

        first.InteractionEffects.Add(new RippleEffect());
        first.StyleTransitions.Add(
            VisualState.Normal,
            VisualState.Hover,
            new VisualStateTransition());

        Assert.Single(first.InteractionEffects);
        Assert.Equal(1, first.StyleTransitions.Count);
        Assert.Empty(second.InteractionEffects);
        Assert.Equal(0, second.StyleTransitions.Count);
        Assert.Null(second.Ripple);
        Assert.Null(second.PressEffect);
    }

    [Fact]
    public void DefaultVisualStatesApplyImmediatelyWithoutSchedulerTicksOrLayout()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = new TestButton { AnimationSchedulerOverride = harness.Scheduler };
        control.Style.BackgroundColor = SKColors.Black;
        control.StyleHover.BackgroundColor = SKColors.Blue;
        control.StylePressed.BackgroundColor = SKColors.Red;
        control.StyleFocused.BackgroundColor = SKColors.Green;
        control.StyleDisabled.BackgroundColor = SKColors.Gray;
        int layouts = 0;
        control.Layout += (_, _) => layouts++;

        control.EnterForTest();
        AssertState(control, VisualState.Hover, control.StyleHover, SKColors.Blue);
        control.DownForTest();
        AssertState(control, VisualState.Pressed, control.StylePressed, SKColors.Red);
        control.UpForTest();
        AssertState(control, VisualState.Hover, control.StyleHover, SKColors.Blue);
        control.LeaveForTest();
        AssertState(control, VisualState.Normal, control.Style, SKColors.Black);
        control.FocusForTest();
        AssertState(control, VisualState.Focused, control.StyleFocused, SKColors.Green);
        control.Enabled = false;
        AssertState(control, VisualState.Disabled, control.StyleDisabled, SKColors.Gray);

        Assert.Empty(control.InteractionEffects);
        Assert.Empty(control.StyleTransitions);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, layouts);
    }

    private static void AssertState(
        TestButton control,
        VisualState state,
        ControlStyle style,
        SKColor background)
    {
        Assert.Equal(state, control.VisualState);
        Assert.Same(style, control.CurrentStyle);
        Assert.Equal(background, control.CurrentStyle.GetBackgroundColor());
    }

    private sealed class TestButton : Button
    {
        public void EnterForTest()
            => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, System.Drawing.Point.Empty));

        public void LeaveForTest()
            => OnMouseLeave(EventArgs.Empty);

        public void DownForTest()
            => RaiseMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, System.Drawing.Point.Empty));

        public void UpForTest()
            => RaiseMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, System.Drawing.Point.Empty));

        public void FocusForTest() => OnGotFocus(EventArgs.Empty);
    }
}
