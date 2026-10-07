using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;

namespace ModernFormsNext.WindowKit.Backend.Windows.Notifications;

/// <summary>Native toast scenario; these values do not emulate a popup or override user settings.</summary>
public enum WindowsNotificationScenario
{
    /// <summary>Ordinary notification.</summary>
    Default,
    /// <summary>A reminder requiring user interaction.</summary>
    Reminder,
    /// <summary>An alarm, normally with looping audio.</summary>
    Alarm,
    /// <summary>An incoming call.</summary>
    IncomingCall,
    /// <summary>Urgent delivery, only after a successful runtime support query.</summary>
    Urgent
}

/// <summary>System-controlled toast duration, subject to accessibility and user settings.</summary>
public enum WindowsNotificationDuration
{
    /// <summary>Leave the OS default unchanged.</summary>
    Default,
    /// <summary>Request the native short duration.</summary>
    Short,
    /// <summary>Request the native long duration.</summary>
    Long
}

/// <summary>Optional style for a supported native action button.</summary>
public enum WindowsNotificationButtonStyle
{
    /// <summary>System default.</summary>
    Default,
    /// <summary>Native success styling.</summary>
    Success,
    /// <summary>Native critical styling.</summary>
    Critical
}

/// <summary>Optional Windows action configuration.</summary>
public sealed record WindowsNotificationActionOptions : SystemNotificationPlatformOptions
{
    /// <summary>Gets an optional native activation mechanism. Background tasks are unsupported by the desktop providers.</summary>
    public WindowsNotificationActivationType ActivationType { get; init; }
    /// <summary>Gets a local/remote/package icon URI. Visible buttons must consistently use icons or labels.</summary>
    public string? Icon { get; init; }
    /// <summary>Gets a tooltip for an icon-only action, when supported.</summary>
    public string? ToolTip { get; init; }
    /// <summary>Gets the native button style.</summary>
    public WindowsNotificationButtonStyle Style { get; init; }
    /// <summary>Gets whether the action belongs in the notification context menu.</summary>
    public bool ContextMenu { get; init; }
    /// <summary>Gets the input identifier associated with a reply or snooze action.</summary>
    public string? InputId { get; init; }
    /// <summary>Gets whether Windows should leave the notification pending an application update.</summary>
    public bool PendingUpdate { get; init; }
    /// <summary>Gets an optional protocol target package family name.</summary>
    public string? TargetApplicationPfn { get; init; }
}

/// <summary>Windows-specific toast content and delivery configuration.</summary>
/// <remarks>Immutable; the service may remove unsupported optional properties with warnings.</remarks>
public sealed record WindowsSystemNotificationOptions : SystemNotificationPlatformOptions
{
    /// <summary>Gets optional attribution text.</summary>
    public string? Attribution { get; init; }
    /// <summary>Gets an optional BCP-47 language for the visual and attribution.</summary>
    public string? Language { get; init; }
    /// <summary>Gets an optional notification center header.</summary>
    public WindowsNotificationHeader? Header { get; init; }
    /// <summary>Gets the native scenario.</summary>
    public WindowsNotificationScenario Scenario { get; init; }
    /// <summary>Gets the native duration.</summary>
    public WindowsNotificationDuration Duration { get; init; }
    /// <summary>Gets whether sound is muted. Cannot be combined with Audio or LoopAudio.</summary>
    public bool Silent { get; init; }
    /// <summary>Gets an ms-winsoundevent URI, or supported ms-appx/ms-resource custom audio URI.</summary>
    public string? Audio { get; init; }
    /// <summary>Gets native audio looping. Requires Long duration and a looping-capable source.</summary>
    public bool LoopAudio { get; init; }
    /// <summary>Gets whether the OS should add to history without showing a banner.</summary>
    public bool SuppressPopup { get; init; }
    /// <summary>Gets the native ExpiresOnReboot value; null preserves the OS default.</summary>
    /// <remarks>Passed directly to the native property after capability probing. No application timer is used.</remarks>
    public bool? ExpiresOnReboot { get; init; }
    /// <summary>Gets whether native cross-device mirroring is allowed; null preserves OS policy.</summary>
    public bool? AllowMirroring { get; init; }
    /// <summary>Gets an optional cross-device correlation ID (at most 64 characters); requires Mirroring capability.</summary>
    public string? RemoteId { get; init; }
    /// <summary>Gets whether native high priority is requested; this does not bypass user settings.</summary>
    public bool HighPriority { get; init; }
    /// <summary>Gets optional advanced toast XML. DTDs/entities and oversized payloads are rejected.</summary>
    /// <remarks>Uses the complete native XML schema for adaptive groups and binding options. RequiredFeatures
    /// must declare features beyond basic content. This escape hatch is rejected on legacy templates/balloons.
    /// Native launch/action arguments in raw XML are delivered unchanged, with no inferred notification ID.</remarks>
    public string? RawXml { get; init; }
    /// <summary>Gets explicit feature requirements for RawXml; missing features reject the request.</summary>
    public WindowsSystemNotificationFeatures RequiredFeatures { get; init; }
    /// <summary>Gets semantic requirements for raw XML, such as progress, actions or inputs, in addition to Windows extensions.</summary>
    public SystemNotificationFeatures RequiredCommonFeatures { get; init; } = SystemNotificationFeatures.Basic;
    /// <summary>Gets legacy balloon behavior; ignored by toast backends with a diagnostic.</summary>
    public WindowsBalloonOptions? Balloon { get; init; }
}

