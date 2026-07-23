namespace ModernFormsNext.WindowKit.Backend;

/// <summary>Identifies a platform light/dark preference without referencing the UI framework.</summary>
public enum PlatformColorScheme
{
    /// <summary>The platform preference is unavailable.</summary>
    Unknown,
    /// <summary>The platform prefers a light color scheme.</summary>
    Light,
    /// <summary>The platform prefers a dark color scheme.</summary>
    Dark
}

/// <summary>
/// Provides optional platform theme and motion preferences to shared framework services.
/// </summary>
/// <remarks>
/// Implementations must be side-effect free and safe when no window or activity is active.
/// </remarks>
public interface IPlatformThemeSettings
{
    /// <summary>Gets the current platform light/dark preference.</summary>
    PlatformColorScheme GetPreferredVariant();

    /// <summary>Gets whether the platform requests reduced motion, or <see langword="null"/> when unavailable.</summary>
    bool? GetReducedMotion();
}
