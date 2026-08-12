using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using SkiaSharp;
using Xunit;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext.Tests;

public sealed class BrushInterpolationCompatibilityTests
{
    [Fact]
    public void SolidInterpolationReturnsExactEndpointsAndIncludesAlpha()
    {
        var from = new SolidColorBrush(Color.FromArgb(20, 10, 30, 50));
        var to = new SolidColorBrush(Color.FromArgb(220, 110, 130, 150));
        IAnimationInterpolator<MfnBrush> interpolator = AnimationInterpolators.CreateBrushInterpolator();

        Assert.Same(from, interpolator.Interpolate(from, to, -0.25f));
        var middle = Assert.IsType<SolidColorBrush>(interpolator.Interpolate(from, to, 0.5f));
        Assert.Equal(Color.FromArgb(120, 60, 80, 100).ToArgb(), middle.PaintColor.ToArgb());
        Assert.Same(to, interpolator.Interpolate(from, to, 1f));
        Assert.Same(to, interpolator.Interpolate(from, to, 1.25f));
    }

    [Fact]
    public void DifferentStopCountsNormalizeOnceWithoutMutatingAuthoredCollections()
    {
        LinearGradientBrush from = Linear(
            (Color.Red, 0f),
            (Color.Blue, 1f));
        LinearGradientBrush to = Linear(
            (Color.Green, 0f),
            (Color.Yellow, 0.5f),
            (Color.White, 1f));
        IAnimationInterpolator<MfnBrush> interpolator = AnimationInterpolators.CreateBrushInterpolator();

        var first = Assert.IsType<LinearGradientBrush>(interpolator.Interpolate(from, to, 0.25f));
        var middle = Assert.IsType<LinearGradientBrush>(interpolator.Interpolate(from, to, 0.5f));

        Assert.Same(first, middle);
        Assert.Equal([0f, 0.5f, 1f], middle.GradientStops.Select(static stop => stop.Offset));
        Assert.Equal(3, middle.GradientStops.Count);
        Assert.Equal(2, from.GradientStops.Count);
        Assert.Equal(3, to.GradientStops.Count);
        Assert.Equal(Color.Red.ToArgb(), from.GradientStops[0].PaintColor.ToArgb());
        Assert.Equal(Color.Yellow.ToArgb(), to.GradientStops[1].PaintColor.ToArgb());
    }

    [Fact]
    public void StopNormalizationPreservesHardStopMultiplicityAndStableOrder()
    {
        LinearGradientBrush from = Linear(
            (Color.Red, 0f),
            (Color.Blue, 0f),
            (Color.White, 1f));
        LinearGradientBrush to = Linear(
            (Color.Green, 0f),
            (Color.Black, 0.5f),
            (Color.Yellow, 1f),
            (Color.White, 1f));

        var middle = Assert.IsType<LinearGradientBrush>(
            AnimationInterpolators.CreateBrushInterpolator().Interpolate(from, to, 0.5f));

        Assert.Equal([0f, 0f, 0.5f, 1f, 1f], middle.GradientStops.Select(static stop => stop.Offset));
        Assert.NotEqual(
            middle.GradientStops[0].PaintColor.ToArgb(),
            middle.GradientStops[1].PaintColor.ToArgb());

        LinearGradientBrush internalHardStop = Linear(
            (Color.Red, 0f),
            (Color.Green, 0.25f),
            (Color.Blue, 0.25f),
            (Color.White, 1f));
        LinearGradientBrush shifted = Linear(
            (Color.Black, 0f),
            (Color.Yellow, 0.75f),
            (Color.White, 1f));

        var shiftedMiddle = Assert.IsType<LinearGradientBrush>(
            AnimationInterpolators.CreateBrushInterpolator().Interpolate(internalHardStop, shifted, 0.5f));

        Assert.Equal(
            [0f, 0.25f, 0.25f, 0.75f, 1f],
            shiftedMiddle.GradientStops.Select(static stop => stop.Offset));
        Assert.NotEqual(
            shiftedMiddle.GradientStops[1].PaintColor.ToArgb(),
            shiftedMiddle.GradientStops[2].PaintColor.ToArgb());
    }

