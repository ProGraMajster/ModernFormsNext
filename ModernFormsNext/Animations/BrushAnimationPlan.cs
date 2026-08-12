using System.Drawing;
using System.Numerics;
using ModernFormsNext.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext.Animations;

/// <summary>
/// Captures one compatible brush transition and mutates a single animation-local destination.
/// </summary>
/// <remarks>
/// Planning performs all structural work, including gradient-stop normalization. Applying a frame
/// does not inspect authored collections, invoke user code, or allocate replacement stop arrays.
/// </remarks>
internal sealed class BrushAnimationPlan
{
    private readonly MfnBrush source;
    private readonly MfnBrush target;
    private readonly BrushSnapshot from;
    private readonly BrushSnapshot to;

    private BrushAnimationPlan(
        MfnBrush source,
        MfnBrush target,
        BrushSnapshot from,
        BrushSnapshot to,
        MfnBrush destination)
    {
        this.source = source;
        this.target = target;
        this.from = from;
        this.to = to;
        Destination = destination;
    }

    public MfnBrush Destination { get; }

    /// <summary>
    /// Creates an in-place plan for a destination whose concrete type and gradient structure are
    /// already stable.
    /// </summary>
    public static BrushAnimationPlan Create(MfnBrush from, MfnBrush to, MfnBrush destination)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(destination);

        if (from.GetType() != to.GetType() || from.GetType() != destination.GetType())
        {
            throw new ArgumentException(
                "In-place brush animations require matching concrete source, target, and destination types.",
                nameof(to));
        }

        if (!TryCapture(from, out BrushSnapshot fromSnapshot) ||
            !TryCapture(to, out BrushSnapshot toSnapshot) ||
            !TryCapture(destination, out BrushSnapshot destinationSnapshot))
        {
            throw new NotSupportedException(
                $"Brush animation does not support the concrete type '{from.GetType().FullName}'.");
        }

        if (fromSnapshot is GradientSnapshot fromGradient &&
            toSnapshot is GradientSnapshot toGradient &&
            (fromGradient.Stops.Length != toGradient.Stops.Length ||
             fromGradient.Stops.Length != ((GradientSnapshot)destinationSnapshot).Stops.Length))
        {
            throw new ArgumentException(
                "In-place gradient animations require the same number of source, target, and destination stops.",
                nameof(to));
        }

