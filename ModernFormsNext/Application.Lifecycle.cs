using ModernFormsNext.WindowKit.Threading;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext;

public static partial class Application
{
    private static ApplicationLifecycle applicationLifecycle = new();

    /// <summary>Gets normalized lifecycle, activation and explicit state-restoration notifications.</summary>
    /// <remarks>
    /// Access and subscribe on the application UI thread. This facade consumes the existing backend
    /// lifecycle provider; it does not own another application runtime or animation pause policy.
    /// TestHost scopes its subscriptions and diagnostics independently from the borrowed application.
    /// </remarks>
    /// <example><code>
    /// Application.Lifecycle.ActivationReceived += (_, e) =&gt;
    ///     HandleActivation(e.Activation);
    /// Application.Lifecycle.StateSaving += (_, e) =&gt;
    ///     e.Data = new PlatformApplicationStateData(1, new Dictionary&lt;string, string&gt; { ["page"] = "home" });
    /// </code></example>
    public static ApplicationLifecycle Lifecycle
    {
        get { applicationLifecycle.EnsureBound(); return applicationLifecycle; }
    }

    internal static bool IsCurrentLifecycle(ApplicationLifecycle lifecycle)
        => ReferenceEquals(applicationLifecycle, lifecycle);

    internal static void NotifyLifecycleStarting(PlatformApplicationActivation activation)
    {
        object runtimeIdentity = RuntimeIdentity;
        var controller = Lifecycle.Controller;
        if (controller is null || is_exiting)
            return;
        var current = controller.Snapshot;
        controller.Publish(new(PlatformApplicationPhase.Starting, current.State,
            current.IsActive, current.HostCount, current.HostGeneration));
        if (!ReferenceEquals(runtimeIdentity, RuntimeIdentity) || is_exiting ||
            controller.Snapshot.Phase is PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited)
            return;
        controller.Activate(activation);
        if (!ReferenceEquals(runtimeIdentity, RuntimeIdentity) || is_exiting ||
            controller.Snapshot.Phase is PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited)
            return;
        current = controller.Snapshot;
        controller.Publish(new(PlatformApplicationPhase.Running, current.State,
            current.IsActive, current.HostCount, current.HostGeneration));
    }

    internal static void NotifyLifecycleExiting()
    {
        object runtimeIdentity = RuntimeIdentity;
        var controller = Lifecycle.Controller;
        if (controller is null || controller.Snapshot.Phase is PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited)
            return;
        var current = controller.Snapshot;
        Exception? notificationFailure = null;
        try
        {
            controller.Publish(new(PlatformApplicationPhase.Exiting, current.State,
                false, current.HostCount, current.HostGeneration));
        }
        catch (Exception exception) { notificationFailure = exception; }
        try
        {
            if (ReferenceEquals(runtimeIdentity, RuntimeIdentity))
                controller.RequestSaveState(PlatformApplicationStateReason.Exit);
        }
        catch (Exception stateFailure)
        {
            if (notificationFailure is not null)
                throw new AggregateException("Lifecycle exit and state-saving observers failed.", notificationFailure, stateFailure);
            throw;
        }
        if (notificationFailure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(notificationFailure).Throw();
    }

    internal static void NotifyLifecycleExited()
    {
        var controller = Lifecycle.Controller;
        if (controller is null)
            return;
        var current = controller.Snapshot;
        controller.Publish(new(PlatformApplicationPhase.Exited, PlatformApplicationLifecycleState.NoHost,
            false, 0, current.HostGeneration));
    }

    internal static void NotifyLifecycleWindowStateChanged()
        // Individual native windows may be exercised without Application.Run. Updating these
        // counters must not bind process lifecycle subscriptions from a window callback thread.
        => applicationLifecycle.UpdateWindowCounts(OpenForms.Count, OpenForms.Count(form => form.IsActive));
}

/// <summary>Exposes the current backend's normalized application lifecycle on the UI thread.</summary>
/// <remarks>
/// The backend defines deterministic transitions and activation ordering. Window focus is reported
/// separately in diagnostics: an inactive desktop window does not imply a suspended application.
/// StateSaving/StateRestoring exchange explicit bounded data only; no control tree is serialized.
/// Legacy providers implementing only IPlatformApplicationLifecycle cannot supply richer events.
/// Do not retain the facade obtained inside a TestHost after that host is disposed.
/// </remarks>
public sealed class ApplicationLifecycle
{
    private const int HistoryLimit = 64;
    private readonly Queue<PlatformApplicationLifecycleEventArgs> history = new();
    private IPlatformApplicationLifecycleNotifications? provider;
    private IPlatformDispatcher? dispatcher;
    private PlatformApplicationLifecycleSnapshot snapshot = new(PlatformApplicationPhase.NotStarted, PlatformApplicationLifecycleState.Unknown);
    private PlatformActivationKind? lastActivationKind;
    private int openWindowCount;
    private int activeWindowCount;
    private int generation;
    private bool disposed;
    private EventHandler<PlatformApplicationLifecycleEventArgs>? lifecycleChanged;
    private EventHandler<PlatformApplicationActivationEventArgs>? activationReceived;
    private EventHandler<PlatformApplicationStateSavingEventArgs>? stateSaving;
    private EventHandler<PlatformApplicationStateRestoringEventArgs>? stateRestoring;

