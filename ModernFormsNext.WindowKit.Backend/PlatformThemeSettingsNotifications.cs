namespace ModernFormsNext.WindowKit.Backend;

/// <summary>Extends platform theme settings with optional light and dark change notifications.</summary>
public interface IPlatformThemeSettingsNotifications : IPlatformThemeSettings
{
    /// <summary>Occurs when the current color scheme may have changed.</summary>
    /// <remarks>
    /// Subscribe and unsubscribe on the platform UI thread. Consumers should reread
    /// <see cref="IPlatformThemeSettings.GetPreferredVariant"/> on their UI dispatcher.
    /// </remarks>
    event EventHandler? PreferredVariantChanged;
}
