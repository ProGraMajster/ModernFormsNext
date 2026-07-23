using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class InteractionEffectTests
{
    [Fact]
    public void RippleStartsAtPointerAndRendersRadiusFadeAndRoundedClip()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = new TestButton
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(100, 50)
        };
        control.Style.Border.Radius = 18;
        var ripple = new RippleEffect
        {
            Color = Color.FromArgb(100, 20, 40, 60),
            Duration = TimeSpan.FromMilliseconds(100),
            Easing = Easings.Linear
        };
        control.Ripple = ripple;

        control.DownForTest(Mouse(10, 10, pointerId: 7, PointerDeviceKind.Touch));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        using SKBitmap bitmap = RenderEffect(control, InteractionEffectLayer.AboveBackgroundBelowContent);

        Assert.Equal(1, ripple.ActiveRippleCount);
        SKColor center = bitmap.GetPixel(10, 10);
        Assert.Equal(50, center.Alpha);
        Assert.InRange(center.Red, (byte)14, (byte)20);
        Assert.InRange(center.Green, (byte)34, (byte)40);
        Assert.InRange(center.Blue, (byte)54, (byte)60);
        Assert.Equal(0, bitmap.GetPixel(99, 49).Alpha);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);

        control.Size = new Size(160, 80);
        using SKBitmap resized = RenderEffect(control, InteractionEffectLayer.AboveBackgroundBelowContent);
        Assert.NotEqual(0, resized.GetPixel(70, 10).Alpha);
    }

    [Fact]
    public void CenterRippleUsesKeyboardActivationAndReducedMotionSkipsDecoration()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = new TestButton
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(80, 40)
        };
        var ripple = new RippleEffect
        {
            StartFromPointer = false,
            RadiusMode = RippleRadiusMode.Fixed,
            FixedRadius = 20,
            Duration = TimeSpan.FromMilliseconds(100),
            Easing = Easings.Linear
        };
        control.Ripple = ripple;

        control.KeyUpForTest(Keys.Space);
        Assert.Equal(1, ripple.ActiveRippleCount);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        using SKBitmap bitmap = RenderEffect(control, InteractionEffectLayer.AboveBackgroundBelowContent);
        Assert.NotEqual(0, bitmap.GetPixel(40, 20).Alpha);
        Assert.Equal(0, bitmap.GetPixel(10, 10).Alpha);

        ripple.CancelForTest();
        ripple.Enabled = true;
        harness.Policy.ReducedMotion = true;
        control.KeyUpForTest(Keys.Enter);
        Assert.Equal(0, ripple.ActiveRippleCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void RapidRippleHonorsBoundAndEvictsOldestHandle()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = new TestButton
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(100, 40)
        };
        var ripple = new RippleEffect
        {
            Duration = TimeSpan.FromMilliseconds(100),
            MaxConcurrentRipples = 4
        };
        control.Ripple = ripple;

        for (int index = 0; index < 5; index++)
            control.DownForTest(Mouse(10 + index, 10, index, PointerDeviceKind.Touch));

        Assert.Equal(4, ripple.ActiveRippleCount);
        Assert.Equal(4, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().CanceledCount);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, ripple.ActiveRippleCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void RippleRejectsUndefinedRadiusAndEvictionPolicies()
    {
        var ripple = new RippleEffect();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ripple.RadiusMode = (RippleRadiusMode)int.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ripple.EvictionPolicy = (RippleEvictionPolicy)int.MaxValue);
    }

    [Fact]
    public void PressScaleComposesWithControlScaleAndReleasesWithoutSticking()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = new TestButton
        {
            AnimationSchedulerOverride = harness.Scheduler,
            ScaleX = 2f,
            ScaleY = 2f
        };
        var press = new PressScaleEffect
        {
            PressedScale = 0.9f,
            PressDuration = TimeSpan.FromMilliseconds(100),
            ReleaseDuration = TimeSpan.FromMilliseconds(100),
            Easing = Easings.Linear
        };
        control.PressEffect = press;

        MouseEventArgs pointer = Mouse(5, 5, pointerId: 3, PointerDeviceKind.Touch);
        control.DownForTest(pointer);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(1.9f, control.EffectiveScaleX, 3);

        control.UpForTest(pointer);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(2f, control.EffectiveScaleX, 3);
        Assert.False(harness.TickSource.IsRunning);

        control.DownForTest(pointer);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        control.Enabled = false;
        Assert.Equal(2f, control.EffectiveScaleX, 3);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void TouchPointersRemainIndependentAndCancelClearsEffectState()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = new TestButton
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(80, 30)
        };
        control.PressEffect = new PressScaleEffect
        {
            PressedScale = 0.8f,
            PressDuration = TimeSpan.Zero,
            ReleaseDuration = TimeSpan.Zero
        };
        control.Ripple = new RippleEffect { Duration = TimeSpan.FromMilliseconds(100) };

        MouseEventArgs first = Mouse(2, 2, 11, PointerDeviceKind.Touch);
        MouseEventArgs second = Mouse(3, 3, 12, PointerDeviceKind.Touch);
        control.DownForTest(first);
        control.DownForTest(second);
        Assert.Equal(VisualState.Pressed, control.VisualState);

        control.UpForTest(first);
        Assert.Equal(VisualState.Pressed, control.VisualState);
        Assert.Equal(2, control.Ripple.ActiveRippleCount);
        control.CancelPointerForTest(11);
        Assert.Equal(VisualState.Pressed, control.VisualState);
        Assert.Equal(1, control.Ripple.ActiveRippleCount);
        control.CancelPointerForTest(12);

        Assert.NotEqual(VisualState.Pressed, control.VisualState);
        Assert.Equal(0, control.Ripple.ActiveRippleCount);
        Assert.Equal(1f, control.EffectiveScaleX, 3);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void FocusLossReleasesKeyboardPressWithoutWaitingForKeyUp()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = new TestButton { AnimationSchedulerOverride = harness.Scheduler };
        control.PressEffect = new PressScaleEffect
        {
            PressedScale = 0.8f,
            PressDuration = TimeSpan.Zero,
            ReleaseDuration = TimeSpan.FromMilliseconds(100),
            Easing = Easings.Linear
        };

        control.KeyDownForTest(Keys.Space);
        Assert.Equal(0.8f, control.EffectiveScaleX, 3);

        control.LostFocusForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(1f, control.EffectiveScaleX, 3);
        Assert.NotEqual(VisualState.Pressed, control.VisualState);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void CollectionAttachDetachIsIdempotentAndDisposeCancelsOwnedWork()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var control = new TestButton
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(80, 30)
        };
        var ripple = new RippleEffect { Duration = TimeSpan.FromMilliseconds(100) };

        control.InteractionEffects.Add(ripple);
        control.InteractionEffects.Add(ripple);
        Assert.Single(control.InteractionEffects);
        Assert.Same(control, ripple.Target);
        control.DownForTest(Mouse(4, 4));
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);

        control.Dispose();

        Assert.Null(ripple.Target);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void RemovingConvenienceEffectClearsPropertyAndAllowsReattachment()
    {
        var control = new TestButton();
        var ripple = new RippleEffect();
        control.Ripple = ripple;

        Assert.True(control.InteractionEffects.Remove(ripple));
        Assert.Null(control.Ripple);
        Assert.Null(ripple.Target);

        control.Ripple = ripple;

        Assert.Same(ripple, control.Ripple);
        Assert.Same(control, ripple.Target);
        Assert.Single(control.InteractionEffects);
    }

    [Fact]
    public void DisabledAndDesignerTargetsDoNotStartRuntimeRippleWork()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var disabled = new TestButton
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Enabled = false,
            Size = new Size(80, 30),
            Ripple = new RippleEffect()
        };

        disabled.DownForTest(Mouse(4, 4));
        Assert.Equal(0, disabled.Ripple!.ActiveRippleCount);

        var designer = new TestButton
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(80, 30),
            Ripple = new RippleEffect(),
            PressEffect = new PressScaleEffect
            {
                PressedScale = 0.9f,
                PressDuration = TimeSpan.FromSeconds(1),
                ReleaseDuration = TimeSpan.FromSeconds(1)
            }
        };
        designer.Site = new DesignModeSite(designer);

        designer.DownForTest(Mouse(4, 4));
        Assert.Equal(0, designer.Ripple!.ActiveRippleCount);
        Assert.Equal(0.9f, designer.EffectiveScaleX, 3);
        designer.UpForTest(Mouse(4, 4));
        Assert.Equal(1f, designer.EffectiveScaleX, 3);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);

        var custom = new DesignerSchedulerProbeEffect();
        designer.InteractionEffects.Add(custom);
        designer.DownForTest(Mouse(5, 5));
        Assert.Equal(1, custom.UpdateCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void ReparentDoesNotDuplicateEffectsAndDisposalStopsFutureRepaint()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var firstParent = new Panel();
        var secondParent = new Panel();
        var control = new InvalidationCountingButton
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(80, 30),
            Ripple = new RippleEffect { Duration = TimeSpan.FromMilliseconds(100) }
        };
        firstParent.Controls.Add(control);
        firstParent.Controls.Remove(control);
        secondParent.Controls.Add(control);

        control.ResetInvalidations();
        control.DownForTest(Mouse(4, 4));
        Assert.Single(control.InteractionEffects);
        Assert.Equal(1, control.Ripple!.ActiveRippleCount);

        control.Dispose();
        int afterDispose = control.InvalidationCount;
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(afterDispose, control.InvalidationCount);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void OneSchedulerTickBatchesVisualInvalidationPerWindowWithoutLayout()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var firstWindow = new TestWindow();
        using var secondWindow = new TestWindow();
        var first = CreatePressButton(harness);
        var second = CreatePressButton(harness);
        int layouts = 0;
        first.Layout += (_, _) => layouts++;
        second.Layout += (_, _) => layouts++;
        firstWindow.Controls.Add(first);
        secondWindow.Controls.Add(second);
        first.DownForTest(Mouse(4, 4));
        second.DownForTest(Mouse(4, 4));
        firstWindow.ResetInvalidations();
        secondWindow.ResetInvalidations();
        layouts = 0;

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, firstWindow.InvalidationCount);
        Assert.Equal(1, secondWindow.InvalidationCount);
        Assert.Equal(0, layouts);
    }

    [Fact]
    public void SharedRenderHookPreservesBelowContentAboveFocusOrdering()
    {
        var order = new List<string>();
        var control = new RenderOrderButton(order) { Size = new Size(40, 20) };
        control.InteractionEffects.Add(new RecordingEffect(
            InteractionEffectLayer.AboveBackgroundBelowContent,
            () => order.Add("below")));
        control.InteractionEffects.Add(new RecordingEffect(
            InteractionEffectLayer.AboveContent,
            () => order.Add("above")));
        using var bitmap = new SKBitmap(40, 20);
        using var canvas = new SKCanvas(bitmap);
        var args = new PaintEventArgs(bitmap.Info, canvas, 1d);

        control.RaisePaint(args);

        Assert.Equal(["below", "content", "above", "focus"], order);
    }

    private static MouseEventArgs Mouse(
        int x,
        int y,
        int pointerId = 0,
        PointerDeviceKind kind = PointerDeviceKind.Mouse)
        => new(
            MouseButtons.Left,
            1,
            x,
            y,
            Point.Empty,
            pointerId: pointerId,
            pointerKind: kind);

    private static SKBitmap RenderEffect(Control control, InteractionEffectLayer layer)
    {
        var bitmap = new SKBitmap(control.Width, control.Height);
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);
        control.RenderInteractionEffects(
            layer,
            new PaintEventArgs(bitmap.Info, canvas, 1d));
        canvas.Flush();
        return bitmap;
    }

    private static TestButton CreatePressButton(AnimationSchedulerTestHarness harness)
    {
        var control = new TestButton { AnimationSchedulerOverride = harness.Scheduler };
        control.PressEffect = new PressScaleEffect
        {
            PressedScale = 0.9f,
            PressDuration = TimeSpan.FromMilliseconds(100),
            Easing = Easings.Linear
        };
        return control;
    }

    private class TestButton : Button
    {
        public void DownForTest(MouseEventArgs e) => OnMouseDown(e);
        public void UpForTest(MouseEventArgs e) => OnMouseUp(e);
        public void KeyDownForTest(Keys key) => OnKeyDown(new KeyEventArgs(key));
        public void KeyUpForTest(Keys key) => OnKeyUp(new KeyEventArgs(key));
        public void LostFocusForTest() => OnLostFocus(EventArgs.Empty);
        public void CancelPointerForTest(int? pointerId = null) => CancelPointerInteraction(pointerId);
    }

    private sealed class InvalidationCountingButton : TestButton
    {
        public int InvalidationCount { get; private set; }

        public void ResetInvalidations() => InvalidationCount = 0;

        protected override void OnInvalidated(EventArgs<Rectangle> e)
        {
            InvalidationCount++;
            base.OnInvalidated(e);
        }
    }

    private sealed class RenderOrderButton(List<string> order) : TestButton
    {
        protected override void OnPaint(PaintEventArgs e) => order.Add("content");
        internal override void RenderFocusOverlay(PaintEventArgs e) => order.Add("focus");
    }

    private sealed class RecordingEffect(
        InteractionEffectLayer layer,
        Action render) : InteractionEffect
    {
        public override InteractionEffectLayer RenderLayer => layer;

        protected override void OnRender(InteractionEffectRenderContext context) => render();
    }

    private sealed class DesignerSchedulerProbeEffect : InteractionEffect
    {
        public int UpdateCount { get; private set; }

        protected override void OnPointerDown(MouseEventArgs e)
            => Scheduler.Start(
                this,
                "DesignerProbe",
                _ => UpdateCount++,
                new AnimationOptions { Duration = TimeSpan.FromSeconds(1) });
    }

    private sealed class DesignModeSite(IComponent component) : ISite
    {
        public IComponent Component { get; } = component;
        public IContainer? Container => null;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type serviceType) => null;
    }

    private sealed class TestWindow : WindowBase
    {
        private readonly RecordingWindowProxy proxy;

        public TestWindow()
            : this(DispatchProxy.Create<IWindowBaseImpl, RecordingWindowProxy>())
        {
        }

        private TestWindow(IWindowBaseImpl implementation)
            : base(implementation)
        {
            proxy = (RecordingWindowProxy)implementation;
        }

        public int InvalidationCount => proxy.InvalidationCount;

        public void ResetInvalidations() => proxy.InvalidationCount = 0;
    }

    private class RecordingWindowProxy : DispatchProxy
    {
        public int InvalidationCount { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IWindowBaseImpl.Invalidate))
                InvalidationCount++;
            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
                return null;
            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}

internal static class RippleEffectTestExtensions
{
    public static void CancelForTest(this RippleEffect effect)
        => effect.Enabled = false;
}
