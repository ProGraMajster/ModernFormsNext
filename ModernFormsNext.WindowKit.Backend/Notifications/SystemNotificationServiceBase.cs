using ModernFormsNext.Notifications;

namespace ModernFormsNext.WindowKit.Backend.Notifications;

/// <summary>Serializes service operations and centralizes validation, cancellation and callback lifetime.</summary>
/// <remarks>Backend implementations must not synchronously wait for the dispatcher. The semaphore is deliberately
/// retained after disposal so already queued callers observe ObjectDisposedException instead of racing disposal.</remarks>
public abstract class SystemNotificationServiceBase : ISystemNotificationService
{
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly Action<Action> dispatch;
    private readonly Action<SystemNotificationActivation>? lifecycleActivation;
    private readonly object lifetimeGate = new();
    private Task? disposal;
    private int disposed;

    /// <summary>Creates a service with a nonblocking dispatcher and optional canonical lifecycle ingress.</summary>
    protected SystemNotificationServiceBase(Action<Action> dispatch, Action<SystemNotificationActivation>? lifecycleActivation = null)
    {
        this.dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        this.lifecycleActivation = lifecycleActivation;
    }

    /// <inheritdoc/>
    public abstract SystemNotificationCapabilities Capabilities { get; }
    /// <inheritdoc/>
    public event EventHandler<SystemNotificationActivation>? Activated;
    /// <inheritdoc/>
    public event EventHandler<SystemNotificationChange>? Changed;