    [Fact]
    public void EqualStopCountsContinueToInterpolateOffsets()
    {
        LinearGradientBrush from = Linear(
            (Color.Red, 0f),
            (Color.Blue, 0.6f));
        LinearGradientBrush to = Linear(
            (Color.Green, 0.2f),
            (Color.White, 1f));

        var middle = Assert.IsType<LinearGradientBrush>(
            AnimationInterpolators.CreateBrushInterpolator().Interpolate(from, to, 0.5f));

        Assert.Equal(0.1f, middle.GradientStops[0].Offset, 3);
        Assert.Equal(0.8f, middle.GradientStops[1].Offset, 3);
    }

    [Fact]
    public void SolidAndGradientUseTheGradientGeometryWithAConstantSolidEndpoint()
    {
        var solid = new SolidColorBrush(Color.Red) { Opacity = 0.4f };
        LinearGradientBrush gradient = Linear(
            (Color.Blue, 0f),
            (Color.Green, 0.4f),
            (Color.White, 1f));
        gradient.Start = new PointF(0.2f, 0.3f);
        gradient.End = new PointF(0.8f, 0.9f);
        gradient.Opacity = 0.8f;

        var forward = Assert.IsType<LinearGradientBrush>(
            AnimationInterpolators.CreateBrushInterpolator().Interpolate(solid, gradient, 0.5f));
        var reverse = Assert.IsType<LinearGradientBrush>(
            AnimationInterpolators.CreateBrushInterpolator().Interpolate(gradient, solid, 0.5f));

        Assert.Equal(gradient.Start, forward.Start);
        Assert.Equal(gradient.End, forward.End);
        Assert.Equal(0.6f, forward.Opacity, 3);
        Assert.Equal(gradient.GradientStops.Count, forward.GradientStops.Count);
        Assert.Equal(gradient.Start, reverse.Start);
        Assert.Equal(gradient.End, reverse.End);
        Assert.Same(gradient, AnimationInterpolators.CreateBrushInterpolator().Interpolate(solid, gradient, 1f));
    }

    [Theory]
    [InlineData("Linear")]
    [InlineData("Radial")]
    [InlineData("Sweep")]
    public void EveryGradientKindNormalizesDifferentStopCounts(string kind)
    {
        GradientBrush from = Gradient(
            kind,
            (Color.Red, 0f),
            (Color.Blue, 1f));
        GradientBrush to = Gradient(
            kind,
            (Color.Green, 0f),
            (Color.Yellow, 0.4f),
            (Color.White, 1f));

        IAnimationInterpolator<MfnBrush> interpolator = AnimationInterpolators.CreateBrushInterpolator();

        Assert.Same(from, interpolator.Interpolate(from, to, -0.1f));
        MfnBrush middle = interpolator.Interpolate(from, to, 0.5f);
        Assert.Same(to, interpolator.Interpolate(from, to, 1.1f));

        Assert.IsType(from.GetType(), middle);
        Assert.Equal(
            [0f, 0.4f, 1f],
            Assert.IsAssignableFrom<GradientBrush>(middle).GradientStops.Select(static stop => stop.Offset));
        Assert.Equal(2, from.GradientStops.Count);
        Assert.Equal(3, to.GradientStops.Count);
    }