    internal ApplicationLifecycle() { }

    /// <summary>Gets the provider's immutable current lifecycle snapshot.</summary>
    public PlatformApplicationLifecycleSnapshot Snapshot
    {
        get { EnsureBound(); return snapshot; }
    }

    /// <summary>Gets the most recently supplied explicit activation payload, or null before activation.</summary>
    /// <remarks>Payloads may contain private arguments or file locations; diagnostics intentionally omit them.</remarks>
    public PlatformApplicationActivation? LastActivation
    {
        get { EnsureBound(); return provider?.LastActivation; }
    }

    /// <summary>Delivers an explicit activation through the current platform lifecycle provider.</summary>
    /// <param name="activation">The immutable, bounded activation payload supplied by the application or host.</param>
    /// <remarks>
    /// Call on the UI thread. Delivery raises the canonical activation notification; it does not
    /// activate a window, open a file or URI, grant permissions, or implement inter-process transport.
    /// Reentrant delivery is queued behind the provider's current notification.
    /// </remarks>
    /// <exception cref="NotSupportedException">The current provider does not support explicit lifecycle delivery.</exception>
    public void DeliverActivation(PlatformApplicationActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        GetController().Activate(activation);
    }

    /// <summary>Synchronously requests explicit state from the current application's state-saving subscribers.</summary>
    /// <param name="reason">The host operation for which state is requested.</param>
    /// <returns>The bounded, versioned string data supplied by subscribers, or null when none is supplied.</returns>
    /// <remarks>
    /// Call on the UI thread outside a lifecycle notification. This method performs no persistence
    /// or serialization of controls. The caller owns storage and any later restoration. A reentrant
    /// request is rejected because its synchronous result cannot wait behind the active notification.
    /// </remarks>
    /// <exception cref="NotSupportedException">The current provider does not support explicit lifecycle delivery.</exception>
    public PlatformApplicationStateData? SaveState(PlatformApplicationStateReason reason)
        => GetController().RequestSaveState(reason);

    /// <summary>Delivers explicit saved data to the current application's state-restoration subscribers.</summary>
    /// <param name="data">The immutable versioned string data supplied by the host or application.</param>
    /// <param name="reason">The host operation responsible for restoration.</param>
    /// <remarks>
    /// Call on the UI thread. Subscribers interpret the schema; this method performs no file I/O,
    /// arbitrary deserialization, window activation, or automatic control-tree restoration.
    /// Reentrant delivery is queued behind the provider's current notification.
    /// </remarks>
    /// <exception cref="NotSupportedException">The current provider does not support explicit lifecycle delivery.</exception>
    public void RestoreState(PlatformApplicationStateData data, PlatformApplicationStateReason reason)
    {
        ArgumentNullException.ThrowIfNull(data);
        GetController().RestoreState(data, reason);
    }

    /// <summary>Occurs after a normalized lifecycle transition commits, on the UI thread.</summary>
    public event EventHandler<PlatformApplicationLifecycleEventArgs>? LifecycleChanged
    {
        add { EnsureBound(); lifecycleChanged += value; }
        remove { if (!disposed) { EnsureBound(); lifecycleChanged -= value; } }
    }

    /// <summary>Occurs for an explicit launch or subsequent activation, on the UI thread.</summary>
    public event EventHandler<PlatformApplicationActivationEventArgs>? ActivationReceived
    {
        add { EnsureBound(); activationReceived += value; }
        remove { if (!disposed) { EnsureBound(); activationReceived -= value; } }
    }

    /// <summary>Provides an opportunity to supply bounded application state before suspension, recreation or exit.</summary>
    /// <remarks>Runs synchronously on the UI thread. Set Data explicitly; persistence is owned by the host/application.</remarks>
    public event EventHandler<PlatformApplicationStateSavingEventArgs>? StateSaving
    {
        add { EnsureBound(); stateSaving += value; }
        remove { if (!disposed) { EnsureBound(); stateSaving -= value; } }
    }

    /// <summary>Supplies previously saved explicit data during a host restoration operation.</summary>
    /// <remarks>Runs on the UI thread before subsequent backend activation as defined by its transition sequence.</remarks>
    public event EventHandler<PlatformApplicationStateRestoringEventArgs>? StateRestoring
    {
        add { EnsureBound(); stateRestoring += value; }
        remove { if (!disposed) { EnsureBound(); stateRestoring -= value; } }
    }

    /// <summary>Captures lifecycle metadata without activation arguments, files, URIs or saved-state contents.</summary>
    /// <returns>A detached immutable snapshot containing at most 64 recent transitions.</returns>
    public ApplicationLifecycleDiagnostics GetDiagnostics()
    {
        EnsureBound();
        return new(snapshot, lastActivationKind, openWindowCount, activeWindowCount, history.ToArray());
    }

    internal IPlatformApplicationLifecycleController? Controller
    {
        get { EnsureBound(); return provider as IPlatformApplicationLifecycleController; }
    }

