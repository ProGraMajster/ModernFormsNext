using System.Collections.Immutable;
using System.Globalization;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.Automation;

/// <summary>Owns an explicit development-time in-process semantic session over registered canonical roots.</summary>
/// <remarks>
/// Construct, register roots and dispose on the application's initialized UI dispatcher. Async
/// query/action methods may be called from any thread and marshal all live reads through that
/// captured production dispatcher. Await them; do not block the UI thread on queued operations.
/// This package starts no listener and provides no transport authentication. Omit it from applications
/// that do not need development automation. Dispose the session before shutting down its dispatcher.
/// The application must initialize that dispatcher before construction; VerifyAccess checks thread
/// access, not backend readiness. All roots in a session must belong to that same dispatcher.
/// </remarks>
/// <example><code>
/// using var automation = new AutomationSession();
/// using var root = automation.RegisterRoot(form);
/// var found = await automation.FindOneAsync(root.RootId, new AutomationQuery { AutomationId = "save" });
/// if (found.Error == AutomationErrorCode.None)
///     await automation.PerformActionAsync(root.RootId, found.Value!.Handle, AccessibleActions.Invoke);
/// </code></example>
public sealed partial class AutomationSession : IDisposable
{
    private const AutomationCapability AllCapabilities = AutomationCapability.Inspect | AutomationCapability.Query | AutomationCapability.Actions;
    private readonly Dispatcher dispatcher;
    private readonly List<AutomationRootRegistration> roots = [];
    private volatile bool stopped;

    /// <summary>Creates a new session on the current production UI dispatcher.</summary>
    /// <param name="capabilities">Explicitly allowed operations; unknown flags are rejected.</param>
    /// <param name="limits">Immutable limits applying to every traversal; null selects bounded defaults.</param>
    /// <exception cref="InvalidOperationException">The caller is not on the captured UI dispatcher.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Capabilities or limits are invalid.</exception>
    public AutomationSession(AutomationCapability capabilities = AllCapabilities, AutomationQueryOptions? limits = null)
    {
        dispatcher = Dispatcher.UIThread;
        dispatcher.VerifyAccess();
        ValidateCapabilities(capabilities);
        Limits = limits ?? new();
        Limits.Validate();
        Capabilities = capabilities;
    }

    /// <summary>Gets a unique random nonce for this session, independent of process IDs and root names.</summary>
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    /// <summary>Gets the session's immutable capability policy.</summary>
    public AutomationCapability Capabilities { get; }
    /// <summary>Gets immutable budgets used by all traversals in this session.</summary>
    public AutomationQueryOptions Limits { get; }
    /// <summary>Gets whether Stop or Dispose has ended this session; safe to read from any thread.</summary>
    public bool IsStopped => stopped;

    /// <summary>Registers a borrowed Form or other WindowBase on its UI thread.</summary>
    /// <param name="root">The live window explicitly allowed for automation; it may be registered before Show.</param>
    /// <param name="capabilities">The requested root policy, intersected with session capabilities.</param>
    /// <returns>A disposable registration that never owns or closes the window.</returns>
    /// <exception cref="ArgumentNullException">The root is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Capabilities contain unknown flags.</exception>
    /// <exception cref="ObjectDisposedException">The session or window has ended.</exception>
    /// <exception cref="InvalidOperationException">The root is already registered or the caller is not on the UI thread.</exception>
    public AutomationRootRegistration RegisterRoot(WindowBase root, AutomationCapability capabilities = AllCapabilities)
    {
        VerifyRegistration(root, capabilities);
        ObjectDisposedException.ThrowIf(root.InputBindingsClosed, nameof(root));
        var registration = new AutomationRootRegistration(this, root, Capabilities & capabilities);
        roots.Add(registration);
        return registration;
    }

    /// <summary>Registers a borrowed windowless Skia surface on its UI thread.</summary>
    /// <param name="root">The live surface. Its canonical Root remains the only semantic source.</param>
    /// <param name="capabilities">The requested root policy, intersected with session capabilities.</param>
    /// <returns>A disposable registration; surface/root disposal is checked on each live operation.</returns>
    /// <exception cref="ArgumentNullException">The root is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Capabilities contain unknown flags.</exception>
    /// <exception cref="ObjectDisposedException">The session, surface or root has ended.</exception>
    /// <exception cref="InvalidOperationException">The surface is already registered or the caller is not on the UI thread.</exception>
    public AutomationRootRegistration RegisterRoot(SkiaControlSurface root, AutomationCapability capabilities = AllCapabilities)
    {
        VerifyRegistration(root, capabilities);
        ObjectDisposedException.ThrowIf(root.IsDisposed || root.Root.IsDisposed, nameof(root));
        var registration = new AutomationRootRegistration(this, root, Capabilities & capabilities);
        roots.Add(registration);
        return registration;
    }