    [Theory]
    [InlineData("Linear")]
    [InlineData("Radial")]
    [InlineData("Sweep")]
    public void SolidPromotionWorksForEveryGradientKind(string kind)
    {
        var solid = new SolidColorBrush(Color.FromArgb(96, 10, 20, 30));
        GradientBrush gradient = Gradient(
            kind,
            (Color.Red, 0f),
            (Color.Green, 0.3f),
            (Color.Blue, 1f));

        IAnimationInterpolator<MfnBrush> forwardInterpolator = AnimationInterpolators.CreateBrushInterpolator();
        IAnimationInterpolator<MfnBrush> reverseInterpolator = AnimationInterpolators.CreateBrushInterpolator();

        Assert.Same(solid, forwardInterpolator.Interpolate(solid, gradient, 0f));
        MfnBrush forward = forwardInterpolator.Interpolate(solid, gradient, 0.5f);
        Assert.Same(gradient, forwardInterpolator.Interpolate(solid, gradient, 1f));
        Assert.Same(gradient, reverseInterpolator.Interpolate(gradient, solid, 0f));
        MfnBrush reverse = reverseInterpolator.Interpolate(gradient, solid, 0.5f);
        Assert.Same(solid, reverseInterpolator.Interpolate(gradient, solid, 1f));

        Assert.IsType(gradient.GetType(), forward);
        Assert.IsType(gradient.GetType(), reverse);
        Assert.Equal(3, Assert.IsAssignableFrom<GradientBrush>(forward).GradientStops.Count);
        Assert.Equal(3, Assert.IsAssignableFrom<GradientBrush>(reverse).GradientStops.Count);
    }

    [Fact]
    public void IncompatibleGeometryGlassNoBrushAndDerivedBrushesUseCompatibilityFallback()
    {
        LinearGradientBrush linear = Linear((Color.Red, 0f), (Color.Blue, 1f));
        RadialGradientBrush radial = Radial((Color.Green, 0f), (Color.White, 1f));
        SweepGradientBrush sweep = Sweep((Color.Black, 0f), (Color.White, 1f));
        var solid = new SolidColorBrush(Color.Red);
        var glass = new GlassBrush();
        var noBrush = new NoBrush();

        AssertIncompatible(linear, radial);
        AssertIncompatible(radial, linear);
        AssertIncompatible(linear, sweep);
        AssertIncompatible(sweep, linear);
        AssertIncompatible(radial, sweep);
        AssertIncompatible(sweep, radial);
        AssertIncompatible(glass, glass);
        AssertIncompatible(glass, solid);
        AssertIncompatible(solid, glass);
        AssertIncompatible(noBrush, noBrush);
        AssertIncompatible(noBrush, solid);
        AssertIncompatible(solid, noBrush);
        Assert.False(AnimationInterpolators.TryCreateBrushInterpolator(
            new DerivedSolidBrush(Color.Red),
            new DerivedSolidBrush(Color.Blue),
            out _));
        Assert.Throws<ArgumentException>(() =>
            AnimationInterpolators.CreateBrushInterpolator().Interpolate(linear, radial, 0.5f));
    }

    [Fact]
    public void EmptyGradientDoesNotImplicitlyBecomeTransparentPaint()
    {
        var empty = new LinearGradientBrush();
        var populated = Linear((Color.Red, 0f), (Color.Blue, 1f));

        Assert.False(AnimationInterpolators.TryCreateBrushInterpolator(empty, populated, out _));
        Assert.False(AnimationInterpolators.TryCreateBrushInterpolator(
            new SolidColorBrush(Color.Transparent),
            empty,
            out _));
    }

    [Fact]
    public void PreparedPlanDoesNotAllocatePerIntermediateFrame()
    {
        LinearGradientBrush from = Linear(
            (Color.Red, 0f),
            (Color.Blue, 1f));
        LinearGradientBrush to = Linear(
            (Color.Green, 0f),
            (Color.Yellow, 0.25f),
            (Color.White, 1f));
        Assert.True(BrushAnimationPlan.TryCreateLocal(from, to, out BrushAnimationPlan? plan));

        for (int index = 0; index < 32; index++)
            plan!.Apply(0.5f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1_000; index++)
            plan!.Apply(0.5f);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.InRange(allocated, 0, 256);
    }

