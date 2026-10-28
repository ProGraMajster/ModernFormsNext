namespace ModernFormsNext.Animations;

/// <summary>Configures an explicitly selected brush interpolation policy.</summary>
/// <remarks>
/// Configure on the UI thread before starting an animation. The planner captures these values;
/// changing this instance does not change a running animation. Defaults preserve the original
/// encoded-sRGB compatibility matrix and discrete no-paint fallback.
/// </remarks>
/// <example>
/// <code>
/// var interpolation = AnimationInterpolators.CreateBrushInterpolator(new BrushInterpolationOptions
/// {
///     ColorSpace = ColorInterpolationSpace.LinearSrgb,
///     EnableExtendedMorphing = true
/// });
/// </code>
/// </example>
public sealed class BrushInterpolationOptions
{
    private ColorInterpolationSpace colorSpace;

    /// <summary>Gets or sets temporal RGB interpolation. The default is encoded sRGB.</summary>
    /// <remarks>Alpha remains a separate linear channel; this does not select premultiplied interpolation.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined color space.</exception>
    public ColorInterpolationSpace ColorSpace
    {
        get => colorSpace;
        set
        {
            if (value is not (ColorInterpolationSpace.Srgb or ColorInterpolationSpace.LinearSrgb))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The color interpolation space is not defined.");
            colorSpace = value;
        }
    }

    /// <summary>Gets or sets whether the additional documented brush mappings are enabled.</summary>
    /// <remarks>
    /// The default is false. Opting in permits compatible glass-color transitions and fades
    /// between supported paint and explicit no-paint values. A null brush still uses the
    /// caller's discrete fallback because null can select a control's fallback color.
    /// Populated gradients of different kinds and unknown/derived brushes remain discrete.
    /// This changes presentation brushes and render invalidation, never layout or authored values.
    /// </remarks>
    public bool EnableExtendedMorphing { get; set; }

    internal BrushInterpolationOptions Clone()
        => new() { ColorSpace = ColorSpace, EnableExtendedMorphing = EnableExtendedMorphing };
}
