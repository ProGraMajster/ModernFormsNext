namespace ModernFormsNext.Notifications;

/// <summary>Operation outcome; acceptance is not proof that the OS displayed a banner.</summary>
public enum SystemNotificationStatus
{
    /// <summary>The native platform accepted the request.</summary>
    Accepted,
    /// <summary>The requested operation or content is not supported.</summary>
    Unsupported,
    /// <summary>The user disabled notifications.</summary>
    Disabled,
    /// <summary>Required application registration is missing.</summary>
    RegistrationRequired,
    /// <summary>The platform no longer has the referenced notification.</summary>
    NotFound,
    /// <summary>The request contains invalid data.</summary>
    Invalid,
    /// <summary>A native operation failed; ErrorCode may contain a platform error.</summary>
    Failed,
    /// <summary>An explicit permission request is required before submission.</summary>
    PermissionRequired,
    /// <summary>Notification permission has been denied.</summary>
    PermissionDenied,
    /// <summary>Policy or platform restrictions currently prevent submission.</summary>
    Restricted,
    /// <summary>No usable native notification service is currently available.</summary>
    Unavailable
}

/// <summary>A logical application identity, independent of native tags, integer IDs and transport instances.</summary>
/// <param name="Id">Application replacement identity, compared ordinally.</param>
/// <param name="Group">Logical group/partition, compared ordinally. Empty is the default group.</param>
public sealed record SystemNotificationKey(string Id, string Group = "");

/// <summary>Opaque backend-owned reference to an actual native entry, scoped to the application's native identity.</summary>
/// <remarks>Providers define immutable derived records. Do not parse, fabricate or interchange references
/// between providers. Native references can expire even when the logical application key remains valid.</remarks>
public abstract record SystemNotificationReference;

/// <summary>An entry returned by native history, not a local substitute for an OS entry.</summary>
/// <param name="Key">Logical key if recoverable; null for foreign or advanced raw native content.</param>
/// <param name="Reference">Backend reference permitting exact removal even without a known logical key.</param>
public sealed record SystemNotificationHistoryEntry(SystemNotificationKey? Key, SystemNotificationReference Reference);

/// <summary>A validation/degradation diagnostic without sensitive notification content.</summary>
/// <param name="Code">Stable diagnostic code.</param>
/// <param name="Message">Developer-facing explanation.</param>
public sealed record SystemNotificationWarning(string Code, string Message);

/// <summary>Immutable submission/operation result.</summary>
/// <param name="Status">Operation outcome.</param>
/// <param name="Key">Logical application identity, when known.</param>
/// <param name="Warnings">Removed or adjusted optional content.</param>
/// <param name="ErrorCode">Platform error code, if available; its interpretation belongs to that backend.</param>
public sealed record SystemNotificationResult(SystemNotificationStatus Status, SystemNotificationKey? Key = null,
    IReadOnlyList<SystemNotificationWarning>? Warnings = null, int? ErrorCode = null)
{
    /// <summary>Gets whether native submission succeeded, including degraded submissions.</summary>
    public bool IsAccepted => Status == SystemNotificationStatus.Accepted;
}

/// <summary>Copied native response. Treat payload and user input as untrusted application data.</summary>
/// <param name="NotificationId">Logical application identity, or empty for unknown/raw native content.</param>
/// <param name="ActivationData">Opaque application payload.</param>
/// <param name="ActionId">Action identity, or null for body activation.</param>
/// <param name="UserInput">Copied text/selection response values.</param>
public sealed record SystemNotificationActivation(string NotificationId, string ActivationData, string? ActionId,
    IReadOnlyDictionary<string, string> UserInput)
{
    /// <summary>Gets the logical group when supplied by the native response envelope.</summary>
    public string Group { get; init; } = string.Empty;
    /// <summary>Gets typed native response metadata, such as a future Linux activation token.</summary>
    public SystemNotificationOptions PlatformData { get; init; } = SystemNotificationOptions.Empty;
}

/// <summary>OS lifecycle change delivered on the service dispatcher.</summary>
/// <param name="Key">Logical application identity.</param>
/// <param name="Reason">Native reason; missing callbacks must not be interpreted as dismissal.</param>
/// <param name="ErrorCode">Native failure code, if available.</param>
public sealed record SystemNotificationChange(SystemNotificationKey Key, string Reason, int? ErrorCode = null);

/// <summary>Owns a native notification session. Calls may originate on any thread.</summary>
/// <remarks>Cancellation before native acceptance cannot retract an already accepted request. Dispose detaches
/// callbacks without erasing history. Subscribe during startup for cold responses; events use the owning dispatcher.
/// GetStatusAsync never prompts. Only an explicit RequestPermissionAsync may ask the native platform for consent.</remarks>
public interface ISystemNotificationService : IAsyncDisposable
{
    /// <summary>Gets supported semantic/platform features independently of current authorization.</summary>
    SystemNotificationCapabilities Capabilities { get; }
    /// <summary>Occurs on a native body/action response.</summary>
    event EventHandler<SystemNotificationActivation>? Activated;
    /// <summary>Occurs for actual native lifecycle callbacks provided by the backend.</summary>
    event EventHandler<SystemNotificationChange>? Changed;
    /// <summary>Queries current native availability/authorization without prompting or registering an identity.</summary>
    Task<SystemNotificationAccess> GetStatusAsync(CancellationToken cancellationToken = default);
    /// <summary>Explicitly requests native authorization if applicable; otherwise returns current status without a dialog.</summary>
    Task<SystemNotificationAccess> RequestPermissionAsync(SystemNotificationPermissionRequest? request = null, CancellationToken cancellationToken = default);
    /// <summary>Snapshots and submits content. Matching logical Id/Group replaces content when Replacement is supported.</summary>
    Task<SystemNotificationResult> ShowAsync(SystemNotification notification, CancellationToken cancellationToken = default);
    /// <summary>Updates progress under the same identity. A backend may use native data updates or resubmit that same entry.</summary>
    Task<SystemNotificationResult> UpdateAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken cancellationToken = default);
    /// <summary>Removes one logical identity, including after restart where the backend supports it.</summary>
    /// <exception cref="ArgumentNullException">The key is null; null never means clear history.</exception>
    Task<SystemNotificationResult> DismissAsync(SystemNotificationKey key, CancellationToken cancellationToken = default);
    /// <summary>Removes the exact native history reference, including a raw entry without a known logical key.</summary>
    Task<SystemNotificationResult> DismissHistoryAsync(SystemNotificationReference reference, CancellationToken cancellationToken = default);
    /// <summary>Removes the specified logical Id across groups where supported.</summary>
    /// <exception cref="ArgumentNullException">The identifier is null.</exception>
    Task<SystemNotificationResult> RemoveByIdAsync(string id, CancellationToken cancellationToken = default);
    /// <summary>Removes a logical group, including the empty default group, where supported.</summary>
    /// <exception cref="ArgumentNullException">The group is null.</exception>
    Task<SystemNotificationResult> RemoveByGroupAsync(string group, CancellationToken cancellationToken = default);
    /// <summary>Clears this application's native delivered notifications where supported.</summary>
    Task<SystemNotificationResult> ClearAsync(CancellationToken cancellationToken = default);
    /// <summary>Enumerates native history. Unsupported providers return an empty list; inspect History first.</summary>
    Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken = default);
}