    [Fact]
    public void VisualStateUsesInterpolatedBackgroundForegroundAndBorderBrushesForRendering()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreateBrushTransitionButton(harness);
        control.Text = "Brush";
        control.Width = 40;
        control.Height = 20;
        control.Style.BackgroundBrush = new SolidColorBrush(Color.Red);
        control.Style.ForegroundBrush = new SolidColorBrush(Color.Black);
        control.Style.BorderBrush = new SolidColorBrush(Color.Blue);
        control.Style.Border.Width = 2;
        control.StyleHover.BackgroundBrush = Linear(
            (Color.Blue, 0f),
            (Color.Green, 0.5f),
            (Color.White, 1f));
        control.StyleHover.ForegroundBrush = Linear((Color.White, 0f), (Color.Yellow, 1f));
        control.StyleHover.BorderBrush = Linear((Color.Yellow, 0f), (Color.Magenta, 1f));
        using var surface = new SkiaControlSurface(control);
        surface.Resize(40, 20);

        control.EnterForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.IsType<LinearGradientBrush>(control.EffectiveBackgroundBrush);
        Assert.IsType<LinearGradientBrush>(control.EffectiveTextBrush);
        Assert.IsType<LinearGradientBrush>(control.EffectiveBorderBrush);
        using var bitmap = new SKBitmap(40, 20, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        surface.Render(canvas);
        canvas.Flush();
        SKColor pixel = bitmap.GetPixel(10, 10);
        Assert.NotEqual(SKColors.Red, pixel);
        Assert.NotEqual(SKColors.Blue, pixel);
    }

    [Fact]
    public void RapidVisualStateRetargetStartsFromCurrentPresentationBrushAndCleansScheduler()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreateBrushTransitionButton(harness);
        control.Style.BackgroundBrush = new SolidColorBrush(Color.Red);
        control.StyleHover.BackgroundBrush = Linear((Color.Blue, 0f), (Color.Green, 1f));
        control.StylePressed.BackgroundBrush = Linear(
            (Color.Yellow, 0f),
            (Color.Magenta, 0.5f),
            (Color.White, 1f));
        AddTransition(control, VisualState.Hover, VisualState.Pressed);

