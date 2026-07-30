using System.Runtime.InteropServices;
using Microsoft.Win32;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Windows;

/// <summary>Reads Windows application-theme and client-animation preferences on demand.</summary>
internal sealed class WindowsPlatformThemeSettings : IPlatformThemeSettings
{
    private const string PersonalizeKey =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private readonly WindowsPlatformAnimationSettings animationSettings;

    public WindowsPlatformThemeSettings(WindowsPlatformAnimationSettings animationSettings)
        => this.animationSettings = animationSettings ?? throw new ArgumentNullException(nameof(animationSettings));

    public PlatformColorScheme GetPreferredVariant()
    {
        if (!OperatingSystem.IsWindows())
            return PlatformColorScheme.Unknown;

        try
        {
            object? value = Registry.GetValue(PersonalizeKey, "AppsUseLightTheme", null);
            return value is int integer
                ? integer == 0 ? PlatformColorScheme.Dark : PlatformColorScheme.Light
                : PlatformColorScheme.Unknown;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return PlatformColorScheme.Unknown;
        }
    }

    public bool? GetReducedMotion()
    {
        PlatformAnimationSettingsSnapshot snapshot = animationSettings.Current;
        return snapshot.FallbackUsed ? null : snapshot.ReducedMotion;
    }
}
