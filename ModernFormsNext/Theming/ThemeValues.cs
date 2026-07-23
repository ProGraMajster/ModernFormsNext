using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

/// <summary>
/// Describes a named typography role in a theme.
/// </summary>
/// <remarks>
/// Font family, size, bold, and italic styling can be mapped to the current renderer. Line height
/// and letter spacing are preserved as tokens for controls that support them; the base text
/// renderer does not yet apply those two values globally.
/// </remarks>
public sealed class ThemeTypography : IEquatable<ThemeTypography>
{
    /// <summary>
    /// Creates a typography value.
    /// </summary>
    /// <param name="fontFamily">The non-empty platform font family name.</param>
    /// <param name="size">The positive finite size in points.</param>
    /// <param name="style">The ModernFormsNext font style flags.</param>
    /// <param name="lineHeight">An optional positive finite line-height multiplier.</param>
    /// <param name="letterSpacing">Optional finite letter spacing in logical pixels.</param>
    public ThemeTypography(
        string fontFamily,
        float size,
        FontStyle style = FontStyle.Regular,
        float? lineHeight = null,
        float? letterSpacing = null)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
            throw new ArgumentException("The font family cannot be empty.", nameof(fontFamily));
        if (!float.IsFinite(size) || size <= 0f)
            throw new ArgumentOutOfRangeException(nameof(size), size, "The typography size must be finite and positive.");
        if ((style & ~(FontStyle.Bold | FontStyle.Italic | FontStyle.Underline | FontStyle.Strikeout)) != 0)
            throw new ArgumentOutOfRangeException(nameof(style), style, "The font style contains unsupported flags.");
        if (lineHeight is { } line && (!float.IsFinite(line) || line <= 0f))
            throw new ArgumentOutOfRangeException(nameof(lineHeight), lineHeight, "Line height must be finite and positive.");
        if (letterSpacing is { } spacing && !float.IsFinite(spacing))
            throw new ArgumentOutOfRangeException(nameof(letterSpacing), letterSpacing, "Letter spacing must be finite.");

        FontFamily = fontFamily;
        Size = size;
        Style = style;
        LineHeight = lineHeight;
        LetterSpacing = letterSpacing;
    }

    /// <summary>Gets the platform font family name.</summary>
    public string FontFamily { get; }

    /// <summary>Gets the font size in points.</summary>
    public float Size { get; }

    /// <summary>Gets the font style flags.</summary>
    public FontStyle Style { get; }

    /// <summary>Gets the optional line-height multiplier.</summary>
    public float? LineHeight { get; }

    /// <summary>Gets optional letter spacing in logical pixels.</summary>
    public float? LetterSpacing { get; }

    /// <summary>Creates a ModernFormsNext font for controls that consume the typography token.</summary>
    /// <returns>A new immutable font description.</returns>
    public Font ToFont() => new(FontFamily, Size, Style);

    /// <inheritdoc />
    public bool Equals(ThemeTypography? other)
        => other is not null &&
           string.Equals(FontFamily, other.FontFamily, StringComparison.Ordinal) &&
           Size.Equals(other.Size) && Style == other.Style &&
           LineHeight.Equals(other.LineHeight) && LetterSpacing.Equals(other.LetterSpacing);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ThemeTypography);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(FontFamily, Size, Style, LineHeight, LetterSpacing);
}

/// <summary>
/// Identifies a stable easing name supported by JSON themes.
/// </summary>
public enum ThemeEasing
{
    /// <summary>Linear progress.</summary>
    Linear,
    /// <summary>Slow start and accelerating finish.</summary>
    EaseIn,
    /// <summary>Fast start and decelerating finish.</summary>
    EaseOut,
    /// <summary>Slow start and finish.</summary>
    EaseInOut
}

/// <summary>
/// Describes a named animation token without storing executable delegates in theme JSON.
/// </summary>
public sealed class ThemeAnimationSettings : IEquatable<ThemeAnimationSettings>
{
    /// <summary>Creates an animation setting.</summary>
    /// <param name="duration">A non-negative duration.</param>
    /// <param name="easing">The allow-listed easing name.</param>
    /// <param name="enabled">Whether consumers should animate this role.</param>
    public ThemeAnimationSettings(TimeSpan duration, ThemeEasing easing = ThemeEasing.EaseInOut, bool enabled = true)
    {
        if (duration < TimeSpan.Zero || duration > ThemeSecurityLimits.MaximumAnimationDuration)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "The animation duration is outside the supported range.");
        if (!Enum.IsDefined(easing))
            throw new ArgumentOutOfRangeException(nameof(easing), easing, "The theme easing is not defined.");

        Duration = duration;
        Easing = easing;
        Enabled = enabled;
    }

    /// <summary>Gets the unscaled duration.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Gets the stable easing name.</summary>
    public ThemeEasing Easing { get; }

    /// <summary>Gets whether consumers should animate this role.</summary>
    public bool Enabled { get; }

    /// <inheritdoc />
    public bool Equals(ThemeAnimationSettings? other)
        => other is not null && Duration == other.Duration && Easing == other.Easing && Enabled == other.Enabled;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ThemeAnimationSettings);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Duration, Easing, Enabled);

    internal Func<float, float> GetEasing() => Easing switch
    {
        ThemeEasing.Linear => Easings.Linear,
        ThemeEasing.EaseIn => Easings.EaseIn,
        ThemeEasing.EaseOut => Easings.EaseOut,
        ThemeEasing.EaseInOut => Easings.EaseInOut,
        _ => Easings.Linear
    };
}

