using System.Runtime.InteropServices;
using Microsoft.Win32;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Windows;

/// <summary>Reads Windows application-theme and client-animation preferences on demand.</summary>
internal sealed class WindowsPlatformThemeSettings : IPlatformThemeSettings
{
    private const string PersonalizeKey =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const uint SpiGetClientAreaAnimation = 0x1042;

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
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            return SystemParametersInfo(SpiGetClientAreaAnimation, 0, out bool enabled, 0)
                ? !enabled
                : null;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        [MarshalAs(UnmanagedType.Bool)] out bool value,
        uint updateFlags);
}
