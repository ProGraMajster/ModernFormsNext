namespace ModernFormsNext.Notifications;

/// <summary>Current notification authorization/availability, independent of backend abilities.</summary>
public enum SystemNotificationAvailability
{
    /// <summary>Submission is currently allowed; OS delivery and presentation are still not guaranteed.</summary>
    Ready,
    /// <summary>A supported platform has not yet received a permission decision.</summary>
    PermissionRequired,
    /// <summary>The user denied permission; another prompt may not be permitted.</summary>
    PermissionDenied,
    /// <summary>The user disabled notification delivery in system settings.</summary>
    DisabledByUser,
    /// <summary>Policy or platform restrictions prevent delivery.</summary>
    Restricted,
    /// <summary>No usable registered service/native transport is available.</summary>
    Unavailable,
    /// <summary>The native transport cannot reliably query current delivery authorization.</summary>
    Unknown
}

/// <summary>Presentation permissions a native platform can distinguish; these are not capability flags.</summary>
[Flags]
public enum SystemNotificationPresentation
{
    /// <summary>No requested/granted presentation.</summary>
    None = 0,
    /// <summary>Visible alert presentation.</summary>
    Alert = 1,
    /// <summary>Sound presentation.</summary>
    Sound = 2,
    /// <summary>Application badge presentation, where a platform extension uses it.</summary>
    Badge = 4
}

/// <summary>Explicit permission request preferences. Merely constructing these never prompts.</summary>
public sealed record SystemNotificationPermissionRequest
{
    /// <summary>Gets presentation permissions to request; the platform may grant only a subset.</summary>
    public SystemNotificationPresentation Presentation { get; init; } = SystemNotificationPresentation.Alert | SystemNotificationPresentation.Sound;
    /// <summary>Gets typed authorization extensions, for example a future Apple provisional request.</summary>
    public SystemNotificationOptions PlatformOptions { get; init; } = SystemNotificationOptions.Empty;
}

/// <summary>Current access snapshot. Query again after settings changes; it is never a cached permission promise.</summary>
/// <param name="Availability">Current native state.</param>
/// <param name="CanRequestPermission">Whether an explicit permission request can currently make progress.</param>
/// <param name="Reason">Optional diagnostic explanation.</param>
public sealed record SystemNotificationAccess(SystemNotificationAvailability Availability, bool CanRequestPermission = false, string? Reason = null)
{
    /// <summary>Gets known allowed presentations; null means the backend cannot report these granular settings.</summary>
    public SystemNotificationPresentation? AllowedPresentation { get; init; }
    /// <summary>Gets an unavailable access snapshot for an unregistered service.</summary>
    public static SystemNotificationAccess Unavailable { get; } = new(SystemNotificationAvailability.Unavailable);
}
