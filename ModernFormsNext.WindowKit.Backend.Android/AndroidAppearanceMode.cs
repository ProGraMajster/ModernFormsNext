namespace ModernFormsNext.WindowKit.Backend.Android;

// Android Configuration.uiMode combines the type and night-mode flags.
internal static class AndroidAppearanceMode
{
    internal static PlatformColorScheme GetColorScheme(int? uiMode)
        => (uiMode & 0x30) switch
        {
            0x10 => PlatformColorScheme.Light,
            0x20 => PlatformColorScheme.Dark,
            _ => PlatformColorScheme.Unknown
        };
}
