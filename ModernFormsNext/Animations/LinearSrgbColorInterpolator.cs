using System.Drawing;

namespace ModernFormsNext.Animations;

/// <summary>Converts temporal RGB channels through the bounded sRGB transfer function.</summary>
internal sealed class LinearSrgbColorInterpolator : IAnimationInterpolator<Color>
{
    private static readonly double[] decodedChannels = CreateDecodedChannels();

    public Color Interpolate(Color from, Color to, float progress)
    {
        if (!float.IsFinite(progress))
            throw new ArgumentOutOfRangeException(nameof(progress), progress, "Interpolation progress must be finite.");

        // Alpha keeps the existing straight-channel contract. Clamp in Double before the
        // integer conversion so even a very large finite easing result cannot overflow.
        int alpha = (int)Math.Round(Math.Clamp(from.A + (((double)to.A - from.A) * progress), 0d, 255d),
            MidpointRounding.AwayFromZero);
        return Color.FromArgb(alpha, Channel(from.R, to.R, progress),
            Channel(from.G, to.G, progress), Channel(from.B, to.B, progress));
    }

    private static int Channel(byte from, byte to, float progress)
    {
        double linear = Math.Clamp(decodedChannels[from] +
            ((decodedChannels[to] - decodedChannels[from]) * progress), 0d, 1d);
        // sRGB transfer functions (RGB conversion only, not CSS alpha interpolation):
        // https://www.w3.org/TR/css-color-4/#valdef-color-srgb
        double encoded = linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1d / 2.4) - 0.055;
        return Math.Clamp((int)Math.Round(encoded * 255d, MidpointRounding.AwayFromZero), 0, 255);
    }

    private static double[] CreateDecodedChannels()
    {
        // The fixed 256-entry table is shared by stateless interpolators. Its one-time cost
        // occurs outside warmed frame measurements; no color/array is allocated per frame.
        var result = new double[256];
        for (int index = 0; index < result.Length; index++)
        {
            double encoded = index / 255d;
            result[index] = encoded <= 0.04045 ? encoded / 12.92 : Math.Pow((encoded + 0.055) / 1.055, 2.4);
        }
        return result;
    }
}
