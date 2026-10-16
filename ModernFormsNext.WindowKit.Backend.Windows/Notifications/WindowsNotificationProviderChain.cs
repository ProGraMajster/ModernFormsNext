using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using ModernFormsNext.Notifications;
using ModernFormsNext.WindowKit.Backend.Notifications;

namespace ModernFormsNext.WindowKit.Backend.Windows.Notifications;

// Owned and serialized by SystemNotificationServiceBase, not a second platform
// service. Each candidate is tried at most once for this application's lifetime.
// Native initialization/disposal remains on the dispatcher even after async failure.
internal sealed class WindowsNotificationProviderChain(
    IReadOnlyList<(string Name, Func<IWindowsNotificationBackend> Create)> candidates,
    Action<Action> dispatch, Func<bool> checkAccess, IEnumerable<string>? initialReasons = null) : IWindowsNotificationBackend
{
    private readonly ConcurrentQueue<string> reasons = new(initialReasons ?? []);
    private volatile IWindowsNotificationBackend? current;
    private WindowsNotificationRegistrationOptions options = null!;
    private Action<SystemNotificationActivation> activated = null!;
    private Action<SystemNotificationChange> changed = null!;
    private int next;
    private volatile bool disposed;
    public SystemNotificationCapabilities Capabilities
    {
        get
        {
            var capabilities = current?.Capabilities ?? SystemNotificationCapabilities.Unavailable;
            return reasons.IsEmpty ? capabilities : capabilities with
            { Reason = string.Join(" ", reasons.Append(capabilities.Reason).Where(reason => !string.IsNullOrWhiteSpace(reason))) };
        }
    }

    public void Initialize(WindowsNotificationRegistrationOptions options, Action<SystemNotificationActivation> activated,
        Action<SystemNotificationChange> changed)
    {
        this.options = options; this.activated = activated; this.changed = changed;
        SelectNext();
    }

    private void SelectNext()
    {
        while (!disposed && next < candidates.Count)
        {
            var candidate = candidates[next++];
            try
            {
                var backend = candidate.Create();
                current = backend; // Cold activation during Register must not be dropped.
                backend.Initialize(options,
                    activation => { if (!disposed && ReferenceEquals(current, backend)) activated(activation); },
                    change => { if (!disposed && ReferenceEquals(current, backend)) changed(change); });
                return;
            }
            catch (Exception error) when (InitializationFailure(error))
            {
                // Providers unwind partially created native state in Initialize's
                // catch. Only public failure category/code, never payload/path/URL.
                reasons.Enqueue($"{candidate.Name} initialization unavailable (0x{error.HResult:X8}).");
                current = null;
            }
        }
    }

    private static bool InitializationFailure(Exception e) => e is FileNotFoundException or FileLoadException or
        DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeLoadException or
        TargetInvocationException or COMException or Win32Exception or PlatformNotSupportedException;

    private static bool BrokenProvider(Exception e) => e is PlatformNotSupportedException ||
        e is COMException && e.HResult is unchecked((int)0x80070490) or unchecked((int)0x80040154) or unchecked((int)0x800706BA);

    private async Task<T> Run<T>(Func<IWindowsNotificationBackend, Task<T>> operation, T unavailable, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        while (current is { } backend)
        {
            token.ThrowIfCancellationRequested();
            try { return await operation(backend).ConfigureAwait(false); }
            catch (Exception error) when (BrokenProvider(error))
            {
                reasons.Enqueue($"{backend.Capabilities.Backend} disabled for this process after native failure (0x{error.HResult:X8}).");
                current = null;
                try { await OnUi(() => backend.DisposeAsync().AsTask()).ConfigureAwait(false); }
                catch (Exception cleanup) { reasons.Enqueue($"Provider cleanup failed (0x{cleanup.HResult:X8})."); }
                await OnUi(() => { SelectNext(); return Task.CompletedTask; }).ConfigureAwait(false);
                // The failed provider is never retried; at most three candidates.
            }
        }
        return unavailable;
    }

    private Task OnUi(Func<Task> operation)
    {
        if (checkAccess()) return operation();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatch(async () =>
        {
            try { await operation().ConfigureAwait(false); completion.TrySetResult(); }
            catch (Exception error) { completion.TrySetException(error); }
        });
        return completion.Task;
    }

    public Task<SystemNotificationAccess> GetStatusAsync(CancellationToken token)
        => Run(backend => backend.GetStatusAsync(token), SystemNotificationAccess.Unavailable, token);
    public Task<SystemNotificationResult> ShowAsync(SystemNotification notification, CancellationToken token)
        => Run(async backend =>
        {
            // A runtime fallback can lower capabilities after the facade normalized
            // the request. Revalidate against the actual recipient, not stale toast capabilities.
            var warnings = new List<SystemNotificationWarning>();
            var normalized = SystemNotificationValidation.Normalize(notification, backend.Capabilities, warnings);
            if (warnings.Count > 0 && !notification.AllowDegradation) return new(SystemNotificationStatus.Unsupported, Warnings: warnings);
            var result = await backend.ShowAsync(normalized, token).ConfigureAwait(false);
            return result with { Warnings = warnings.Concat(result.Warnings ?? []).ToArray() };
        }, new(SystemNotificationStatus.Unsupported), token);
    public Task<SystemNotificationResult> UpdateAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken token)
        => Run(backend => backend.UpdateAsync(key, progress, token), new(SystemNotificationStatus.Unsupported, key), token);
    public Task<SystemNotificationResult> RemoveAsync(SystemNotificationKey? key, string? tag, string? group, CancellationToken token)
        => Run(backend => backend.RemoveAsync(key, tag, group, token), new(SystemNotificationStatus.Unsupported, key), token);
    public Task<SystemNotificationResult> DismissHistoryAsync(SystemNotificationReference reference, CancellationToken token)
        => Run(backend => backend.DismissHistoryAsync(reference, token), new(SystemNotificationStatus.Unsupported), token);
    public Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryAsync(CancellationToken token)
        => Run<IReadOnlyList<SystemNotificationHistoryEntry>>(backend => backend.GetHistoryAsync(token), [], token);
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        var backend = current; current = null;
        if (backend is not null) await OnUi(() => backend.DisposeAsync().AsTask()).ConfigureAwait(false);
    }
}
