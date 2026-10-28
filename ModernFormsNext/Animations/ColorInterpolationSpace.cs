namespace ModernFormsNext.Animations;

/// <summary>Identifies the RGB encoding used to interpolate color channels over time.</summary>
/// <remarks>
/// Alpha retains the existing separate linear byte interpolation. These values do not change
/// the renderer's compositing, color management or spatial gradient interpolation.
/// </remarks>
public enum ColorInterpolationSpace
{
    /// <summary>Interpolates encoded sRGB bytes, preserving the existing animation behavior.</summary>
    Srgb,
    /// <summary>Decodes sRGB to linear light, interpolates RGB, then encodes the result to sRGB.</summary>
    LinearSrgb
}
