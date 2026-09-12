using System.Drawing;
//using ModernFormsNext.WindowKit.Media;

namespace ModernFormsNext.WindowKit.Platform;

/// <summary>
/// System theme variant or mode.
/// </summary>
public enum PlatformThemeVariant
{
    /// <summary>
    /// The platform is using a light theme.
    /// </summary>
    Light,

    /// <summary>
    /// The platform is using a dark theme.
    /// </summary>
    Dark
}

/// <summary>
/// System high contrast preference.
/// </summary>
public enum ColorContrastPreference
{
    /// <summary>
    /// The platform has no explicit high-contrast preference.
    /// </summary>
    NoPreference,

    /// <summary>
    /// The platform prefers high-contrast colors.
    /// </summary>
    High
}

/// <summary>
/// Information about current system color values, including information about dark mode and accent colors.
/// </summary>
public record PlatformColorValues
{
    private static Color DefaultAccent => Color.FromArgb (255, 0, 120, 215);
    private Color _accentColor2, _accentColor3;

    /// <summary>
    /// System theme variant or mode.
    /// </summary>
    public PlatformThemeVariant ThemeVariant { get; init; }

    /// <summary>
    /// System high contrast preference.
    /// </summary>
    public ColorContrastPreference ContrastPreference { get; init; }

    /// <summary>Gets the detected system content background, or null when no palette value is available.</summary>
    public Color? BackgroundColor { get; init; }

    /// <summary>Gets the detected system content foreground, or null when no palette value is available.</summary>
    public Color? ForegroundColor { get; init; }
    
    /// <summary>
    /// Primary system accent color.
    /// </summary>
    public Color AccentColor1 { get; init; }

    /// <summary>
    /// Secondary system accent color. On some platforms can return the same value as <see cref="AccentColor1"/>.
    /// </summary>
    public Color AccentColor2
    {
        get => _accentColor2 != default ? _accentColor2 : AccentColor1;
        init => _accentColor2 = value;
    }

    /// <summary>
    /// Tertiary system accent color. On some platforms can return the same value as <see cref="AccentColor1"/>.
    /// </summary>
    public Color AccentColor3
    {
        get => _accentColor3 != default ? _accentColor3 : AccentColor1;
        init => _accentColor3 = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformColorValues"/> class with the default accent color.
    /// </summary>
    public PlatformColorValues()
    {
        AccentColor1 = DefaultAccent;
    }

    // Copy known storage without invoking a derived record's virtual Clone, retaining its
    // payload, or normalizing optional accent backing fields (which would alter record equality).
    internal PlatformColorValues Detach() => new()
    {
        ThemeVariant = ThemeVariant, ContrastPreference = ContrastPreference,
        AccentColor1 = AccentColor1, _accentColor2 = _accentColor2, _accentColor3 = _accentColor3,
        BackgroundColor = BackgroundColor, ForegroundColor = ForegroundColor
    };
}
