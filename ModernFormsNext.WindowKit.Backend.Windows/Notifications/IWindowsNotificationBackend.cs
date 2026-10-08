using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;

namespace ModernFormsNext.WindowKit.Backend.Windows.Notifications;

/// <summary>Transport boundary for optional Windows notification assemblies. Not a service locator.</summary>
/// <remarks>Initialize is called once on the application UI thread. Subsequent operations are serialized by
/// WindowsSystemNotificationService and may execute on worker threads. Implementations marshal UI-affine work.
/// Native callbacks must copy data and must not propagate managed exceptions into COM or WndProc.</remarks>
public interface IWindowsNotificationBackend : IAsyncDisposable
{
    /// <summary>Gets successfully probed capabilities after initialization.</summary>
    SystemNotificationCapabilities Capabilities { get; }
    /// <summary>Queries the native application's notification setting without showing a permission dialog.</summary>
    Task<SystemNotificationAccess> GetStatusAsync(CancellationToken cancellationToken);
    /// <summary>Removes an opaque native history entry belonging to this provider instance.</summary>
    Task<SystemNotificationResult> DismissHistoryAsync(SystemNotificationReference reference, CancellationToken cancellationToken);
    /// <summary>Registers native handlers before enabling OS activation.</summary>
    void Initialize(WindowsNotificationRegistrationOptions options, Action<SystemNotificationActivation> activated, Action<SystemNotificationChange> changed);
    /// <summary>Validates Windows content and submits it to the native API.</summary>
    Task<SystemNotificationResult> ShowAsync(SystemNotification notification, CancellationToken cancellationToken);
    /// <summary>Updates existing native progress data.</summary>
    Task<SystemNotificationResult> UpdateAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken cancellationToken);
    /// <summary>Removes native notifications selected by identity, tag, group or all (all-null).</summary>
    Task<SystemNotificationResult> RemoveAsync(SystemNotificationKey? key, string? tag, string? group, CancellationToken cancellationToken);
    /// <summary>Queries the actual native notification history.</summary>
    Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken);
}