    private IPlatformApplicationLifecycleController GetController()
        => Controller ?? throw new NotSupportedException("The current platform lifecycle provider does not support explicit activation or state delivery.");

    internal void EnsureBound()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        IPlatformDispatcher? currentDispatcher = PlatformServiceRegistry.GetService<IPlatformDispatcher>();
        var currentProvider = PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>() as IPlatformApplicationLifecycleNotifications;
        // Before backend initialization there is no dispatcher to validate yet. Resolving the
        // WindowKit singleton then would cache its null implementation for subsequent Run.
        if (currentDispatcher is null && currentProvider is not null)
            Dispatcher.UIThread.VerifyAccess();
        else if (currentDispatcher is not null && !currentDispatcher.CheckAccess())
            throw new InvalidOperationException("Application lifecycle must be accessed on the UI thread.");
        if (ReferenceEquals(currentProvider, provider))
            return;
        DetachProvider();
        provider = currentProvider;
        dispatcher = currentDispatcher;
        if (provider is null)
            return;
        snapshot = provider.Snapshot;
        lastActivationKind = provider.LastActivation?.Kind;
        provider.LifecycleChanged += OnLifecycleChanged;
        provider.ActivationReceived += OnActivationReceived;
        provider.StateSaving += OnStateSaving;
        provider.StateRestoring += OnStateRestoring;
    }

    internal void UpdateWindowCounts(int openCount, int activeCount)
    {
        openWindowCount = openCount;
        activeWindowCount = activeCount;
    }

    private void OnLifecycleChanged(object? sender, PlatformApplicationLifecycleEventArgs args)
        => Receive(() =>
        {
            object runtimeIdentity = Application.RuntimeIdentity;
            snapshot = args.Current;
            history.Enqueue(args);
            if (history.Count > HistoryLimit)
                history.Dequeue();
            Exception? observerFailure = null;
            try { Invoke(lifecycleChanged, args); }
            catch (Exception exception) { observerFailure = exception; }
            try
            {
                // A platform exit request must cancel the existing application loop even when an
                // observer fails. A callback can dispose a TestHost and restore a borrowed runtime;
                // the old notification must never exit that restored application.
                if (!disposed && Application.IsCurrentLifecycle(this) &&
                    ReferenceEquals(runtimeIdentity, Application.RuntimeIdentity) &&
                    args.Current.Phase is PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited)
                    Application.Exit();
            }
            catch (Exception exitFailure)
            {
                if (observerFailure is not null)
                    throw new AggregateException("Lifecycle observers and application exit reported failures.", observerFailure, exitFailure);
                throw;
            }
            if (observerFailure is not null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(observerFailure).Throw();
        });

    private void OnActivationReceived(object? sender, PlatformApplicationActivationEventArgs args)
        => Receive(() =>
        {
            lastActivationKind = args.Activation.Kind;
            Invoke(activationReceived, args);
        });

    private void OnStateSaving(object? sender, PlatformApplicationStateSavingEventArgs args)
    {
        // Save is request/response: posting it would return before Data is supplied. Providers
        // therefore must initiate save requests on their verified UI thread.
        if (dispatcher is not null && !dispatcher.CheckAccess())
            throw new InvalidOperationException("Application state-saving requests require the UI thread.");
        if (!disposed)
            Invoke(stateSaving, args);
    }

    private void OnStateRestoring(object? sender, PlatformApplicationStateRestoringEventArgs args)
        => Receive(() => Invoke(stateRestoring, args));

    private void Invoke<T>(EventHandler<T>? handlers, T args) where T : EventArgs
    {
        if (handlers is null)
            return;
        List<Exception>? failures = null;
        foreach (EventHandler<T> handler in handlers.GetInvocationList())
        {
            if (disposed)
                break;
            try { handler(this, args); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        if (failures is { Count: 1 })
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is { Count: > 1 })
            throw new AggregateException("Application lifecycle observers reported failures.", failures);
    }

    private void Receive(Action callback)
    {
        if (disposed)
            return;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            callback();
            return;
        }
        int capturedGeneration = generation;
        dispatcher.Post(() =>
        {
            if (!disposed && generation == capturedGeneration)
                callback();
        });
    }

    private void DetachProvider()
    {
        generation++;
        var previous = provider;
        provider = null;
        dispatcher = null;
        if (previous is null)
            return;
        List<Exception> failures = [];
        Detach(() => previous.LifecycleChanged -= OnLifecycleChanged, failures);
        Detach(() => previous.ActivationReceived -= OnActivationReceived, failures);
        Detach(() => previous.StateSaving -= OnStateSaving, failures);
        Detach(() => previous.StateRestoring -= OnStateRestoring, failures);
        if (failures.Count > 0)
            throw new AggregateException("One or more lifecycle subscriptions could not be detached.", failures);
    }

    internal void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        try { DetachProvider(); }
        finally
        {
            lifecycleChanged = null;
            activationReceived = null;
            stateSaving = null;
            stateRestoring = null;
            history.Clear();
        }
    }

    private static void Detach(Action action, ICollection<Exception> failures)
    {
        try { action(); }
        catch (Exception exception) { failures.Add(exception); }
    }
}
