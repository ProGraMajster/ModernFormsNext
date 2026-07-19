using System.Drawing;
using System.Numerics;
using ModernFormsNext.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext.Animations;

internal sealed class BrushAnimationPlan
{
    private readonly BrushSnapshot from;
    private readonly BrushSnapshot to;

    private BrushAnimationPlan(BrushSnapshot from, BrushSnapshot to, MfnBrush destination)
    {
        this.from = from;
        this.to = to;
        Destination = destination;
    }

    public MfnBrush Destination { get; }

    public static BrushAnimationPlan Create(MfnBrush from, MfnBrush to, MfnBrush destination)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(destination);

        if (from.GetType() != to.GetType() || from.GetType() != destination.GetType())
            throw new ArgumentException("Brush animations require matching concrete source, target, and destination types.", nameof(to));

        BrushSnapshot fromSnapshot = BrushSnapshot.Capture(from);
        BrushSnapshot toSnapshot = BrushSnapshot.Capture(to);
        ValidateCompatibility(fromSnapshot, toSnapshot);
        return new BrushAnimationPlan(fromSnapshot, toSnapshot, destination);
    }

    public static MfnBrush CloneSupportedBrush(MfnBrush source)
    {
        ArgumentNullException.ThrowIfNull(source);
        MfnBrush clone = source switch
        {
            SolidColorBrush solid when solid.GetType() == typeof(SolidColorBrush) => new SolidColorBrush(solid.PaintColor),
            LinearGradientBrush linear when linear.GetType() == typeof(LinearGradientBrush) => CloneLinear(linear),
            RadialGradientBrush radial when radial.GetType() == typeof(RadialGradientBrush) => CloneRadial(radial),
            SweepGradientBrush sweep when sweep.GetType() == typeof(SweepGradientBrush) => CloneSweep(sweep),
            _ => throw new NotSupportedException(
                $"Brush animation does not support the concrete type '{source.GetType().FullName}'.")
        };

        clone.Opacity = source.Opacity;
        clone.Transform = source.Transform;
        return clone;
    }

    public void Apply(float progress)
    {
        if (!float.IsFinite(progress))
            throw new ArgumentOutOfRangeException(nameof(progress), progress, "Brush animation progress must be finite.");

        Destination.Opacity = Math.Clamp(AnimationInterpolators.Float.Interpolate(from.Opacity, to.Opacity, progress), 0f, 1f);
        Destination.Transform = AnimationInterpolators.Matrix3x2.Interpolate(from.Transform, to.Transform, progress);

        switch (from, to, Destination)
        {
            case (SolidSnapshot start, SolidSnapshot end, SolidColorBrush destination):
                destination.PaintColor = AnimationInterpolators.Color.Interpolate(start.Color, end.Color, progress);
                break;
            case (LinearSnapshot start, LinearSnapshot end, LinearGradientBrush destination):
                destination.Start = AnimationInterpolators.PointF.Interpolate(start.Start, end.Start, progress);
                destination.End = AnimationInterpolators.PointF.Interpolate(start.End, end.End, progress);
                ApplyGradient(start, end, destination, progress);
                break;
            case (RadialSnapshot start, RadialSnapshot end, RadialGradientBrush destination):
                destination.CenterPoint = AnimationInterpolators.PointF.Interpolate(start.Center, end.Center, progress);
                destination.GradientOrigin = AnimationInterpolators.PointF.Interpolate(start.Origin, end.Origin, progress);
                destination.Radius = Math.Max(0f, AnimationInterpolators.Float.Interpolate(start.Radius, end.Radius, progress));
                ApplyGradient(start, end, destination, progress);
                break;
            case (SweepSnapshot start, SweepSnapshot end, SweepGradientBrush destination):
                destination.CenterPoint = AnimationInterpolators.PointF.Interpolate(start.Center, end.Center, progress);
                destination.StartAngle = AnimationInterpolators.Float.Interpolate(start.StartAngle, end.StartAngle, progress);
                destination.EndAngle = AnimationInterpolators.Float.Interpolate(start.EndAngle, end.EndAngle, progress);
                ApplyGradient(start, end, destination, progress);
                break;
            default:
                throw new InvalidOperationException("The brush animation plan no longer matches its destination.");
        }
    }

    private static LinearGradientBrush CloneLinear(LinearGradientBrush source)
    {
        var clone = new LinearGradientBrush
        {
            Start = source.Start,
            End = source.End,
            SpreadMode = source.SpreadMode
        };
        CopyStops(source, clone);
        return clone;
    }

    private static RadialGradientBrush CloneRadial(RadialGradientBrush source)
    {
        var clone = new RadialGradientBrush
        {
            CenterPoint = source.CenterPoint,
            GradientOrigin = source.GradientOrigin,
            Radius = source.Radius,
            SpreadMode = source.SpreadMode
        };
        CopyStops(source, clone);
        return clone;
    }

    private static SweepGradientBrush CloneSweep(SweepGradientBrush source)
    {
        var clone = new SweepGradientBrush
        {
            CenterPoint = source.CenterPoint,
            StartAngle = source.StartAngle,
            EndAngle = source.EndAngle,
            SpreadMode = source.SpreadMode
        };
        CopyStops(source, clone);
        return clone;
    }

    private static void CopyStops(GradientBrush source, GradientBrush destination)
    {
        foreach (GradientStop stop in source.GradientStops)
            destination.GradientStops.Add(new GradientStop(stop.PaintColor, stop.Offset));
    }

    private static void ValidateCompatibility(BrushSnapshot from, BrushSnapshot to)
    {
        if (from.GetType() != to.GetType())
            throw new ArgumentException("Source and target brush snapshots are incompatible.", nameof(to));

        if (from is GradientSnapshot fromGradient && to is GradientSnapshot toGradient &&
            fromGradient.Stops.Length != toGradient.Stops.Length)
        {
            throw new ArgumentException("Gradient brush animations require the same number of stops.", nameof(to));
        }
    }

    private static void ApplyGradient(
        GradientSnapshot from,
        GradientSnapshot to,
        GradientBrush destination,
        float progress)
    {
        destination.SpreadMode = progress >= 1f ? to.SpreadMode : from.SpreadMode;
        for (int index = 0; index < from.Stops.Length; index++)
        {
            StopSnapshot start = from.Stops[index];
            StopSnapshot end = to.Stops[index];
            GradientStop stop = destination.GradientStops[index];
            stop.PaintColor = AnimationInterpolators.Color.Interpolate(start.Color, end.Color, progress);
            stop.Offset = Math.Clamp(
                AnimationInterpolators.Float.Interpolate(start.Offset, end.Offset, progress),
                0f,
                1f);
        }
    }

    private abstract record BrushSnapshot(float Opacity, Matrix3x2 Transform)
    {
        public static BrushSnapshot Capture(MfnBrush brush)
            => brush switch
            {
                SolidColorBrush solid when solid.GetType() == typeof(SolidColorBrush) =>
                    new SolidSnapshot(solid.Opacity, solid.Transform, solid.PaintColor),
                LinearGradientBrush linear when linear.GetType() == typeof(LinearGradientBrush) =>
                    new LinearSnapshot(
                        linear.Opacity,
                        linear.Transform,
                        CaptureStops(linear),
                        linear.SpreadMode,
                        linear.Start,
                        linear.End),
                RadialGradientBrush radial when radial.GetType() == typeof(RadialGradientBrush) =>
                    new RadialSnapshot(
                        radial.Opacity,
                        radial.Transform,
                        CaptureStops(radial),
                        radial.SpreadMode,
                        radial.CenterPoint,
                        radial.GradientOrigin,
                        radial.Radius),
                SweepGradientBrush sweep when sweep.GetType() == typeof(SweepGradientBrush) =>
                    new SweepSnapshot(
                        sweep.Opacity,
                        sweep.Transform,
                        CaptureStops(sweep),
                        sweep.SpreadMode,
                        sweep.CenterPoint,
                        sweep.StartAngle,
                        sweep.EndAngle),
                _ => throw new NotSupportedException(
                    $"Brush animation does not support the concrete type '{brush.GetType().FullName}'.")
            };

        private static StopSnapshot[] CaptureStops(GradientBrush brush)
        {
            var result = new StopSnapshot[brush.GradientStops.Count];
            for (int index = 0; index < result.Length; index++)
            {
                GradientStop stop = brush.GradientStops[index];
                result[index] = new StopSnapshot(stop.PaintColor, stop.Offset);
            }
            return result;
        }
    }

    private sealed record SolidSnapshot(float Opacity, Matrix3x2 Transform, Color Color)
        : BrushSnapshot(Opacity, Transform);

    private abstract record GradientSnapshot(
        float Opacity,
        Matrix3x2 Transform,
        StopSnapshot[] Stops,
        GradientSpreadMode SpreadMode)
        : BrushSnapshot(Opacity, Transform);

    private sealed record LinearSnapshot(
        float Opacity,
        Matrix3x2 Transform,
        StopSnapshot[] Stops,
        GradientSpreadMode SpreadMode,
        PointF Start,
        PointF End)
        : GradientSnapshot(Opacity, Transform, Stops, SpreadMode);

    private sealed record RadialSnapshot(
        float Opacity,
        Matrix3x2 Transform,
        StopSnapshot[] Stops,
        GradientSpreadMode SpreadMode,
        PointF Center,
        PointF Origin,
        float Radius)
        : GradientSnapshot(Opacity, Transform, Stops, SpreadMode);

    private sealed record SweepSnapshot(
        float Opacity,
        Matrix3x2 Transform,
        StopSnapshot[] Stops,
        GradientSpreadMode SpreadMode,
        PointF Center,
        float StartAngle,
        float EndAngle)
        : GradientSnapshot(Opacity, Transform, Stops, SpreadMode);

    private readonly record struct StopSnapshot(Color Color, float Offset);
}