/// <summary>
/// Identifies the allow-listed value stored in a custom theme resource.
/// </summary>
public enum ThemeResourceKind
{
    /// <summary>A UTF-16 string.</summary>
    String,
    /// <summary>A Boolean value.</summary>
    Boolean,
    /// <summary>A 32-bit signed integer.</summary>
    Integer,
    /// <summary>A finite double-precision number.</summary>
    Number,
    /// <summary>A platform-neutral color.</summary>
    Color,
    /// <summary>An allow-listed ModernFormsNext brush.</summary>
    Brush,
    /// <summary>A ModernFormsNext padding value.</summary>
    Padding,
    /// <summary>A typography value.</summary>
    Typography,
    /// <summary>An animation setting.</summary>
    Animation
}

/// <summary>
/// Wraps a custom theme resource using a closed, JSON-safe type allow-list.
/// </summary>
/// <remarks>
/// The wrapper never accepts arbitrary CLR type names and never activates types through
/// reflection. Brush values are cloned on input and output to prevent authoring objects from
/// sharing mutable state with resolved or applied themes.
/// </remarks>
public sealed class ThemeResourceValue
{
    private readonly object value;

    private ThemeResourceValue(ThemeResourceKind kind, object value)
    {
        Kind = kind;
        this.value = ThemeValueCloner.CloneValue(value);
    }

    /// <summary>Gets the allow-listed value kind.</summary>
    public ThemeResourceKind Kind { get; }

    /// <summary>
    /// Gets an isolated value. Mutable brush values are cloned for the caller.
    /// </summary>
    public object Value => ThemeValueCloner.CloneValue(value);

    /// <summary>Creates a string resource.</summary>
    public static ThemeResourceValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(ThemeResourceKind.String, value);
    }

    /// <summary>Creates a Boolean resource.</summary>
    public static ThemeResourceValue FromBoolean(bool value) => new(ThemeResourceKind.Boolean, value);

    /// <summary>Creates a 32-bit integer resource.</summary>
    public static ThemeResourceValue FromInteger(int value) => new(ThemeResourceKind.Integer, value);

    /// <summary>Creates a finite numeric resource.</summary>
    public static ThemeResourceValue FromNumber(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Theme numbers must be finite.");
        return new(ThemeResourceKind.Number, value);
    }

    /// <summary>Creates a color resource.</summary>
    public static ThemeResourceValue FromColor(Color value) => new(ThemeResourceKind.Color, value);

    /// <summary>Creates an isolated brush resource.</summary>
    public static ThemeResourceValue FromBrush(MfnBrush value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(ThemeResourceKind.Brush, value);
    }

    /// <summary>Creates a padding resource.</summary>
    public static ThemeResourceValue FromPadding(Padding value) => new(ThemeResourceKind.Padding, value);

    /// <summary>Creates a typography resource.</summary>
    public static ThemeResourceValue FromTypography(ThemeTypography value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(ThemeResourceKind.Typography, value);
    }

    /// <summary>Creates an animation resource.</summary>
    public static ThemeResourceValue FromAnimation(ThemeAnimationSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(ThemeResourceKind.Animation, value);
    }

    internal object GetRawValue() => value;

    internal ThemeResourceValue Clone() => new(Kind, value);
}

/// <summary>
/// Defines security and complexity limits used by theme validation and JSON loading.
/// </summary>
public sealed class ThemeSecurityLimits
{
    /// <summary>The default maximum UTF-8 document size: 1 MiB.</summary>
    public const int DefaultMaximumDocumentBytes = 1024 * 1024;
    /// <summary>The default maximum JSON nesting depth.</summary>
    public const int DefaultMaximumJsonDepth = 64;
    /// <summary>The default maximum combined token count.</summary>
    public const int DefaultMaximumTokenCount = 4096;
    /// <summary>The default maximum gradient-stop count per brush.</summary>
    public const int DefaultMaximumGradientStops = 64;
    /// <summary>The default maximum metadata or resource string length.</summary>
    public const int DefaultMaximumStringLength = 512;
    /// <summary>The default maximum inheritance depth.</summary>
    public const int DefaultMaximumInheritanceDepth = 16;
    /// <summary>The maximum allow-listed animation duration.</summary>
    public static readonly TimeSpan MaximumAnimationDuration = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the maximum UTF-8 document size.</summary>
    public int MaximumDocumentBytes { get; set; } = DefaultMaximumDocumentBytes;
    /// <summary>Gets or sets the maximum JSON nesting depth.</summary>
    public int MaximumJsonDepth { get; set; } = DefaultMaximumJsonDepth;
    /// <summary>Gets or sets the maximum combined token count.</summary>
    public int MaximumTokenCount { get; set; } = DefaultMaximumTokenCount;
    /// <summary>Gets or sets the maximum gradient stops per brush.</summary>
    public int MaximumGradientStops { get; set; } = DefaultMaximumGradientStops;
    /// <summary>Gets or sets the maximum string length.</summary>
    public int MaximumStringLength { get; set; } = DefaultMaximumStringLength;
    /// <summary>Gets or sets the maximum inheritance depth.</summary>
    public int MaximumInheritanceDepth { get; set; } = DefaultMaximumInheritanceDepth;

    internal ThemeSecurityLimits CloneValidated()
    {
        if (MaximumDocumentBytes <= 0 || MaximumJsonDepth <= 0 || MaximumTokenCount <= 0 ||
            MaximumGradientStops <= 0 || MaximumStringLength <= 0 || MaximumInheritanceDepth <= 0)
        {
            throw new InvalidOperationException("Every theme security limit must be positive.");
        }

        return (ThemeSecurityLimits)MemberwiseClone();
    }
}
