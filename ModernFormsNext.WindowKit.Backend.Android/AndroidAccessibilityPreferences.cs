using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Backend.Android;

// Pure normalization shared by the native provider and deterministic backend tests. A missing
// activity, unavailable API, or invalid native result stays unknown rather than implying normal.
internal static class AndroidAccessibilityPreferences
{
    internal static PlatformAccessibilityPreferences Create(bool hasHost, float? fontScale, float? contrast, bool dark)
    {
        if (!hasHost) return new();
        double? scale = fontScale is { } text && float.IsFinite(text) && text > 0 ? text : null;
        PlatformColorValues? colors = contrast is { } value && float.IsFinite(value) && value is >= -1 and <= 1
            ? new PlatformColorValues
            {
                ThemeVariant = dark ? PlatformThemeVariant.Dark : PlatformThemeVariant.Light,
                // Android's continuous preference is projected to the existing binary contract.
                // Positive levels request higher contrast; zero/negative do not request High.
                ContrastPreference = value > 0 ? ColorContrastPreference.High : ColorContrastPreference.NoPreference
            } : null;
        return new(colors, scale);
    }
}