    /// <inheritdoc/>
    public Task<SystemNotificationResult> ShowAsync(SystemNotification notification, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(notification);
        SystemNotification snapshot;
        try { snapshot = SystemNotificationValidation.Snapshot(notification); }
        catch (ArgumentException) { return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Invalid)); }
        return Execute(async () =>
        {
            if (!Capabilities.IsSupported) return new(SystemNotificationStatus.Unsupported);
            var access = await GetShowAccessCoreAsync(snapshot, cancellationToken).ConfigureAwait(false);
            if (Blocked(access) is { } blocked) return new(blocked);
            var warnings = new List<SystemNotificationWarning>();
            SystemNotification normalized;
            try { normalized = SystemNotificationValidation.Normalize(snapshot, Capabilities, warnings); }
            catch (ArgumentException) { return new(SystemNotificationStatus.Invalid); }
            if (warnings.Count > 0 && !snapshot.AllowDegradation)
                return new(SystemNotificationStatus.Unsupported, Warnings: warnings.AsReadOnly());
            var result = await ShowCoreAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (result.Warnings is not null) warnings.AddRange(result.Warnings);
            return result with { Warnings = warnings.AsReadOnly() };
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SystemNotificationResult> UpdateAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken cancellationToken = default)
        => Execute(async () =>
        {
            try { SystemNotificationValidation.ValidateKey(key); SystemNotificationValidation.ValidateProgress(progress); }
            catch (ArgumentException) { return new(SystemNotificationStatus.Invalid, key); }
            var access = await GetUpdateAccessCoreAsync(key, cancellationToken).ConfigureAwait(false);
            if (Blocked(access) is { } blocked) return new(blocked, key);
            return Capabilities.Supports(SystemNotificationFeatures.LiveUpdates) &&
                (progress.Value is not null || Capabilities.Supports(SystemNotificationFeatures.IndeterminateProgress))
                ? await UpdateCoreAsync(key, progress, cancellationToken).ConfigureAwait(false)
                : new(SystemNotificationStatus.Unsupported, key);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<SystemNotificationResult> DismissAsync(SystemNotificationKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Remove(key, null, null, cancellationToken);
    }
    /// <inheritdoc/>
    public Task<SystemNotificationResult> RemoveByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Remove(null, id, null, cancellationToken);
    }
    /// <inheritdoc/>
    public Task<SystemNotificationResult> RemoveByGroupAsync(string group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        return Remove(null, null, group, cancellationToken);
    }
    /// <inheritdoc/>
    public Task<SystemNotificationResult> ClearAsync(CancellationToken cancellationToken = default)
        => Remove(null, null, null, cancellationToken);

    private Task<SystemNotificationResult> Remove(SystemNotificationKey? key, string? id, string? group, CancellationToken cancellationToken)
        => Execute(async () =>
        {
            try
            {
                if (key is not null) SystemNotificationValidation.ValidateKey(key);
                if (id is not null) SystemNotificationValidation.CheckString(id, 4096, false);
                if (group is not null) SystemNotificationValidation.CheckString(group, 4096, true);
            }
            catch (ArgumentException) { return new(SystemNotificationStatus.Invalid, key); }
            var required = key is not null || id is not null ? SystemNotificationFeatures.Removal : group is not null ? SystemNotificationFeatures.GroupRemoval : SystemNotificationFeatures.Clear;
            if (!Capabilities.Supports(required))
                return new(SystemNotificationStatus.Unsupported);
            return await RemoveCoreAsync(key, id, group, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken = default)
        => Execute(() => Capabilities.Supports(SystemNotificationFeatures.History)
            ? GetHistoryCoreAsync(cancellationToken) : Task.FromResult<IReadOnlyList<SystemNotificationHistoryEntry>>([]), cancellationToken);

    /// <inheritdoc/>
    public Task<SystemNotificationAccess> GetStatusAsync(CancellationToken cancellationToken = default)
        => Execute(() => GetStatusCoreAsync(cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public Task<SystemNotificationAccess> RequestPermissionAsync(SystemNotificationPermissionRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new();
        if ((request.Presentation & ~(SystemNotificationPresentation.Alert | SystemNotificationPresentation.Sound | SystemNotificationPresentation.Badge)) != 0)
            throw new ArgumentException("Unknown presentation request.", nameof(request));
        var snapshot = request with { PlatformOptions = request.PlatformOptions.Snapshot() };
        return Execute(() => RequestPermissionCoreAsync(snapshot, cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SystemNotificationResult> DismissHistoryAsync(SystemNotificationReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return Execute(() => DismissHistoryCoreAsync(reference, cancellationToken), cancellationToken);
    }

    private static SystemNotificationStatus? Blocked(SystemNotificationAccess access) => access.Availability switch
    {
        SystemNotificationAvailability.PermissionRequired => SystemNotificationStatus.PermissionRequired,
        SystemNotificationAvailability.PermissionDenied => SystemNotificationStatus.PermissionDenied,
        SystemNotificationAvailability.DisabledByUser => SystemNotificationStatus.Disabled,
        SystemNotificationAvailability.Restricted => SystemNotificationStatus.Restricted,
        SystemNotificationAvailability.Unavailable => SystemNotificationStatus.Unavailable,
        _ => null
    };

    /// <summary>Queries current access without changing authorization or prompting the user.</summary>
    protected virtual Task<SystemNotificationAccess> GetStatusCoreAsync(CancellationToken cancellationToken)
        => Task.FromResult(Capabilities.IsSupported ? new SystemNotificationAccess(SystemNotificationAvailability.Unknown) : SystemNotificationAccess.Unavailable);
    /// <summary>Queries access for copied content without prompting. Defaults to the global status query.</summary>
    /// <remarks>Override for native channel restrictions or per-notification permission exemptions.
    /// The snapshot has not yet been semantically normalized; inspect native options defensively.
    /// Do not mutate global authorization to permit one exempt request.</remarks>
    protected virtual Task<SystemNotificationAccess> GetShowAccessCoreAsync(SystemNotification notification, CancellationToken cancellationToken)
        => GetStatusCoreAsync(cancellationToken);
    /// <summary>Queries access for an existing logical identity without prompting. Defaults to global status.</summary>
    /// <remarks>Providers with per-notification policy must resolve the native entry/category by key;
    /// they must not grant general permission merely because one category is exempt.</remarks>
    protected virtual Task<SystemNotificationAccess> GetUpdateAccessCoreAsync(SystemNotificationKey key, CancellationToken cancellationToken)
        => GetStatusCoreAsync(cancellationToken);
    /// <summary>Requests authorization only when explicitly called. The default merely queries access.</summary>
    protected virtual Task<SystemNotificationAccess> RequestPermissionCoreAsync(SystemNotificationPermissionRequest request, CancellationToken cancellationToken)
        => GetStatusCoreAsync(cancellationToken);
    /// <summary>Removes an opaque history entry. Providers must reject references from other services.</summary>
    protected virtual Task<SystemNotificationResult> DismissHistoryCoreAsync(SystemNotificationReference reference, CancellationToken cancellationToken)
        => Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported));

    private async Task<T> Execute<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            return await action().ConfigureAwait(false);
        }
        finally { operations.Release(); }
    }

    /// <summary>Raises copied native activation on the dispatcher and existing lifecycle, outside native callbacks.</summary>
    protected void PublishActivation(SystemNotificationActivation activation)
    {
        var snapshot = SystemNotificationValidation.CopyActivation(activation);
        if (Volatile.Read(ref disposed) != 0) return;
        dispatch(() =>
        {
            if (Volatile.Read(ref disposed) != 0) return;
            // Notify both ingress paths even if an application observer throws. The dispatcher owns
            // reporting managed observer failures; none unwind across a COM/WndProc callback.
            try { lifecycleActivation?.Invoke(snapshot); }
            finally { Activated?.Invoke(this, snapshot); }
        });
    }

    /// <summary>Raises a native lifecycle callback on the dispatcher unless this service has been disposed.</summary>
    protected void PublishChange(SystemNotificationChange change)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        dispatch(() => { if (Volatile.Read(ref disposed) == 0) Changed?.Invoke(this, change); });
    }

    /// <summary>Checks the service lifetime.</summary>
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
    /// <summary>Submits validated, copied content; implementations perform optional native-specific validation.</summary>
    protected abstract Task<SystemNotificationResult> ShowCoreAsync(SystemNotification notification, CancellationToken cancellationToken);
    /// <summary>Submits a validated native progress update.</summary>
    protected abstract Task<SystemNotificationResult> UpdateCoreAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken cancellationToken);
    /// <summary>Removes matching native content; all-null selectors mean clear history.</summary>
    protected abstract Task<SystemNotificationResult> RemoveCoreAsync(SystemNotificationKey? key, string? id, string? group, CancellationToken cancellationToken);
    /// <summary>Reads native history, without a local substitute for missing OS entries.</summary>
    protected abstract Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryCoreAsync(CancellationToken cancellationToken);
    /// <summary>Detaches native handlers and registration. Called once after in-flight operations finish.</summary>
    protected abstract ValueTask DisposeCoreAsync();

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        lock (lifetimeGate)
        {
            Interlocked.Exchange(ref disposed, 1);
            return new(disposal ??= DisposeOnceAsync());
        }
    }

    private async Task DisposeOnceAsync()
    {
        await operations.WaitAsync().ConfigureAwait(false);
        try { await DisposeCoreAsync().ConfigureAwait(false); }
        finally { Activated = null; Changed = null; operations.Release(); }
    }
}
