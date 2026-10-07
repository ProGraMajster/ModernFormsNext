namespace ModernFormsNext.Notifications;

/// <summary>Semantic content for an OS-owned notification, independent of native layout or registration.</summary>
/// <remarks>The service copies collections before asynchronous work. Configure records before submission;
/// do not mutate source collections during the call. Platform extensions must honor their snapshot contract.</remarks>
public sealed record SystemNotification
{
    /// <summary>Gets the application-defined logical identity. Persist it to replace or remove content after restart.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    /// <summary>Gets a logical group. The ordinal pair Id/Group identifies content; native identifier limits do not apply here.</summary>
    public string Group { get; init; } = string.Empty;
    /// <summary>Gets the plain-text title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets the plain-text body.</summary>
    public string Message { get; init; } = string.Empty;
    /// <summary>Gets additional text. The backend may combine or limit blocks with diagnostics.</summary>
    public IReadOnlyList<SystemNotificationText> Text { get; init; } = [];
    /// <summary>Gets semantic images. The application owns source files and their lifetime.</summary>
    public IReadOnlyList<SystemNotificationImage> Images { get; init; } = [];
    /// <summary>Gets application or URI actions; no executable delegate crosses a native boundary.</summary>
    public IReadOnlyList<SystemNotificationAction> Actions { get; init; } = [];
    /// <summary>Gets inputs referenced by action InputIds. Native input presentation depends on capabilities.</summary>
    public IReadOnlyList<SystemNotificationInput> Inputs { get; init; } = [];
    /// <summary>Gets optional progress. A backend owns any native counters or update protocol.</summary>
    public SystemNotificationProgress? Progress { get; init; }
    /// <summary>Gets opaque application data returned on body activation. Never treat it as a command to execute.</summary>
    public string ActivationData { get; init; } = string.Empty;
    /// <summary>Gets an optional displayed event time, not a delivery schedule.</summary>
    public DateTimeOffset? Timestamp { get; init; }
    /// <summary>Gets an absolute expiration time. Null leaves native expiry policy unchanged; no timer is emulated.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
    /// <summary>Gets an attention preference, subject to OS policy, authorization and user settings.</summary>
    public SystemNotificationUrgency Urgency { get; init; }
    /// <summary>Gets optional sender metadata. Rich conversation/person presentation requires backend support.</summary>
    public SystemNotificationPerson? Sender { get; init; }
    /// <summary>Gets immutable typed settings for any number of platforms; a provider reads only its own types.</summary>
    public SystemNotificationOptions PlatformOptions { get; init; } = SystemNotificationOptions.Empty;
    /// <summary>Gets whether unsupported optional content is removed with warnings or rejected.</summary>
    public bool AllowDegradation { get; init; } = true;
}

/// <summary>A platform-independent request for relative attention, never an entitlement or delivery guarantee.</summary>
public enum SystemNotificationUrgency
{
    /// <summary>Use ordinary native behavior.</summary>
    Default,
    /// <summary>Prefer low interruption where supported.</summary>
    Low,
    /// <summary>Prefer higher attention where supported; does not bypass quiet mode or require a critical alert.</summary>
    High
}

/// <summary>One additional plain-text block, with optional BCP-47 language metadata.</summary>
/// <param name="Content">Text content; no native markup syntax is implied.</param>
/// <param name="Language">Optional BCP-47 language.</param>
public sealed record SystemNotificationText(string Content, string? Language = null)
{
    /// <summary>Gets platform presentation hints, such as Windows line limits.</summary>
    public SystemNotificationOptions PlatformOptions { get; init; } = SystemNotificationOptions.Empty;
}

/// <summary>Describes what an image represents rather than its native screen position or pixel size.</summary>
public enum SystemNotificationImageRole
{
    /// <summary>Image content that accompanies the message, such as a photo or artwork.</summary>
    Content,
    /// <summary>A preview of the item described by the notification.</summary>
    Thumbnail,
    /// <summary>An identifying logo or symbol for the notification's source.</summary>
    Identity,
    /// <summary>A person's portrait. Native crop and placement remain platform choices.</summary>
    Portrait
}

