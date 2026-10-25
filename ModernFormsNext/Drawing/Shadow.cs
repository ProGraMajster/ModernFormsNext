using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>Describes one immutable, platform-neutral outer shadow of a control's border box.</summary>
/// <remarks>
/// Distances are logical pixels, scaled once by the control's DPI. BlurRadius is the Gaussian
/// standard deviation, not a CSS blur diameter. The shared Skia compositor draws the shadow
/// behind the control and clips it to every ancestor's client area. Shadows do not affect layout,
/// preferred size, hit testing or focus. Leave enough parent space to display the outer footprint.
/// Values own no native resources and can be shared across controls and dynamic resources.
/// </remarks>
/// <example><code>
/// button.Shadow = new Shadow(Color.FromArgb(96, Color.Black), blurRadius: 5, offsetY: 3);
/// </code></example>
public sealed class Shadow : IEquatable<Shadow>
{
    /// <summary>Gets the maximum supported Gaussian standard deviation in logical pixels.</summary>
    public const float MaximumBlurRadius = 128;
    /// <summary>Gets the maximum absolute spread in logical pixels.</summary>
    public const float MaximumSpread = 128;
    /// <summary>Gets the maximum absolute offset along either axis in logical pixels.</summary>
    public const float MaximumOffset = 2048;

    /// <summary>Creates a bounded outer shadow.</summary>
    /// <param name="color">The shadow color including its opacity.</param>
    /// <param name="blurRadius">Finite Gaussian standard deviation from zero through 128 logical pixels.</param>
    /// <param name="offsetX">Finite horizontal offset, at most 2048 logical pixels in magnitude.</param>
    /// <param name="offsetY">Finite vertical offset, at most 2048 logical pixels in magnitude.</param>
    /// <param name="spread">Finite border-box expansion, from -128 through 128 logical pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">A distance is non-finite or outside its bound.</exception>
    public Shadow(Color color, float blurRadius = 0, float offsetX = 0, float offsetY = 0, float spread = 0)
    {
        ValidateDistance(blurRadius, 0, MaximumBlurRadius, nameof(blurRadius));
        ValidateDistance(offsetX, -MaximumOffset, MaximumOffset, nameof(offsetX));
        ValidateDistance(offsetY, -MaximumOffset, MaximumOffset, nameof(offsetY));
        ValidateDistance(spread, -MaximumSpread, MaximumSpread, nameof(spread));
        Color = color;
        BlurRadius = blurRadius;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Spread = spread;
    }

    /// <summary>Gets the color including its alpha channel.</summary>
    public Color Color { get; }
    /// <summary>Gets the Gaussian standard deviation in logical pixels.</summary>
    public float BlurRadius { get; }
    /// <summary>Gets the horizontal offset in logical pixels.</summary>
    public float OffsetX { get; }
    /// <summary>Gets the vertical offset in logical pixels.</summary>
    public float OffsetY { get; }
    /// <summary>Gets the border-box expansion in logical pixels, which may be negative.</summary>
    public float Spread { get; }

    /// <inheritdoc/>
    public bool Equals(Shadow? other) => other is not null && Color.ToArgb() == other.Color.ToArgb() &&
        BlurRadius == other.BlurRadius && OffsetX == other.OffsetX && OffsetY == other.OffsetY && Spread == other.Spread;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Shadow other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Color.ToArgb(), BlurRadius, OffsetX, OffsetY, Spread);

    private static void ValidateDistance(float value, float minimum, float maximum, string parameter)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameter, value, "The shadow distance exceeds its finite logical-pixel range.");
    }
}
