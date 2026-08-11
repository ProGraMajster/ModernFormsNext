using System.Drawing;
using System.Numerics;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using Xunit;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext.Tests;

public sealed class AnimationInterpolatorTests
{
    [Fact]
    public void PrimitiveInterpolatorsUseComponentWiseLinearValues()
    {
        Assert.Equal(2.5f, AnimationInterpolators.Float.Interpolate(0f, 10f, 0.25f));
        Assert.Equal(7.5d, AnimationInterpolators.Double.Interpolate(0d, 10d, 0.75f));
        Assert.Equal(3, AnimationInterpolators.Int32.Interpolate(0, 5, 0.5f));
        Assert.Equal(new PointF(5f, 15f),
            AnimationInterpolators.PointF.Interpolate(new PointF(0f, 10f), new PointF(10f, 20f), 0.5f));
        Assert.Equal(new SizeF(15f, 25f),
            AnimationInterpolators.SizeF.Interpolate(new SizeF(10f, 20f), new SizeF(20f, 30f), 0.5f));
        Assert.Equal(new RectangleF(5f, 10f, 15f, 20f),
            AnimationInterpolators.RectangleF.Interpolate(
                new RectangleF(0f, 0f, 10f, 10f),
                new RectangleF(10f, 20f, 20f, 30f),
                0.5f));
        Assert.Equal(new Padding(5, 10, 15, 20),
            AnimationInterpolators.Padding.Interpolate(
                new Padding(0, 0, 10, 10),
                new Padding(10, 20, 20, 30),
                0.5f));
    }

    [Fact]
    public void ColorInterpolatorIncludesAlphaAndClampsOvershoot()
    {
        Color middle = AnimationInterpolators.Color.Interpolate(
            Color.FromArgb(0, 0, 20, 40),
            Color.FromArgb(200, 100, 120, 140),
            0.5f);
        Color overshoot = AnimationInterpolators.Color.Interpolate(Color.Black, Color.White, 2f);

        Assert.Equal(Color.FromArgb(100, 50, 70, 90).ToArgb(), middle.ToArgb());
        Assert.Equal(Color.White.ToArgb(), overshoot.ToArgb());
    }

    [Fact]
    public void MatrixInterpolatorIncludesScaleShearAndTranslationComponents()
    {
        Matrix3x2 result = AnimationInterpolators.Matrix3x2.Interpolate(
            Matrix3x2.Identity,
            new Matrix3x2(3f, 2f, 4f, 5f, 20f, 30f),
            0.5f);

        Assert.Equal(new Matrix3x2(2f, 1f, 2f, 3f, 10f, 15f), result);
    }

