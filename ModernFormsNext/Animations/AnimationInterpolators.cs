using System.Drawing;
using System.Numerics;

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
}