        return new BrushAnimationPlan(from, to, fromSnapshot, toSnapshot, destination);
    }

    /// <summary>
    /// Attempts to create a local plan that may normalize gradient structures or promote a solid
    /// color to the geometry of the other endpoint.
    /// </summary>
    public static bool TryCreateLocal(
        MfnBrush from,
        MfnBrush to,
        out BrushAnimationPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        plan = null;
        if (!TryCapture(from, out BrushSnapshot fromSnapshot) ||
            !TryCapture(to, out BrushSnapshot toSnapshot) ||
            !TryPrepareLocalSnapshots(ref fromSnapshot, ref toSnapshot))
        {
            return false;
        }

        MfnBrush destination = CreateBrush(fromSnapshot);
        plan = new BrushAnimationPlan(from, to, fromSnapshot, toSnapshot, destination);
        return true;
    }

    public static BrushAnimationPlan CreateLocal(MfnBrush from, MfnBrush to)
        => TryCreateLocal(from, to, out BrushAnimationPlan? plan)
            ? plan!
            : throw new ArgumentException(
                $"Brush types '{from.GetType().FullName}' and '{to.GetType().FullName}' do not have a safe interpolation path.",
                nameof(to));

    /// <summary>
    /// Returns an exact endpoint outside the open interval and the reusable working brush within
    /// it. This is used by value-style transitions that can replace a brush reference.
    /// </summary>
    public MfnBrush Interpolate(float progress)
    {
        ValidateProgress(progress);
        if (progress <= 0f)
            return source;
        if (progress >= 1f)
            return target;

        ApplyCore(progress);
        return Destination;
    }

    /// <summary>
    /// Applies a frame to the fixed destination used by explicit in-place brush animation.
    /// </summary>
    public void Apply(float progress)
    {
        ValidateProgress(progress);
        if (progress <= 0f)
        {
            ApplySnapshot(from, Destination);
            return;
        }

        if (progress >= 1f)
        {
            ApplySnapshot(to, Destination);
            return;
        }

        ApplyCore(progress);
    }

    private void ApplyCore(float progress)
    {
        Destination.Opacity = Math.Clamp(
            AnimationInterpolators.Float.Interpolate(from.Opacity, to.Opacity, progress),
            0f,
            1f);
        Destination.Transform = AnimationInterpolators.Matrix3x2.Interpolate(
            from.Transform,
            to.Transform,
            progress);

        switch (from, to, Destination)
        {
            case (SolidSnapshot start, SolidSnapshot end, SolidColorBrush destination):
                destination.PaintColor = AnimationInterpolators.Color.Interpolate(
                    start.Color,
                    end.Color,
                    progress);
                break;
            case (LinearSnapshot start, LinearSnapshot end, LinearGradientBrush destination):
                destination.Start = AnimationInterpolators.PointF.Interpolate(start.Start, end.Start, progress);
                destination.End = AnimationInterpolators.PointF.Interpolate(start.End, end.End, progress);
                ApplyGradient(start, end, destination, progress);
                break;
            case (RadialSnapshot start, RadialSnapshot end, RadialGradientBrush destination):
                destination.CenterPoint = AnimationInterpolators.PointF.Interpolate(start.Center, end.Center, progress);
                destination.GradientOrigin = AnimationInterpolators.PointF.Interpolate(start.Origin, end.Origin, progress);
                destination.Radius = Math.Max(
                    0f,
                    AnimationInterpolators.Float.Interpolate(start.Radius, end.Radius, progress));
                ApplyGradient(start, end, destination, progress);
                break;
            case (SweepSnapshot start, SweepSnapshot end, SweepGradientBrush destination):
                destination.CenterPoint = AnimationInterpolators.PointF.Interpolate(start.Center, end.Center, progress);
                destination.StartAngle = AnimationInterpolators.Float.Interpolate(
                    start.StartAngle,
                    end.StartAngle,
                    progress);
                destination.EndAngle = AnimationInterpolators.Float.Interpolate(
                    start.EndAngle,
                    end.EndAngle,
                    progress);
                ApplyGradient(start, end, destination, progress);
                break;
            default:
                throw new InvalidOperationException("The brush animation plan no longer matches its destination.");
        }
    }

    private static bool TryPrepareLocalSnapshots(
        ref BrushSnapshot from,
        ref BrushSnapshot to)
    {
        if (from.GetType() == to.GetType())
        {
            if (from is not GradientSnapshot fromGradient || to is not GradientSnapshot toGradient)
                return true;

            return TryNormalizeGradientPair(ref from, ref to, fromGradient, toGradient);
        }

        if (from is SolidSnapshot fromSolid && to is GradientSnapshot toGradientSnapshot)
        {
            if (toGradientSnapshot.Stops.Length == 0)
                return false;
            from = PromoteSolid(fromSolid, toGradientSnapshot);
            return true;
        }

        if (from is GradientSnapshot fromGradientSnapshot && to is SolidSnapshot toSolid)
        {
            if (fromGradientSnapshot.Stops.Length == 0)
                return false;
            to = PromoteSolid(toSolid, fromGradientSnapshot);
            return true;
        }

        // Geometry has no unambiguous morph between linear, radial, and sweep gradients.
        return false;
    }

    private static bool TryNormalizeGradientPair(
        ref BrushSnapshot from,
        ref BrushSnapshot to,
        GradientSnapshot fromGradient,
        GradientSnapshot toGradient)
    {
        if (fromGradient.Stops.Length == toGradient.Stops.Length)
            return true;

        // An empty gradient means no paint. Treating it as transparent would change the authored
        // compositing contract, so only two equally empty gradients are compatible.
        if (fromGradient.Stops.Length == 0 || toGradient.Stops.Length == 0)
            return false;

        float[] canonicalOffsets = BuildCanonicalOffsets(
            fromGradient.Stops,
            toGradient.Stops);
        from = WithStops(fromGradient, ResampleStops(fromGradient.Stops, canonicalOffsets));
        to = WithStops(toGradient, ResampleStops(toGradient.Stops, canonicalOffsets));
        return true;
    }

    private static GradientSnapshot PromoteSolid(
        SolidSnapshot solid,
        GradientSnapshot geometry)
    {
        var stops = new StopSnapshot[geometry.Stops.Length];
        for (int index = 0; index < stops.Length; index++)
            stops[index] = new StopSnapshot(solid.Color, geometry.Stops[index].Offset);

        return geometry switch
        {
            LinearSnapshot linear => new LinearSnapshot(
                solid.Opacity,
                solid.Transform,
                stops,
                linear.SpreadMode,
                linear.Start,
                linear.End),
            RadialSnapshot radial => new RadialSnapshot(
                solid.Opacity,
                solid.Transform,
                stops,
                radial.SpreadMode,
                radial.Center,
                radial.Origin,
                radial.Radius),
            SweepSnapshot sweep => new SweepSnapshot(
                solid.Opacity,
                solid.Transform,
                stops,
                sweep.SpreadMode,
                sweep.Center,
                sweep.StartAngle,
                sweep.EndAngle),
            _ => throw new InvalidOperationException("Unknown gradient snapshot type.")
        };
    }

    private static float[] BuildCanonicalOffsets(
        StopSnapshot[] first,
        StopSnapshot[] second)
    {
        int count = CountCanonicalOffsets(first, second);
        var result = new float[count];
        int firstIndex = 0;
        int secondIndex = 0;
        int resultIndex = 0;

        while (firstIndex < first.Length || secondIndex < second.Length)
        {
            float offset = secondIndex >= second.Length ||
                firstIndex < first.Length && first[firstIndex].Offset < second[secondIndex].Offset
                    ? first[firstIndex].Offset
                    : second[secondIndex].Offset;
            int firstCount = CountOffset(first, ref firstIndex, offset);
            int secondCount = CountOffset(second, ref secondIndex, offset);
            int multiplicity = Math.Max(firstCount, secondCount);
            for (int occurrence = 0; occurrence < multiplicity; occurrence++)
                result[resultIndex++] = offset;
        }

        return result;
    }

    private static int CountCanonicalOffsets(StopSnapshot[] first, StopSnapshot[] second)
    {
        int firstIndex = 0;
        int secondIndex = 0;
        int count = 0;
        while (firstIndex < first.Length || secondIndex < second.Length)
        {
            float offset = secondIndex >= second.Length ||
                firstIndex < first.Length && first[firstIndex].Offset < second[secondIndex].Offset
                    ? first[firstIndex].Offset
                    : second[secondIndex].Offset;
            int firstCount = CountOffset(first, ref firstIndex, offset);
            int secondCount = CountOffset(second, ref secondIndex, offset);
            count += Math.Max(firstCount, secondCount);
        }

        return count;
    }

    private static int CountOffset(StopSnapshot[] stops, ref int index, float offset)
    {
        int start = index;
        while (index < stops.Length && stops[index].Offset.Equals(offset))
            index++;
        return index - start;
    }

    private static StopSnapshot[] ResampleStops(
        StopSnapshot[] sourceStops,
        float[] canonicalOffsets)
    {
        var result = new StopSnapshot[canonicalOffsets.Length];
        int sourceIndex = 0;
        int resultIndex = 0;

        while (resultIndex < canonicalOffsets.Length)
        {
            float offset = canonicalOffsets[resultIndex];
            int resultStart = resultIndex;
            while (resultIndex < canonicalOffsets.Length && canonicalOffsets[resultIndex].Equals(offset))
                resultIndex++;
            int resultCount = resultIndex - resultStart;

            while (sourceIndex < sourceStops.Length && sourceStops[sourceIndex].Offset < offset)
                sourceIndex++;
            int exactStart = sourceIndex;
            while (sourceIndex < sourceStops.Length && sourceStops[sourceIndex].Offset.Equals(offset))
                sourceIndex++;
            int exactCount = sourceIndex - exactStart;

            if (exactCount > 0)
            {
                for (int occurrence = 0; occurrence < resultCount; occurrence++)
                {
                    result[resultStart + occurrence] = new StopSnapshot(
                        SampleDuplicateColor(
                            sourceStops,
                            exactStart,
                            exactCount,
                            occurrence,
                            resultCount),
                        offset);
                }
                continue;
            }

            Color sampled = SampleColorBetweenStops(sourceStops, sourceIndex, offset);
            for (int occurrence = 0; occurrence < resultCount; occurrence++)
                result[resultStart + occurrence] = new StopSnapshot(sampled, offset);
        }

        return result;
    }

    private static Color SampleDuplicateColor(
        StopSnapshot[] stops,
        int start,
        int sourceCount,
        int destinationOccurrence,
        int destinationCount)
    {
        if (sourceCount == 1 || destinationCount == 1)
            return stops[start].Color;

        float position = destinationOccurrence * (sourceCount - 1f) / (destinationCount - 1f);
        int lower = (int)MathF.Floor(position);
        int upper = Math.Min(lower + 1, sourceCount - 1);
        float progress = position - lower;
        return AnimationInterpolators.Color.Interpolate(
            stops[start + lower].Color,
            stops[start + upper].Color,
            progress);
    }

    private static Color SampleColorBetweenStops(
        StopSnapshot[] stops,
        int rightIndex,
        float offset)
    {
        if (rightIndex <= 0)
            return stops[0].Color;
        if (rightIndex >= stops.Length)
            return stops[^1].Color;

        StopSnapshot left = stops[rightIndex - 1];
        StopSnapshot right = stops[rightIndex];
        float distance = right.Offset - left.Offset;
        if (distance <= 0f)
            return right.Color;
        float progress = (offset - left.Offset) / distance;
        return AnimationInterpolators.Color.Interpolate(left.Color, right.Color, progress);
    }

    private static GradientSnapshot WithStops(
        GradientSnapshot snapshot,
        StopSnapshot[] stops)
        => snapshot switch
        {
            LinearSnapshot linear => linear with { Stops = stops },
            RadialSnapshot radial => radial with { Stops = stops },
            SweepSnapshot sweep => sweep with { Stops = stops },
            _ => throw new InvalidOperationException("Unknown gradient snapshot type.")
        };

    private static MfnBrush CreateBrush(BrushSnapshot snapshot)
    {
        MfnBrush brush = snapshot switch
        {
            SolidSnapshot solid => new SolidColorBrush(solid.Color),
            LinearSnapshot linear => new LinearGradientBrush
            {
                Start = linear.Start,
                End = linear.End,
                SpreadMode = linear.SpreadMode
            },
            RadialSnapshot radial => new RadialGradientBrush
            {
                CenterPoint = radial.Center,
                GradientOrigin = radial.Origin,
                Radius = radial.Radius,
                SpreadMode = radial.SpreadMode
            },
            SweepSnapshot sweep => new SweepGradientBrush
            {
                CenterPoint = sweep.Center,
                StartAngle = sweep.StartAngle,
                EndAngle = sweep.EndAngle,
                SpreadMode = sweep.SpreadMode
            },
            _ => throw new InvalidOperationException("Unknown brush snapshot type.")
        };

        brush.Opacity = snapshot.Opacity;
        brush.Transform = snapshot.Transform;
        if (snapshot is GradientSnapshot gradient && brush is GradientBrush destinationGradient)
        {
            for (int index = 0; index < gradient.Stops.Length; index++)
            {
                StopSnapshot stop = gradient.Stops[index];
                destinationGradient.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));
            }
        }

        return brush;
    }

    private static void ApplySnapshot(BrushSnapshot snapshot, MfnBrush destination)
    {
        destination.Opacity = snapshot.Opacity;
        destination.Transform = snapshot.Transform;

        switch (snapshot, destination)
        {
            case (SolidSnapshot solid, SolidColorBrush solidDestination):
                solidDestination.PaintColor = solid.Color;
                break;
            case (LinearSnapshot linear, LinearGradientBrush linearDestination):
                linearDestination.Start = linear.Start;
                linearDestination.End = linear.End;
                ApplyGradientSnapshot(linear, linearDestination);
                break;
            case (RadialSnapshot radial, RadialGradientBrush radialDestination):
                radialDestination.CenterPoint = radial.Center;
                radialDestination.GradientOrigin = radial.Origin;
                radialDestination.Radius = radial.Radius;
                ApplyGradientSnapshot(radial, radialDestination);
                break;
            case (SweepSnapshot sweep, SweepGradientBrush sweepDestination):
                sweepDestination.CenterPoint = sweep.Center;
                sweepDestination.StartAngle = sweep.StartAngle;
                sweepDestination.EndAngle = sweep.EndAngle;
                ApplyGradientSnapshot(sweep, sweepDestination);
                break;
            default:
                throw new InvalidOperationException("The brush snapshot does not match its destination.");
        }
    }

    private static void ApplyGradientSnapshot(
        GradientSnapshot snapshot,
        GradientBrush destination)
    {
        if (destination.GradientStops.Count != snapshot.Stops.Length)
            throw new InvalidOperationException("The destination gradient structure changed during animation.");

        destination.SpreadMode = snapshot.SpreadMode;
        for (int index = 0; index < snapshot.Stops.Length; index++)
        {
            StopSnapshot source = snapshot.Stops[index];
            GradientStop targetStop = destination.GradientStops[index];
            targetStop.PaintColor = source.Color;
            targetStop.Offset = source.Offset;
        }
    }

    private static void ApplyGradient(
        GradientSnapshot from,
        GradientSnapshot to,
        GradientBrush destination,
        float progress)
    {
        if (destination.GradientStops.Count != from.Stops.Length || from.Stops.Length != to.Stops.Length)
            throw new InvalidOperationException("The destination gradient structure changed during animation.");

        // Spread mode is categorical. Keep the source mode for intermediate frames; the exact
        // target snapshot is applied by Apply when progress reaches one.
        destination.SpreadMode = from.SpreadMode;
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

    private static bool TryCapture(MfnBrush brush, out BrushSnapshot snapshot)
    {
        snapshot = brush switch
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
            _ => null!
        };
        return snapshot is not null;
    }

    private static StopSnapshot[] CaptureStops(GradientBrush brush)
    {
        GradientStop[] ordered = brush.GetOrderedStops();
        var result = new StopSnapshot[ordered.Length];
        for (int index = 0; index < result.Length; index++)
        {
            GradientStop stop = ordered[index];
            result[index] = new StopSnapshot(stop.PaintColor, stop.Offset);
        }

        return result;
    }

    private static void ValidateProgress(float progress)
    {
        if (!float.IsFinite(progress))
        {
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                progress,
                "Brush animation progress must be finite.");
        }
    }

    private abstract record BrushSnapshot(float Opacity, Matrix3x2 Transform);

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