    [Fact]
    public void InterpolatorsRejectNonFiniteProgress()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            AnimationInterpolators.Float.Interpolate(0f, 1f, float.NaN));

    [Fact]
    public void GradientStopInterpolatorReusesOneAnimationLocalResult()
    {
        IAnimationInterpolator<GradientStop> interpolator = AnimationInterpolators.CreateGradientStopInterpolator();
        var from = new GradientStop(Color.Black, 0f);
        var to = new GradientStop(Color.White, 1f);

        GradientStop first = interpolator.Interpolate(from, to, 0.25f);
        GradientStop second = interpolator.Interpolate(from, to, 0.75f);

        Assert.Same(first, second);
        Assert.Equal(0.75f, second.Offset);
        Assert.Equal(Color.FromArgb(191, 191, 191).ToArgb(), second.PaintColor.ToArgb());
        Assert.Equal(0f, from.Offset);
        Assert.Equal(1f, to.Offset);
    }

    [Fact]
    public void BrushInterpolatorClonesOnceAndDoesNotMutateEndpoints()
    {
        var from = new SolidColorBrush(Color.Red)
        {
            Opacity = 0.4f,
            Transform = Matrix3x2.CreateTranslation(2f, 4f)
        };
        var to = new SolidColorBrush(Color.Blue)
        {
            Opacity = 0.8f,
            Transform = Matrix3x2.CreateTranslation(10f, 20f)
        };
        IAnimationInterpolator<MfnBrush> interpolator = AnimationInterpolators.CreateBrushInterpolator();

        var first = Assert.IsType<SolidColorBrush>(interpolator.Interpolate(from, to, 0.5f));
        var second = Assert.IsType<SolidColorBrush>(interpolator.Interpolate(from, to, 0.75f));

        Assert.Same(first, second);
        Assert.NotSame(from, first);
        Assert.Equal(0.7f, second.Opacity, 3);
        Assert.Equal(Matrix3x2.CreateTranslation(8f, 16f), second.Transform);
        Assert.Equal(Color.Red.ToArgb(), from.PaintColor.ToArgb());
        Assert.Equal(0.4f, from.Opacity);
        Assert.Equal(Color.Blue.ToArgb(), to.PaintColor.ToArgb());
    }

    [Fact]
    public void LinearBrushInterpolationIncludesGeometryStopsAndDiscreteSpreadMode()
    {
        LinearGradientBrush from = Linear(
            Color.Red,
            Color.Blue,
            new PointF(0f, 0f),
            new PointF(1f, 0f),
            GradientSpreadMode.Pad);
        LinearGradientBrush to = Linear(
            Color.Green,
            Color.White,
            new PointF(0.2f, 0.4f),
            new PointF(0.8f, 1f),
            GradientSpreadMode.Reflect);
        IAnimationInterpolator<MfnBrush> interpolator = AnimationInterpolators.CreateBrushInterpolator();

        var middle = Assert.IsType<LinearGradientBrush>(interpolator.Interpolate(from, to, 0.5f));

        Assert.Equal(new PointF(0.1f, 0.2f), middle.Start);
        Assert.Equal(new PointF(0.9f, 0.5f), middle.End);
        Assert.Equal(GradientSpreadMode.Pad, middle.SpreadMode);
        Assert.Equal(2, middle.GradientStops.Count);
        Assert.Equal(0f, from.GradientStops[0].Offset);

        var final = Assert.IsType<LinearGradientBrush>(interpolator.Interpolate(from, to, 1f));
        Assert.Equal(GradientSpreadMode.Reflect, final.SpreadMode);
        Assert.Equal(to.GradientStops[0].PaintColor.ToArgb(), final.GradientStops[0].PaintColor.ToArgb());
    }

    [Fact]
    public void RadialAndSweepBrushInterpolationIncludesTypeSpecificGeometry()
    {
        var radialFrom = new RadialGradientBrush
        {
            CenterPoint = new PointF(0.2f, 0.4f),
            GradientOrigin = new PointF(0.1f, 0.3f),
            Radius = 0.4f
        };
        radialFrom.GradientStops.Add(new GradientStop(Color.Red, 0f));
        var radialTo = new RadialGradientBrush
        {
            CenterPoint = new PointF(0.8f, 0.6f),
            GradientOrigin = new PointF(0.5f, 0.7f),
            Radius = 1f
        };
        radialTo.GradientStops.Add(new GradientStop(Color.Blue, 1f));

        var radial = Assert.IsType<RadialGradientBrush>(
            AnimationInterpolators.CreateBrushInterpolator().Interpolate(radialFrom, radialTo, 0.5f));

        Assert.Equal(new PointF(0.5f, 0.5f), radial.CenterPoint);
        Assert.Equal(new PointF(0.3f, 0.5f), radial.GradientOrigin);
        Assert.Equal(0.7f, radial.Radius, 3);

        var sweepFrom = new SweepGradientBrush
        {
            CenterPoint = new PointF(0f, 0f),
            StartAngle = -90f,
            EndAngle = 90f
        };
        sweepFrom.GradientStops.Add(new GradientStop(Color.Black, 0f));
        var sweepTo = new SweepGradientBrush
        {
            CenterPoint = new PointF(1f, 1f),
            StartAngle = 90f,
            EndAngle = 270f
        };
        sweepTo.GradientStops.Add(new GradientStop(Color.White, 1f));

        var sweep = Assert.IsType<SweepGradientBrush>(
            AnimationInterpolators.CreateBrushInterpolator().Interpolate(sweepFrom, sweepTo, 0.5f));

        Assert.Equal(new PointF(0.5f, 0.5f), sweep.CenterPoint);
        Assert.Equal(0f, sweep.StartAngle);
        Assert.Equal(180f, sweep.EndAngle);
    }

    [Fact]
    public void BrushAnimationRejectsMismatchedTypesAndGradientStructuresBeforeScheduling()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var solid = new SolidColorBrush(Color.Red);
        var gradient = Linear(Color.Red, Color.Blue, new PointF(), new PointF(1f, 1f), GradientSpreadMode.Pad);
        var extraStopGradient = Linear(Color.Red, Color.Blue, new PointF(), new PointF(1f, 1f), GradientSpreadMode.Pad);
        extraStopGradient.GradientStops.Add(new GradientStop(Color.White, 0.5f));

        Assert.Throws<ArgumentException>(() =>
            solid.AnimateTo(gradient, TimeSpan.FromMilliseconds(100), scheduler: harness.Scheduler));
        Assert.Throws<ArgumentException>(() =>
            gradient.AnimateTo(extraStopGradient, TimeSpan.FromMilliseconds(100), scheduler: harness.Scheduler));
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    private static LinearGradientBrush Linear(
        Color startColor,
        Color endColor,
        PointF start,
        PointF end,
        GradientSpreadMode spreadMode)
    {
        var brush = new LinearGradientBrush
        {
            Start = start,
            End = end,
            SpreadMode = spreadMode
        };
        brush.GradientStops.AddRange([
            new GradientStop(startColor, 0f),
            new GradientStop(endColor, 1f)
        ]);
        return brush;
    }
}