        control.EnterForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));
        MfnBrush beforeRetarget = control.EffectiveBackgroundBrush!;
        control.DownForTest();

        Assert.Same(beforeRetarget, control.EffectiveBackgroundBrush);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Same(control.StylePressed.BackgroundBrush, control.EffectiveBackgroundBrush);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void UnsupportedAndNullVisualStatePairsSwitchBrushDiscretelyWithoutFaulting()
    {
        using var harness = new AnimationSchedulerTestHarness();
        AssertDiscreteTransition(
            harness,
            Linear((Color.Red, 0f), (Color.Blue, 1f)),
            Radial((Color.Green, 0f), (Color.White, 1f)));
        AssertDiscreteTransition(harness, null, null);
        AssertDiscreteTransition(harness, null, new SolidColorBrush(Color.Red));
        AssertDiscreteReverseTransitionToNull(harness, new SolidColorBrush(Color.Red));
        AssertDiscreteTransition(harness, new NoBrush(), new SolidColorBrush(Color.Red));
        AssertDiscreteTransition(harness, new SolidColorBrush(Color.Red), new NoBrush());
        AssertDiscreteTransition(harness, new GlassBrush(), new SolidColorBrush(Color.Red));
        AssertDiscreteTransition(harness, new SolidColorBrush(Color.Red), new GlassBrush());
        AssertDiscreteTransition(harness, new GlassBrush(), new GlassBrush());

        Assert.Equal(0, harness.Scheduler.GetDiagnostics().FaultedCount);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void ZeroDurationUsesExactTargetAndDoesNotStartTickSource()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = new TestButton { AnimationSchedulerOverride = harness.Scheduler };
        control.Style.BackgroundBrush = new SolidColorBrush(Color.Red);
        var target = Linear((Color.Blue, 0f), (Color.Green, 1f));
        control.StyleHover.BackgroundBrush = target;
        control.StyleTransitions.Add(
            VisualState.Normal,
            VisualState.Hover,
            new VisualStateTransition { Duration = TimeSpan.Zero });

        control.EnterForTest();

        Assert.Same(target, control.EffectiveBackgroundBrush);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    private static TestButton CreateBrushTransitionButton(AnimationSchedulerTestHarness harness)
    {
        var control = new TestButton { AnimationSchedulerOverride = harness.Scheduler };
        AddTransition(control, VisualState.Normal, VisualState.Hover);
        return control;
    }

    private static void AddTransition(TestButton control, VisualState from, VisualState to)
        => control.StyleTransitions.Add(
            from,
            to,
            new VisualStateTransition
            {
                Duration = TimeSpan.FromMilliseconds(100),
                Easing = Easings.Linear
            });

    private static LinearGradientBrush Linear(params (Color Color, float Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            Start = new PointF(0f, 0f),
            End = new PointF(1f, 0f)
        };
        foreach ((Color color, float offset) in stops)
            brush.GradientStops.Add(new GradientStop(color, offset));
        return brush;
    }

    private static RadialGradientBrush Radial(params (Color Color, float Offset)[] stops)
    {
        var brush = new RadialGradientBrush();
        foreach ((Color color, float offset) in stops)
            brush.GradientStops.Add(new GradientStop(color, offset));
        return brush;
    }

    private static SweepGradientBrush Sweep(params (Color Color, float Offset)[] stops)
    {
        var brush = new SweepGradientBrush();
        foreach ((Color color, float offset) in stops)
            brush.GradientStops.Add(new GradientStop(color, offset));
        return brush;
    }

    private static void AssertIncompatible(MfnBrush from, MfnBrush to)
        => Assert.False(AnimationInterpolators.TryCreateBrushInterpolator(from, to, out _));

    private static void AssertDiscreteTransition(
        AnimationSchedulerTestHarness harness,
        MfnBrush? from,
        MfnBrush? to)
    {
        using var control = CreateBrushTransitionButton(harness);
        control.Style.BackgroundBrush = from;
        control.StyleHover.BackgroundBrush = to;

        control.EnterForTest();

        Assert.Same(to ?? from, control.EffectiveBackgroundBrush);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Same(to ?? from, control.EffectiveBackgroundBrush);
    }

    private static void AssertDiscreteReverseTransitionToNull(
        AnimationSchedulerTestHarness harness,
        MfnBrush from)
    {
        using var control = CreateBrushTransitionButton(harness);
        AddTransition(control, VisualState.Hover, VisualState.Normal);
        control.Style.BackgroundBrush = null;
        control.StyleHover.BackgroundBrush = from;
        control.EnterForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        control.LeaveForTest();

        Assert.Null(control.EffectiveBackgroundBrush);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Null(control.EffectiveBackgroundBrush);
    }

    private static GradientBrush Gradient(
        string kind,
        params (Color Color, float Offset)[] stops)
    {
        GradientBrush brush = kind switch
        {
            "Linear" => new LinearGradientBrush
            {
                Start = new PointF(0f, 0f),
                End = new PointF(1f, 1f)
            },
            "Radial" => new RadialGradientBrush
            {
                CenterPoint = new PointF(0.5f, 0.5f),
                GradientOrigin = new PointF(0.4f, 0.3f),
                Radius = 0.8f
            },
            "Sweep" => new SweepGradientBrush
            {
                CenterPoint = new PointF(0.5f, 0.5f),
                StartAngle = 10f,
                EndAngle = 300f
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        foreach ((Color color, float offset) in stops)
            brush.GradientStops.Add(new GradientStop(color, offset));
        return brush;
    }

    private sealed class DerivedSolidBrush(Color color) : SolidColorBrush(color);

    private sealed class TestButton : Button
    {
        public void EnterForTest()
            => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));

        public void DownForTest()
            => RaiseMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, Point.Empty));

        public void LeaveForTest()
            => OnMouseLeave(EventArgs.Empty);
    }
}