    /// <summary>Ends the session on the UI thread, unregistering all roots without closing them. Repeated calls are safe.</summary>
    public void Stop()
    {
        VerifyAccess();
        if (stopped) return;
        stopped = true;
        foreach (var root in roots) root.Detach();
        roots.Clear();
        lifetimeChanged?.Invoke();
    }

    /// <summary>Marshals session cleanup to its production UI dispatcher.</summary>
    /// <returns>A task completed after subscriptions are removed and every handle is invalidated.</returns>
    /// <remarks>A background request is queued. It does not preempt a running getter or action; session end occurs when cleanup executes.</remarks>
    public Task StopAsync()
    {
        if (stopped) return Task.CompletedTask;
        if (dispatcher.CheckAccess()) { Stop(); return Task.CompletedTask; }
        return dispatcher.InvokeAsync(Stop).GetTask();
    }

    /// <summary>Ends the session on its UI thread, without disposing application windows or controls.</summary>
    public void Dispose() => Stop();

    internal void VerifyAccess() => dispatcher.VerifyAccess();
    internal void Remove(AutomationRootRegistration root)
    {
        root.Detach();
        if (roots.Remove(root)) lifetimeChanged?.Invoke();
    }
    internal bool IsLive(AutomationRootRegistration root)
    {
        if (root.IsAlive) return true;
        Remove(root);
        return false;
    }

    private void VerifyRegistration(object root, AutomationCapability capabilities)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(root);
        ObjectDisposedException.ThrowIf(stopped, this);
        ValidateCapabilities(capabilities);
        PruneRoots();
        if (roots.Any(r => r.Represents(root))) throw new InvalidOperationException("The root is already registered.");
        if (roots.Count >= Limits.MaxNodes) throw new InvalidOperationException("The root registration budget is exhausted.");
    }

    private static void ValidateCapabilities(AutomationCapability capabilities)
    {
        if ((capabilities & ~AllCapabilities) != 0) throw new ArgumentOutOfRangeException(nameof(capabilities));
    }

    private void PruneRoots()
    {
        for (int index = roots.Count - 1; index >= 0; index--)
            if (!roots[index].IsAlive) Remove(roots[index]);
    }

    private AutomationErrorCode ResolveRoot(string rootId, AutomationCapability capability, out AutomationRootRegistration? root)
    {
        root = null;
        if (stopped) return AutomationErrorCode.SessionEnded;
        if ((Capabilities & capability) != capability) return AutomationErrorCode.CapabilityDenied;
        if (string.IsNullOrEmpty(rootId)) return AutomationErrorCode.InvalidArgument;
        PruneRoots();
        root = roots.Find(r => string.Equals(r.RootId, rootId, StringComparison.Ordinal));
        if (root is null) return AutomationErrorCode.StaleNode;
        return (root.Capabilities & capability) == capability ? AutomationErrorCode.None : AutomationErrorCode.CapabilityDenied;
    }

    private AutomationErrorCode ValidateHandle(AutomationNodeHandle handle)
    {
        if (stopped) return AutomationErrorCode.SessionEnded;
        if (!string.Equals(handle.SessionId, SessionId, StringComparison.Ordinal)) return AutomationErrorCode.StaleNode;
        if (!long.TryParse(handle.RuntimeId, NumberStyles.None, CultureInfo.InvariantCulture, out long id)
            || id <= 0 || !string.Equals(Id(id), handle.RuntimeId, StringComparison.Ordinal)) return AutomationErrorCode.InvalidArgument;
        return AutomationErrorCode.None;
    }

    private static string Id(long value) => value.ToString(CultureInfo.InvariantCulture);

    private async Task<AutomationResult<T>> Dispatch<T>(Func<string, AutomationResult<T>> action, CancellationToken token, T? empty = default)
    {
        string captureId = Guid.NewGuid().ToString("N");
        if (stopped) return new(empty, AutomationErrorCode.SessionEnded, captureId);
        try
        {
            token.ThrowIfCancellationRequested();
            if (dispatcher.CheckAccess()) return dispatcher.Invoke(() => action(captureId));
            return await dispatcher.InvokeAsync(() => action(captureId), DispatcherPriority.Default, token).GetTask().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Never forward arbitrary application exception messages, inner exceptions or ToString.
            return new(empty, stopped ? AutomationErrorCode.SessionEnded : AutomationErrorCode.ApplicationError, captureId);
        }
    }
}