/// <summary>Native notification-center grouping header.</summary>
/// <param name="Id">Group header identity.</param>
/// <param name="Title">Visible localized title.</param>
/// <param name="Arguments">Application activation payload.</param>
public sealed record WindowsNotificationHeader(string Id, string Title, string Arguments);

/// <summary>Shell balloon icon kind.</summary>
public enum WindowsBalloonIcon
{
    /// <summary>No icon.</summary>
    None,
    /// <summary>System information icon.</summary>
    Information,
    /// <summary>System warning icon.</summary>
    Warning,
    /// <summary>System error icon.</summary>
    Error,
    /// <summary>Use a caller-supplied native icon.</summary>
    Custom
}

/// <summary>Native Shell balloon options. No advanced toast content is simulated.</summary>
public sealed record WindowsBalloonOptions
{
    /// <summary>Gets the native icon kind.</summary>
    public WindowsBalloonIcon Icon { get; init; } = WindowsBalloonIcon.Information;
    /// <summary>Gets a borrowed HICON, required for Custom. The caller keeps it alive through ShowAsync.</summary>
    public nint CustomIcon { get; init; }
    /// <summary>Gets whether to use the native large icon.</summary>
    public bool LargeIcon { get; init; }
    /// <summary>Gets whether Shell quiet time is respected (default true).</summary>
    public bool RespectQuietTime { get; init; } = true;
    /// <summary>Gets whether Shell should discard the balloon if it cannot show it immediately.</summary>
    public bool Realtime { get; init; }
}

/// <summary>Native notification backend preference.</summary>
public enum WindowsNotificationBackendKind
{
    /// <summary>Try App SDK, classic WinRT, then an explicitly allowed Shell fallback.</summary>
    Automatic,
    /// <summary>Require the optional App SDK provider.</summary>
    AppSdk,
    /// <summary>Require the optional classic WinRT provider.</summary>
    Classic,
    /// <summary>Use Shell balloon notifications.</summary>
    Shell,
    /// <summary>No backend is available.</summary>
    Unavailable
}

/// <summary>Explicit application registration policy. Configure on the UI thread during startup.</summary>
public sealed record WindowsNotificationRegistrationOptions
{
    /// <summary>Gets the preferred native backend.</summary>
    public WindowsNotificationBackendKind Backend { get; init; }
    /// <summary>Gets whether automatic selection may fall back to Shell balloons.</summary>
    public bool AllowShellFallback { get; init; } = true;
    /// <summary>Gets the AUMID installed by the application installer, required for classic unpackaged toast.</summary>
    public string? AppUserModelId { get; init; }
    /// <summary>Gets the COM activator CLSID installed with the AUMID shortcut/manifest and LocalServer32 entry.</summary>
    /// <remarks>The framework registers only the running COM class factory; it never writes classic installer registry keys.</remarks>
    public Guid? ClassicActivatorId { get; init; }
    /// <summary>Gets an optional App SDK display name for explicit unpackaged registration.</summary>
    public string? DisplayName { get; init; }
    /// <summary>Gets the optional App SDK branding icon URI paired with DisplayName.</summary>
    public Uri? DisplayIcon { get; init; }
}

/// <summary>Native Windows action mechanisms. Default maps the common action intent.</summary>
public enum WindowsNotificationActivationType
{
    /// <summary>Map the common Application or OpenUri intent.</summary>
    Default,
    /// <summary>Use foreground COM activation.</summary>
    Foreground,
    /// <summary>UWP background-task activation, unsupported by these desktop providers.</summary>
    Background,
    /// <summary>Open the common TargetUri through Windows protocol activation.</summary>
    Protocol,
    /// <summary>Ask Windows to dismiss the notification.</summary>
    Dismiss,
    /// <summary>Ask Windows to snooze an alarm/reminder.</summary>
    Snooze
}

/// <summary>Exact Windows adaptive image placement, independent of the common semantic role.</summary>
public enum WindowsNotificationImagePlacement
{
    /// <summary>Map the common image role.</summary>
    Default,
    /// <summary>Display within notification content.</summary>
    Inline,
    /// <summary>Display a wide hero image.</summary>
    Hero,
    /// <summary>Override the application logo.</summary>
    AppLogo,
    /// <summary>Override the logo with circle cropping.</summary>
    Avatar
}

/// <summary>Optional Windows image layout and URI behavior.</summary>
public sealed record WindowsSystemNotificationImageOptions : SystemNotificationPlatformOptions
{
    /// <summary>Gets exact native placement; Default uses the semantic common role.</summary>
    public WindowsNotificationImagePlacement Placement { get; init; }
    /// <summary>Gets whether Windows appends scale/contrast/language query parameters to the image URI.</summary>
    public bool AddImageQuery { get; init; }
}

/// <summary>Optional Windows adaptive text layout.</summary>
public sealed record WindowsSystemNotificationTextOptions : SystemNotificationPlatformOptions
{
    /// <summary>Gets the maximum number of lines, from one through four (at most two for the first text block).</summary>
    public int? MaxLines { get; init; }
}
