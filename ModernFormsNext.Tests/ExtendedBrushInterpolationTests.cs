using System.Drawing;
using System.Numerics;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using Xunit;
using Xunit.Abstractions;
using SkiaSharp;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext.Tests;

// Other suites explicitly force GC. On this runtime, even counter-only control intervals can
// report positive per-thread deltas during concurrent collection. Reuse the existing isolated
// UI/scheduler collection; the measured frame loop and its zero-byte assertion stay unchanged.
[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class ExtendedBrushInterpolationTests(ITestOutputHelper output)
{
    private static BrushInterpolationOptions Extended => new() { EnableExtendedMorphing = true };

    [Fact]
    public void LinearLightBrushMidpointIsEncodedBackToSrgbWithoutChangingEndpoints()
    {
        var source = new SolidColorBrush(Color.Black);
        var target = new SolidColorBrush(Color.White);
        var interpolation = AnimationInterpolators.CreateBrushInterpolator(new BrushInterpolationOptions
        { ColorSpace = ColorInterpolationSpace.LinearSrgb });
        Assert.Same(source, interpolation.Interpolate(source, target, 0));
        var middle = Assert.IsType<SolidColorBrush>(interpolation.Interpolate(source, target, 0.5f));
        Assert.Equal(Color.FromArgb(255, 188, 188, 188).ToArgb(), middle.PaintColor.ToArgb());
        Assert.Same(target, interpolation.Interpolate(source, target, 1));
        Assert.Equal(Color.Black.ToArgb(), source.PaintColor.ToArgb());
    }

    [Fact]
    public void ExplicitPolicyIsCapturedWhenTheInterpolatorIsCreated()
    {
        var options = new BrushInterpolationOptions { ColorSpace = ColorInterpolationSpace.LinearSrgb };
        var interpolation = AnimationInterpolators.CreateBrushInterpolator(options);
        options.ColorSpace = ColorInterpolationSpace.Srgb;
        var middle = Assert.IsType<SolidColorBrush>(interpolation.Interpolate(
            new SolidColorBrush(Color.Black), new SolidColorBrush(Color.White), 0.5f));
        Assert.Equal(188, middle.PaintColor.R);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoPaintFadeUsesOnePrivatePaintBrushAndExactEndpointReferences(bool reverse)
    {
        var noPaint = new NoBrush();
        var paint = new SolidColorBrush(Color.Red) { Opacity = 0.8f };
        MfnBrush source = reverse ? paint : noPaint;
        MfnBrush target = reverse ? noPaint : paint;
        var interpolation = AnimationInterpolators.CreateBrushInterpolator(Extended);
        Assert.Same(source, interpolation.Interpolate(source, target, 0));
        var middle = Assert.IsType<SolidColorBrush>(interpolation.Interpolate(source, target, 0.5f));
        Assert.NotSame(paint, middle);
        Assert.Equal(0.4f, middle.Opacity, 5);
        Assert.Equal(Color.Red.ToArgb(), middle.PaintColor.ToArgb());
        Assert.Same(middle, interpolation.Interpolate(source, target, 0.75f));
        Assert.Same(target, interpolation.Interpolate(source, target, 1));
        Assert.Equal(0.8f, paint.Opacity);
    }

    [Fact]
    public void EmptyGradientCanFadeToAnotherKindsPaintWithoutInventingEmptyColors()
    {
        var source = new LinearGradientBrush();
        var target = new RadialGradientBrush { CenterPoint = new(0.3f, 0.4f), Radius = 0.8f };
        target.GradientStops.Add(new GradientStop(Color.Red, 0));
        target.GradientStops.Add(new GradientStop(Color.Blue, 1));
        var interpolation = AnimationInterpolators.CreateBrushInterpolator(Extended);
        var middle = Assert.IsType<RadialGradientBrush>(interpolation.Interpolate(source, target, 0.5f));
        Assert.Equal(0.5f, middle.Opacity);
        Assert.Equal(target.CenterPoint, middle.CenterPoint);
        Assert.Equal(target.Radius, middle.Radius);
        Assert.Equal(target.GradientStops[0].PaintColor, middle.GradientStops[0].PaintColor);
        Assert.Empty(source.GradientStops);
        Assert.Same(target, interpolation.Interpolate(source, target, 1));
    }

    [Fact]
    public void MatchingGlassLayersInterpolateColorsWithoutMutatingSharedBrushes()
    {
        var source = new GlassBrush { Tint = Color.Black, SecondaryTint = Color.Black,
            Highlight = Color.Black, Border = Color.Black, Opacity = 0.4f };
        var target = new GlassBrush { Tint = Color.White, SecondaryTint = Color.White,
            Highlight = Color.White, Border = Color.White, Opacity = 0.8f };
        var interpolation = AnimationInterpolators.CreateBrushInterpolator(Extended);
        var middle = Assert.IsType<GlassBrush>(interpolation.Interpolate(source, target, 0.5f));
        Assert.NotSame(source, middle);
        Assert.NotSame(target, middle);
        Assert.Equal(128, middle.Tint.R);
        Assert.Equal(128, middle.SecondaryTint.R);
        Assert.Equal(128, middle.Highlight.R);
        Assert.Equal(128, middle.Border.R);
        Assert.Equal(0.6f, middle.Opacity, 5);
        Assert.Equal(Color.Black.ToArgb(), source.Tint.ToArgb());
        Assert.Equal(Color.White.ToArgb(), target.Tint.ToArgb());
    }

    [Fact]
    public void LinearLightUsesStraightAlphaAndRoundTripsAllEncodedBytes()
    {
        var color = AnimationInterpolators.CreateColorInterpolator(ColorInterpolationSpace.LinearSrgb);
        Assert.Equal(Color.FromArgb(128, 188, 188, 188).ToArgb(), color.Interpolate(
            Color.FromArgb(32, 0, 0, 0), Color.FromArgb(224, 255, 255, 255), 0.5f).ToArgb());
        for (int value = 0; value <= 255; value++)
        {
            Color endpoint = Color.FromArgb(value, value, 255 - value, value);
            Assert.Equal(endpoint.ToArgb(), color.Interpolate(endpoint, endpoint, 0.37f).ToArgb());
        }
        Assert.Equal(Color.White.ToArgb(), color.Interpolate(Color.Black, Color.White, float.MaxValue).ToArgb());
        Assert.Throws<ArgumentOutOfRangeException>(() => color.Interpolate(Color.Black, Color.White, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BrushInterpolationOptions { ColorSpace = (ColorInterpolationSpace)99 });
        Assert.Throws<ArgumentNullException>(() => AnimationInterpolators.CreateBrushInterpolator(null!));
    }

    [Theory]
    [InlineData("solid", false)] [InlineData("solid", true)]
    [InlineData("linear", false)] [InlineData("linear", true)]
    [InlineData("radial", false)] [InlineData("radial", true)]
    [InlineData("sweep", false)] [InlineData("sweep", true)]
    [InlineData("glass", false)] [InlineData("glass", true)]
    public void NoPaintMappingPreservesPaintGeometryAndAuthoredOwnership(string kind, bool reverse)
    {
        MfnBrush empty = new SweepGradientBrush();
        MfnBrush paint = Paint(kind);
        paint.Transform = Matrix3x2.CreateTranslation(3, 7);
        paint.Opacity = 0.6f;
        MfnBrush from = reverse ? paint : empty, to = reverse ? empty : paint;
        var interpolation = AnimationInterpolators.CreateBrushInterpolator(Extended);
        MfnBrush middle = interpolation.Interpolate(from, to, 0.5f);
        Assert.Equal(paint.GetType(), middle.GetType());
        Assert.Equal(paint.Transform, middle.Transform);
        Assert.Equal(0.3f, middle.Opacity, 5);
        Assert.Equal(0.6f, paint.Opacity);
        Assert.NotSame(paint, middle);
        Assert.Same(from, interpolation.Interpolate(from, to, -2));
        Assert.Same(to, interpolation.Interpolate(from, to, 2));
    }

    [Fact]
    public void EmptyKindsCanCrossWithoutPaintingAndUnsupportedKindsRemainDiscrete()
    {
        MfnBrush[] empty = [new NoBrush(), new LinearGradientBrush(), new RadialGradientBrush(), new SweepGradientBrush()];
        foreach (MfnBrush from in empty)
        foreach (MfnBrush to in empty)
        {
            var interpolation = AnimationInterpolators.CreateBrushInterpolator(Extended);
            Assert.IsType<NoBrush>(interpolation.Interpolate(from, to, 0.5f));
            Assert.Same(from, interpolation.Interpolate(from, to, 0));
            Assert.Same(to, interpolation.Interpolate(from, to, 1));
        }
        Assert.False(AnimationInterpolators.TryCreateBrushInterpolator(new GlassBrush(),
            new GlassBrush { ShowHighlight = false }, out _, Extended));
        Assert.False(AnimationInterpolators.TryCreateBrushInterpolator(new GlassBrush(),
            new GlassBrush { ShowInnerBorder = false }, out _, Extended));
        Assert.False(AnimationInterpolators.TryCreateBrushInterpolator(new DerivedBrush(), new DerivedBrush(), out _, Extended));
        Assert.False(AnimationInterpolators.TryCreateBrushInterpolator(Paint("linear"), Paint("radial"), out _, Extended));
        Assert.False(AnimationInterpolators.TryCreateBrushInterpolator(new NoBrush(), Paint("solid"), out _));
        Assert.False(AnimationInterpolators.TryCreateBrushInterpolator(new GlassBrush(), new GlassBrush(), out _));
    }

    [Fact]
    public void TemporalPolicyDoesNotChangeSpatialGradientResampling()
    {
        var source = new LinearGradientBrush();
        source.GradientStops.Add(new GradientStop(Color.Black, 0));
        source.GradientStops.Add(new GradientStop(Color.White, 1));
        var target = new LinearGradientBrush();
        target.GradientStops.Add(new GradientStop(Color.Black, 0));
        target.GradientStops.Add(new GradientStop(Color.Black, 0.5f));
        target.GradientStops.Add(new GradientStop(Color.Black, 1));
        var interpolation = AnimationInterpolators.CreateBrushInterpolator(new() { ColorSpace = ColorInterpolationSpace.LinearSrgb });
        var nearSource = Assert.IsType<LinearGradientBrush>(interpolation.Interpolate(source, target, 0.0001f));
        Assert.Equal(128, nearSource.GradientStops[1].PaintColor.R);
        Assert.Equal(2, source.GradientStops.Count);
    }

    [Fact]
    public void FiniteExtremeGeometryRemainsFiniteAtTheMidpoint()
    {
        var from = new LinearGradientBrush { Start = new(float.MinValue, float.MaxValue),
            Transform = Matrix3x2.CreateTranslation(float.MinValue, float.MaxValue) };
        var to = new LinearGradientBrush { Start = new(float.MaxValue, float.MinValue),
            Transform = Matrix3x2.CreateTranslation(float.MaxValue, float.MinValue) };
        from.GradientStops.Add(new GradientStop(Color.Black, 0));
        to.GradientStops.Add(new GradientStop(Color.White, 1));
        var middle = Assert.IsType<LinearGradientBrush>(AnimationInterpolators.CreateBrushInterpolator().Interpolate(from, to, 0.5f));
        Assert.Equal(PointF.Empty, middle.Start);
        Assert.Equal(0, middle.Transform.M31);
        Assert.Equal(0, middle.Transform.M32);
    }

    [Fact]
    public void StopPlanningBudgetAdmitsBoundaryAndRejectsAuthoredAndNormalizedExcess()
    {
        var boundary = Stops(4096, 0);
        Assert.True(BrushAnimationPlan.TryCreateLocal(boundary, boundary, out _));
        var oversized = Stops(4097, 0);
        Assert.False(BrushAnimationPlan.TryCreateLocal(oversized, boundary, out _));
        Assert.Throws<NotSupportedException>(() => BrushAnimationPlan.Create(oversized, oversized, oversized));
        // Both authored collections fit, but their distinct canonical offset union does not.
        Assert.False(BrushAnimationPlan.TryCreateLocal(Stops(2048, 0), Stops(2049, 0.00001f), out _));
        Assert.Equal(4097, oversized.GradientStops.Count);
    }

    [Theory]
    [InlineData("linear")] [InlineData("glass")] [InlineData("solid")]
    public void ChangingWarmFramesAllocateNoManagedMemory(string kind)
    {
        MfnBrush from = Paint(kind), to = Paint(kind);
        MakeWhite(to);
        from.Opacity = 0.1f;
        to.Opacity = 0.9f;
        var policy = new BrushInterpolationOptions { EnableExtendedMorphing = true, ColorSpace = ColorInterpolationSpace.LinearSrgb };
        Assert.True(BrushAnimationPlan.TryCreateLocal(from, to, out var plan, policy));
        for (int index = 0; index < 2000; index++) plan!.Apply((index % 99 + 1) / 100f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++) plan!.Apply((index % 99 + 1) / 100f);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(0.1f, from.Opacity);
        Assert.Equal(0.9f, to.Opacity);
    }

    [Fact]
    public void SelectedColorPolicyIsVisibleInRenderedPixels()
    {
        var interpolation = AnimationInterpolators.CreateBrushInterpolator(new() { ColorSpace = ColorInterpolationSpace.LinearSrgb });
        using var control = new Panel { Width = 20, Height = 20 };
        control.Style.BackgroundBrush = interpolation.Interpolate(new SolidColorBrush(Color.Black), new SolidColorBrush(Color.White), 0.5f);
        using var surface = new SkiaControlSurface(control);
        surface.Resize(20, 20);
        using var bitmap = new SKBitmap(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        surface.Render(canvas);
        Assert.Equal(new SKColor(188, 188, 188), bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void VisualStateCapturesPolicyAndRapidRetargetUsesCurrentPresentation()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = new StateButton { AnimationSchedulerOverride = harness.Scheduler };
        var policy = new BrushInterpolationOptions { EnableExtendedMorphing = true, ColorSpace = ColorInterpolationSpace.LinearSrgb };
        var transition = new VisualStateTransition { Duration = TimeSpan.FromMilliseconds(100),
            Easing = Easings.Linear, BrushInterpolation = policy };
        control.Style.BackgroundBrush = new NoBrush();
        control.StyleHover.BackgroundBrush = new SolidColorBrush(Color.White);
        control.StylePressed.BackgroundBrush = new SolidColorBrush(Color.Black);
        control.StyleTransitions.Add(VisualState.Normal, VisualState.Hover, transition);
        control.StyleTransitions.Add(VisualState.Hover, VisualState.Pressed, transition);
        control.Enter();
        policy.EnableExtendedMorphing = false;
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        var presentation = Assert.IsType<SolidColorBrush>(control.EffectiveBackgroundBrush);
        Assert.Equal(0.5f, presentation.Opacity);
        control.Down();
        Assert.Same(presentation, control.EffectiveBackgroundBrush);
        policy.ColorSpace = ColorInterpolationSpace.Srgb;
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(188, Assert.IsType<SolidColorBrush>(control.EffectiveBackgroundBrush).PaintColor.R);
        control.Dispose();
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReducedMotionAndZeroDurationCommitExactExtendedTarget(bool reducedMotion)
    {
        using var harness = new AnimationSchedulerTestHarness();
        harness.Policy.ReducedMotion = reducedMotion;
        using var control = new StateButton { AnimationSchedulerOverride = harness.Scheduler };
        var target = new GlassBrush();
        control.Style.BackgroundBrush = new NoBrush();
        control.StyleHover.BackgroundBrush = target;
        control.StyleTransitions.Add(VisualState.Normal, VisualState.Hover, new VisualStateTransition
        {
            Duration = reducedMotion ? TimeSpan.FromMilliseconds(100) : TimeSpan.Zero,
            BrushInterpolation = Extended
        });
        control.Enter();
        Assert.Same(target, control.EffectiveBackgroundBrush);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void InPlaceGlassPolicyKeepsIdentityAndReplacementCapturesCurrentColor()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var brush = new GlassBrush { Tint = Color.Black };
        var white = new GlassBrush { Tint = Color.White };
        var policy = new BrushInterpolationOptions { EnableExtendedMorphing = true, ColorSpace = ColorInterpolationSpace.LinearSrgb };
        var first = brush.AnimateTo(white, policy, TimeSpan.FromMilliseconds(100), scheduler: harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(188, brush.Tint.R);
        var second = brush.AnimateTo(new GlassBrush { Tint = Color.Black }, policy,
            TimeSpan.FromMilliseconds(100), scheduler: harness.Scheduler);
        Assert.Equal(AnimationState.Canceled, first.State);
        Assert.Equal(188, brush.Tint.R);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(AnimationState.Completed, second.State);
        Assert.Equal(Color.Black.ToArgb(), brush.Tint.ToArgb());
        Assert.Equal(Color.White.ToArgb(), white.Tint.ToArgb());
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void ExtendedPolicyKeepsNullAsFallbackAndSnapsUnsupportedPairs()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = new StateButton { AnimationSchedulerOverride = harness.Scheduler };
        var target = new SolidColorBrush(Color.White);
        control.Style.BackgroundBrush = null;
        control.StyleHover.BackgroundBrush = target;
        var transition = new VisualStateTransition { Duration = TimeSpan.FromMilliseconds(100), BrushInterpolation = Extended };
        control.StyleTransitions.Add(VisualState.Normal, VisualState.Hover, transition);
        control.StyleTransitions.Add(VisualState.Hover, VisualState.Normal, transition);
        control.Enter();
        Assert.Same(target, control.EffectiveBackgroundBrush);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        control.Leave();
        Assert.Null(control.EffectiveBackgroundBrush);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().FaultedCount);
    }

    [Fact]
    public void DetachingExtendedPresentationRetiresOwnerAndReattachKeepsAuthoredBrushes()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var parent = new Panel();
        using var control = parent.Controls.Add(new StateButton { AnimationSchedulerOverride = harness.Scheduler });
        var noPaint = new NoBrush();
        var target = new GlassBrush();
        control.Style.BackgroundBrush = noPaint;
        control.StyleHover.BackgroundBrush = target;
        control.StyleTransitions.Add(VisualState.Normal, VisualState.Hover,
            new VisualStateTransition { Duration = TimeSpan.FromMilliseconds(100), BrushInterpolation = Extended });
        control.Enter();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.NotSame(target, control.EffectiveBackgroundBrush);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        parent.Controls.Remove(control);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
        parent.Controls.Add(control);
        Assert.Same(noPaint, control.Style.BackgroundBrush);
        Assert.Same(target, control.StyleHover.BackgroundBrush);
        Assert.Equal(1, target.Opacity);
    }

    private static LinearGradientBrush Stops(int count, float shift)
    {
        var result = new LinearGradientBrush();
        for (int index = 0; index < count; index++)
            result.GradientStops.Add(new GradientStop(Color.Black, index / (float)count + shift));
        return result;
    }

    [Theory]
    [InlineData("solid")] [InlineData("linear")] [InlineData("radial")]
    [InlineData("sweep")] [InlineData("glass")]
    public void NoPaintFadePixelsCompositeOverTheParentWithoutChangingAuthoredPaint(string kind)
    {
        MfnBrush target = Paint(kind);
        MakeWhite(target);
        if (target is GlassBrush glass) { glass.ShowHighlight = false; glass.ShowInnerBorder = false; }
        var interpolation = AnimationInterpolators.CreateBrushInterpolator(Extended);
        using var parent = new Panel { Width = 20, Height = 20, BackColor = new SKColor(211, 211, 211) };
        var child = parent.Controls.Add(new Panel { Bounds = new Rectangle(0, 0, 20, 20) });
        child.Style.BackgroundBrush = interpolation.Interpolate(new NoBrush(), target, 0.5f);
        using var surface = new SkiaControlSurface(parent);
        surface.Resize(20, 20);
        using var bitmap = new SKBitmap(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        surface.Render(canvas);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.InRange(pixel.Red, 232, 234);
        Assert.Equal(pixel.Red, pixel.Green);
        Assert.Equal(pixel.Red, pixel.Blue);
        Assert.Equal(255, pixel.Alpha);
        Assert.Equal(1, target.Opacity);
    }

    [Fact]
    public void GradientStopPolicyUsesLinearLightAndExistingOverloadsRemainUnambiguous()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var stop = new GradientStop(Color.Black, 0);
        stop.AnimateTo(new GradientStop(Color.White, 1), ColorInterpolationSpace.LinearSrgb,
            TimeSpan.FromMilliseconds(100), scheduler: harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(188, stop.PaintColor.R);
        Assert.Equal(0.5f, stop.Offset);
        Assert.Throws<ArgumentNullException>(() => new SolidColorBrush(Color.Black).AnimateTo(
            new SolidColorBrush(Color.White), TimeSpan.Zero, default!, scheduler: harness.Scheduler));
        Assert.Throws<ArgumentNullException>(() => new GradientStop(Color.Black, 0).AnimateTo(
            new GradientStop(Color.White, 1), TimeSpan.Zero, default!, scheduler: harness.Scheduler));
    }

    [Fact]
    public void MaximumCanonicalPlanHasBoundedAllocationAndReusableFrames()
    {
        var source = Stops(4096, 0);
        var target = new LinearGradientBrush();
        for (int index = 0; index < 4095; index++)
            target.GradientStops.Add(new GradientStop(Color.White, index / 4096f));
        long before = GC.GetAllocatedBytesForCurrentThread();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        Assert.True(BrushAnimationPlan.TryCreateLocal(source, target, out var plan,
            new() { ColorSpace = ColorInterpolationSpace.LinearSrgb }));
        double elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"4096 canonical stops: planning bytes={allocated}, milliseconds={elapsed:F3}");
        Assert.InRange(allocated, 1, 8 * 1024 * 1024);
        var working = Assert.IsType<LinearGradientBrush>(plan!.Interpolate(0.25f));
        Assert.Equal(4096, working.GradientStops.Count);
        Assert.Same(working, plan.Interpolate(0.75f));
        Assert.Equal(4096, source.GradientStops.Count);
        Assert.Equal(4095, target.GradientStops.Count);
    }

    private static void MakeWhite(MfnBrush brush)
    {
        if (brush is SolidColorBrush solid) solid.PaintColor = Color.White;
        if (brush is GlassBrush glass) glass.Tint = glass.SecondaryTint = glass.Highlight = glass.Border = Color.White;
        if (brush is GradientBrush gradient)
            foreach (var stop in gradient.GradientStops) stop.PaintColor = Color.White;
    }

    private static MfnBrush Paint(string kind)
    {
        if (kind == "solid") return new SolidColorBrush(Color.Red);
        if (kind == "glass") return new GlassBrush { Tint = Color.Red };
        GradientBrush brush = kind switch
        {
            "linear" => new LinearGradientBrush(), "radial" => new RadialGradientBrush(),
            "sweep" => new SweepGradientBrush(), _ => throw new ArgumentException("Unknown test brush.")
        };
        brush.GradientStops.Add(new GradientStop(Color.Red, 0));
        brush.GradientStops.Add(new GradientStop(Color.Blue, 1));
        return brush;
    }

    private sealed class DerivedBrush : SolidColorBrush;

    private sealed class StateButton : Button
    {
        public void Enter() => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));
        public void Down() => RaiseMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, Point.Empty));
        public void Leave() => OnMouseLeave(EventArgs.Empty);
    }
}
