using System.Drawing;
using System.Numerics;
using ModernFormsNext.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext.Animations;

/// <summary>
/// Provides allocation-free interpolators for common platform-neutral UI values.
/// </summary>
/// <remarks>
/// Numeric and geometric values use component-wise linear interpolation. Colors interpolate alpha,
/// red, green, and blue channels in their current sRGB byte representation. Matrix interpolation is
/// component-wise and is suitable for ordinary UI transitions; decomposition into rotation, scale,
/// and translation is not performed.
/// </remarks>
public static class AnimationInterpolators
{
    /// <summary>Gets the single-precision floating-point interpolator.</summary>
    public static IAnimationInterpolator<float> Float { get; } =
        new DelegateInterpolator<float>(static (from, to, progress) => from + ((to - from) * progress));

    /// <summary>Gets the double-precision floating-point interpolator.</summary>
    public static IAnimationInterpolator<double> Double { get; } =
        new DelegateInterpolator<double>(static (from, to, progress) => from + ((to - from) * progress));

    /// <summary>
    /// Gets the integer interpolator, which rounds midpoint values away from zero and clamps
    /// overshoot to the <see cref="int"/> range.
    /// </summary>
    public static IAnimationInterpolator<int> Int32 { get; } =
        new DelegateInterpolator<int>(static (from, to, progress) =>
        {
            double value = from + (((double)to - from) * progress);
            if (value <= int.MinValue)
                return int.MinValue;
            if (value >= int.MaxValue)
                return int.MaxValue;
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        });

    /// <summary>Gets the component-wise <see cref="PointF"/> interpolator.</summary>
    public static IAnimationInterpolator<PointF> PointF { get; } =
        new DelegateInterpolator<PointF>(static (from, to, progress) => new PointF(
            Lerp(from.X, to.X, progress),
            Lerp(from.Y, to.Y, progress)));

    /// <summary>Gets the component-wise <see cref="SizeF"/> interpolator.</summary>
    public static IAnimationInterpolator<SizeF> SizeF { get; } =
        new DelegateInterpolator<SizeF>(static (from, to, progress) => new SizeF(
            Lerp(from.Width, to.Width, progress),
            Lerp(from.Height, to.Height, progress)));

    /// <summary>Gets the component-wise <see cref="RectangleF"/> interpolator.</summary>
    public static IAnimationInterpolator<RectangleF> RectangleF { get; } =
        new DelegateInterpolator<RectangleF>(static (from, to, progress) => new RectangleF(
            Lerp(from.X, to.X, progress),
            Lerp(from.Y, to.Y, progress),
            Lerp(from.Width, to.Width, progress),
            Lerp(from.Height, to.Height, progress)));

    /// <summary>
    /// Gets the alpha-aware <see cref="Color"/> interpolator.
    /// </summary>
    /// <remarks>Overshoot channel values are clamped to 0..255.</remarks>
    public static IAnimationInterpolator<Color> Color { get; } =
        new DelegateInterpolator<Color>(static (from, to, progress) => System.Drawing.Color.FromArgb(
            LerpChannel(from.A, to.A, progress),
            LerpChannel(from.R, to.R, progress),
            LerpChannel(from.G, to.G, progress),
            LerpChannel(from.B, to.B, progress)));

    /// <summary>Gets the component-wise platform-neutral <see cref="Matrix3x2"/> interpolator.</summary>
    public static IAnimationInterpolator<Matrix3x2> Matrix3x2 { get; } =
        new DelegateInterpolator<Matrix3x2>(static (from, to, progress) => new Matrix3x2(
            Lerp(from.M11, to.M11, progress),
            Lerp(from.M12, to.M12, progress),
            Lerp(from.M21, to.M21, progress),
            Lerp(from.M22, to.M22, progress),
            Lerp(from.M31, to.M31, progress),
            Lerp(from.M32, to.M32, progress)));

    /// <summary>
    /// Creates an animation-local interpolator that reuses one observable
    /// <see cref="GradientStop"/> result instance.
    /// </summary>
    /// <returns>A new interpolator intended for one scheduled animation.</returns>
    /// <remarks>
    /// The returned interpolator is not thread-safe and should not be shared between concurrent
    /// animations. It allocates its result once, then mutates its color and offset on the UI
    /// thread. Use <see cref="GradientStopAnimationExtensions.AnimateTo"/> to mutate an existing
    /// stop directly.
    /// </remarks>
    public static IAnimationInterpolator<GradientStop> CreateGradientStopInterpolator()
        => new ReusableGradientStopInterpolator();

    /// <summary>
    /// Creates an animation-local interpolator for compatible built-in brushes.
    /// </summary>
    /// <returns>A new stateful interpolator intended for one scheduled animation.</returns>
    /// <remarks>
    /// The interpolator creates one local working brush from the start value and mutates it on
    /// subsequent calls. It supports solid, linear, radial, and sweep brushes. Gradient types must
    /// match and contain the same number of stops. Source and target brushes are not mutated, which
    /// prevents a local transition from unexpectedly changing other consumers of a shared dynamic
    /// resource. Use <see cref="BrushAnimationExtensions.AnimateTo"/> for intentional in-place
    /// resource animation.
    /// </remarks>
    public static IAnimationInterpolator<MfnBrush> CreateBrushInterpolator()
        => new ReusableBrushInterpolator();

    private static float Lerp(float from, float to, float progress) => from + ((to - from) * progress);

    private static int LerpChannel(byte from, byte to, float progress)
        => Math.Clamp((int)MathF.Round(Lerp(from, to, progress), MidpointRounding.AwayFromZero), 0, 255);

    private sealed class DelegateInterpolator<T>(Func<T, T, float, T> interpolate) : IAnimationInterpolator<T>
    {
        public T Interpolate(T from, T to, float progress)
        {
            if (!float.IsFinite(progress))
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "Interpolation progress must be finite.");
            return interpolate(from, to, progress);
        }
    }

    private sealed class ReusableGradientStopInterpolator : IAnimationInterpolator<GradientStop>
    {
        private GradientStop? result;

        public GradientStop Interpolate(GradientStop from, GradientStop to, float progress)
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            if (!float.IsFinite(progress))
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "Interpolation progress must be finite.");

            result ??= new GradientStop(from.PaintColor, from.Offset);
            result.PaintColor = Color.Interpolate(from.PaintColor, to.PaintColor, progress);
            result.Offset = Math.Clamp(Float.Interpolate(from.Offset, to.Offset, progress), 0f, 1f);
            return result;
        }
    }

    private sealed class ReusableBrushInterpolator : IAnimationInterpolator<MfnBrush>
    {
        private BrushAnimationPlan? plan;
        private MfnBrush? fromBrush;
        private MfnBrush? toBrush;

        public MfnBrush Interpolate(MfnBrush from, MfnBrush to, float progress)
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            if (!float.IsFinite(progress))
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "Interpolation progress must be finite.");

            if (plan is null)
            {
                MfnBrush workingBrush = BrushAnimationPlan.CloneSupportedBrush(from);
                plan = BrushAnimationPlan.Create(from, to, workingBrush);
                fromBrush = from;
                toBrush = to;
            }
            else if (!ReferenceEquals(fromBrush, from) || !ReferenceEquals(toBrush, to))
            {
                throw new InvalidOperationException("A brush interpolator instance can be used for only one source and target pair.");
            }

            plan.Apply(progress);
            return plan.Destination;
        }
    }
}
