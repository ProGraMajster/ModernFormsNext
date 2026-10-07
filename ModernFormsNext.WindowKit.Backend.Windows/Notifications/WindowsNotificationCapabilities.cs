using ModernFormsNext.Notifications;

namespace ModernFormsNext.WindowKit.Backend.Windows.Notifications;

/// <summary>Windows native features, separate from portable notification capabilities.</summary>
[Flags]
public enum WindowsSystemNotificationFeatures
{
    /// <summary>No Windows extensions.</summary>
    None = 0,
    /// <summary>Native InlineImages support, after OS/API probing.</summary>
    InlineImages = 1 << 0,
    /// <summary>Native HeroImages support, after OS/API probing.</summary>
    HeroImages = 1 << 1,
    /// <summary>Native AppLogo support, after OS/API probing.</summary>
    AppLogo = 1 << 2,
    /// <summary>Native LoopingAudio support, after OS/API probing.</summary>
    LoopingAudio = 1 << 3,
    /// <summary>Native Headers support, after OS/API probing.</summary>
    Headers = 1 << 4,
    /// <summary>Native UrgentScenario support, after OS/API probing.</summary>
    UrgentScenario = 1 << 5,
    /// <summary>Native ButtonStyles support, after OS/API probing.</summary>
    ButtonStyles = 1 << 6,
    /// <summary>Native ContextMenuActions support, after OS/API probing.</summary>
    ContextMenuActions = 1 << 7,
    /// <summary>Native Scenarios support, after OS/API probing.</summary>
    Scenarios = 1 << 8,
    /// <summary>Native Attribution support, after OS/API probing.</summary>
    Attribution = 1 << 9,
    /// <summary>Native SuppressPopup support, after OS/API probing.</summary>
    SuppressPopup = 1 << 10,
    /// <summary>Native Priority support, after OS/API probing.</summary>
    Priority = 1 << 11,
    /// <summary>Native ButtonTooltips support, after OS/API probing.</summary>
    ButtonTooltips = 1 << 12,
    /// <summary>Native RawXml support, after OS/API probing.</summary>
    RawXml = 1 << 13,
    /// <summary>Native RebootExpiration support, after OS/API probing.</summary>
    RebootExpiration = 1 << 14,
    /// <summary>Native Mirroring support, after OS/API probing.</summary>
    Mirroring = 1 << 15,
    /// <summary>Native ExpandableContent support, after OS/API probing.</summary>
    ExpandableContent = 1 << 16,
}

/// <summary>Windows-specific capabilities reported by the selected provider.</summary>
/// <param name="Features">Probed Windows features; independent of the current notification setting.</param>
public sealed record WindowsSystemNotificationCapabilities(WindowsSystemNotificationFeatures Features) : SystemNotificationPlatformCapabilities
{
    /// <summary>Tests whether all requested native extensions are available.</summary>
    public bool Supports(WindowsSystemNotificationFeatures features) => (Features & features) == features;
}

/// <summary>Opaque native history identity scoped to the service instance that returned it.</summary>
/// <param name="Owner">Provider instance token; references from other instances are rejected.</param>
/// <param name="Tag">Native Windows tag.</param>
/// <param name="Group">Native Windows group.</param>
public sealed record WindowsSystemNotificationReference(Guid Owner, string Tag, string Group) : SystemNotificationReference;

/// <summary>Maps equivalent Windows provider setting names without linking either optional projection.</summary>
public static class WindowsNotificationAccess
{
    /// <summary>Returns current access from AppNotificationSetting or NotificationSetting; never requests consent.</summary>
    public static SystemNotificationAccess FromSetting(string setting) => new(setting switch
    {
        "Enabled" => SystemNotificationAvailability.Ready,
        "DisabledForApplication" or "DisabledForUser" => SystemNotificationAvailability.DisabledByUser,
        "DisabledByGroupPolicy" or "DisabledByManifest" => SystemNotificationAvailability.Restricted,
        "Unsupported" => SystemNotificationAvailability.Unavailable,
        _ => SystemNotificationAvailability.Unknown
    }, Reason: setting);
}