/// <summary>An image asset reference. Native providers validate supported URI schemes and actual formats.</summary>
/// <param name="Source">Absolute local path, URI or platform-resolved resource reference.</param>
/// <param name="Role">Semantic role; does not promise a native layout.</param>
/// <param name="AlternateText">Optional description for assistive technology.</param>
public sealed record SystemNotificationImage(string Source, SystemNotificationImageRole Role = SystemNotificationImageRole.Content,
    string? AlternateText = null)
{
    /// <summary>Gets native image presentation settings without requiring a platform dependency in this type.</summary>
    public SystemNotificationOptions PlatformOptions { get; init; } = SystemNotificationOptions.Empty;
}

/// <summary>Semantic sender metadata, independent of Android Person or Apple communication intents.</summary>
/// <param name="Id">Stable application sender identity.</param>
/// <param name="DisplayName">User-visible name.</param>
/// <param name="Image">Optional portrait.</param>
public sealed record SystemNotificationPerson(string Id, string DisplayName, SystemNotificationImage? Image = null);

/// <summary>Describes an action's intent without prescribing COM, PendingIntent or background-task mechanics.</summary>
public enum SystemNotificationActivationType
{
    /// <summary>Deliver a response to the application. Its lifecycle and native options determine foreground presentation.</summary>
    Application,
    /// <summary>Ask the OS to open TargetUri through a registered handler, where supported.</summary>
    OpenUri
}

/// <summary>One logical action, including references to response inputs.</summary>
/// <param name="Title">Visible action label.</param>
/// <param name="ActivationData">Opaque application payload.</param>
public sealed record SystemNotificationAction(string Title, string ActivationData)
{
    /// <summary>Gets a stable action identifier, returned independently of its payload.</summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>Gets application-response or open-URI intent.</summary>
    public SystemNotificationActivationType ActivationType { get; init; }
    /// <summary>Gets the absolute target URI required for OpenUri; unused for application responses.</summary>
    public Uri? TargetUri { get; init; }
    /// <summary>Gets required response input IDs. The OS may also return other declared inputs; native association limits belong to the backend.</summary>
    public IReadOnlyList<string> InputIds { get; init; } = [];
    /// <summary>Gets platform action mechanics/presentation, such as Windows snooze or Apple foreground options.</summary>
    public SystemNotificationOptions PlatformOptions { get; init; } = SystemNotificationOptions.Empty;
}

/// <summary>One text input, or a selection input when Choices is nonempty.</summary>
/// <param name="Id">Unique logical identifier used in action associations and activation data.</param>
public sealed record SystemNotificationInput(string Id)
{
    /// <summary>Gets the input label.</summary>
    public string? Title { get; init; }
    /// <summary>Gets the placeholder for a text input.</summary>
    public string? Placeholder { get; init; }
    /// <summary>Gets initial text or an existing choice identifier.</summary>
    public string? DefaultValue { get; init; }
    /// <summary>Gets selection choices. Limits are reported by the selected backend, not fixed to Windows.</summary>
    public IReadOnlyList<SystemNotificationSelection> Choices { get; init; } = [];
    /// <summary>Gets platform input extensions, for example future Android free-form suggestions or MIME input.</summary>
    public SystemNotificationOptions PlatformOptions { get; init; } = SystemNotificationOptions.Empty;
}

/// <summary>One selection option.</summary>
/// <param name="Id">Value delivered on activation.</param>
/// <param name="Title">Visible localized label.</param>
public sealed record SystemNotificationSelection(string Id, string Title);

/// <summary>Semantic progress. Null Value represents indeterminate work; no native sequence counter is exposed.</summary>
/// <remarks>Updates are serialized by the service. Concurrent callers must order their own application state
/// before submission; the last submitted update is not necessarily the greatest percentage.</remarks>
public sealed record SystemNotificationProgress
{
    /// <summary>Gets a finite fraction in [0,1], or null for indeterminate progress.</summary>
    public double? Value { get; init; }
    /// <summary>Gets status text. Empty is valid in common; a backend may require or supply a native label.</summary>
    public string Status { get; init; } = string.Empty;
    /// <summary>Gets a progress title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets text replacing the percentage, where supported.</summary>
    public string ValueText { get; init; } = string.Empty;
}
