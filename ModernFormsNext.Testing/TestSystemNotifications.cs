using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;

namespace ModernFormsNext.Testing;

/// <summary>Deterministic native-notification substitute, scoped to ModernFormsTestHost.</summary>
/// <remarks>Uses production validation and serialized service operations. No OS API, image I/O, clock or
/// timer is used. This fake is evidence for application contracts, not Windows rendering or registration.</remarks>
public sealed class TestSystemNotifications : SystemNotificationServiceBase
{
    private readonly object gate = new();
    private readonly UiTestDispatcher dispatcher;
    private readonly Dictionary<SystemNotificationKey, SystemNotification> notifications = [];
    private SystemNotificationCapabilities capabilities = new("HeadlessTestHost", SystemNotificationFeatures.Basic |
        SystemNotificationFeatures.Images | SystemNotificationFeatures.Replacement | SystemNotificationFeatures.Actions | SystemNotificationFeatures.Inputs |
        SystemNotificationFeatures.Progress | SystemNotificationFeatures.LiveUpdates | SystemNotificationFeatures.Grouping |
        SystemNotificationFeatures.History | SystemNotificationFeatures.Removal | SystemNotificationFeatures.GroupRemoval | SystemNotificationFeatures.Clear | SystemNotificationFeatures.DismissalEvents | SystemNotificationFeatures.IndeterminateProgress);

    internal TestSystemNotifications(UiTestDispatcher dispatcher, TestApplicationLifecycle lifecycle)
        : base(dispatcher.Post, activation => lifecycle.Activate(PlatformApplicationActivation.FromNotification(activation)))
        => this.dispatcher = dispatcher;

    /// <inheritdoc/>
    public override SystemNotificationCapabilities Capabilities { get { lock (gate) return capabilities; } }

    /// <summary>Gets immutable snapshots of submitted content; inspect on the host UI thread.</summary>
    public IReadOnlyDictionary<SystemNotificationKey, SystemNotification> Notifications
    {
        get
        {
            dispatcher.VerifyAccess(); ThrowIfDisposed();
            lock (gate) return new System.Collections.ObjectModel.ReadOnlyDictionary<SystemNotificationKey, SystemNotification>(
                notifications.ToDictionary(p => p.Key, p => SystemNotificationValidation.Snapshot(p.Value)));
        }
    }

    /// <summary>Changes capabilities for subsequent calls, on the host UI thread.</summary>
    public void SetCapabilities(SystemNotificationFeatures features)
    { dispatcher.VerifyAccess(); ThrowIfDisposed(); lock (gate) capabilities = new("HeadlessTestHost", features); }

    private SystemNotificationAccess access = new(SystemNotificationAvailability.Ready);
    private SystemNotificationAccess? permissionResponse;
    private int permissionRequests;
    private sealed record TestReference(TestSystemNotifications Owner, SystemNotificationKey Key) : SystemNotificationReference;

    /// <summary>Gets the number of explicit permission requests. Showing content never increments it.</summary>
    public int PermissionRequests { get { lock (gate) return permissionRequests; } }
    /// <summary>Configures a complete capability/limit snapshot without impersonating a native platform.</summary>
    public void SetCapabilities(SystemNotificationCapabilities value)
    { ArgumentNullException.ThrowIfNull(value); dispatcher.VerifyAccess(); ThrowIfDisposed(); lock (gate) capabilities = value; }
    /// <summary>Configures current access independently of backend capabilities.</summary>
    public void SetAccess(SystemNotificationAccess value)
    { ArgumentNullException.ThrowIfNull(value); dispatcher.VerifyAccess(); ThrowIfDisposed(); lock (gate) access = value; }
    /// <summary>Configures the access returned by an explicit permission request; no dialog is displayed.</summary>
    public void SetPermissionResponse(SystemNotificationAccess value)
    { ArgumentNullException.ThrowIfNull(value); dispatcher.VerifyAccess(); ThrowIfDisposed(); lock (gate) permissionResponse = value; }
    /// <inheritdoc/>
    protected override Task<SystemNotificationAccess> GetStatusCoreAsync(CancellationToken cancellationToken)
    { lock (gate) return Task.FromResult(access); }
    /// <inheritdoc/>
    protected override Task<SystemNotificationAccess> RequestPermissionCoreAsync(SystemNotificationPermissionRequest request, CancellationToken cancellationToken)
    { lock (gate) { permissionRequests++; access = permissionResponse ?? access; return Task.FromResult(access); } }
    /// <inheritdoc/>
    protected override Task<SystemNotificationResult> DismissHistoryCoreAsync(SystemNotificationReference reference, CancellationToken cancellationToken)
        => reference is TestReference item && ReferenceEquals(item.Owner, this)
            ? RemoveCoreAsync(item.Key, null, null, cancellationToken)
            : Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Invalid));

    /// <summary>Simulates body/action activation, preserving user input and using the canonical lifecycle.</summary>
    /// <param name="key">A currently submitted notification.</param>
    /// <param name="actionId">Action Id or ActivationData; null represents the notification body.</param>
    /// <param name="userInput">Optional user-entered input.</param>
    public void Activate(SystemNotificationKey key, string? actionId = null, IReadOnlyDictionary<string, string>? userInput = null)
    {
        dispatcher.VerifyAccess(); ThrowIfDisposed();
        SystemNotification notification;
        lock (gate) notification = notifications[key];
        var action = actionId is null ? null : notification.Actions.Single(a => (a.Id.Length > 0 ? a.Id : a.ActivationData) == actionId);
        PublishActivation(new(notification.Id, action?.ActivationData ?? notification.ActivationData, actionId, userInput ?? new Dictionary<string, string>()) { Group = notification.Group });
    }

    /// <summary>Simulates actual removal by the user, so later updates return NotFound.</summary>
    public void DismissByUser(SystemNotificationKey key)
    {
        dispatcher.VerifyAccess(); ThrowIfDisposed();
        lock (gate) if (!notifications.Remove(key)) return;
        PublishChange(new(key, "UserCanceled"));
    }

    /// <inheritdoc/>
    protected override Task<SystemNotificationResult> ShowCoreAsync(SystemNotification notification, CancellationToken cancellationToken)
    {
        var key = SystemNotificationValidation.GetKey(notification);
        lock (gate) notifications[key] = notification;
        return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Accepted, key));
    }
    /// <inheritdoc/>
    protected override Task<SystemNotificationResult> UpdateCoreAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (!notifications.TryGetValue(key, out var current)) return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.NotFound, key));
            if (current.Progress is null) return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported, key));
            notifications[key] = current with { Progress = progress };
        }
        return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Accepted, key));
    }
    /// <inheritdoc/>
    protected override Task<SystemNotificationResult> RemoveCoreAsync(SystemNotificationKey? key, string? id, string? group, CancellationToken cancellationToken)
    {
        lock (gate)
            foreach (var item in notifications.Keys.Where(k => key is not null ? k == key : id is not null ? k.Id == id : group is null || k.Group == group).ToArray())
                notifications.Remove(item);
        return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Accepted, key));
    }
    /// <inheritdoc/>
    protected override Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryCoreAsync(CancellationToken cancellationToken)
    { lock (gate) return Task.FromResult<IReadOnlyList<SystemNotificationHistoryEntry>>(notifications.Keys.Select(k => new SystemNotificationHistoryEntry(k, new TestReference(this, k))).ToArray()); }
    /// <inheritdoc/>
    protected override ValueTask DisposeCoreAsync() { lock (gate) notifications.Clear(); return ValueTask.CompletedTask; }
}
