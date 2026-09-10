using System.Runtime.ExceptionServices;

namespace ModernFormsNext.WindowKit.Backend.Lifecycle;

/// <summary>Publishes deterministic lifecycle notifications for one existing platform provider.</summary>
/// <remarks>
/// <para>
/// Mutations and subscription changes require the owning UI thread. Each publication commits its
/// immutable snapshot, invokes all legacy state observers if that state changed, then invokes rich
/// lifecycle observers. Reentrant publications run FIFO after the current notification, never inside
/// it. A failing observer cannot suppress other subscribers or mandatory queued work. After draining,
/// one failure is rethrown with its stack preserved; multiple failures form an aggregate.
/// </para>
/// <para>
/// Equal snapshots and lower host generations are ignored. Exiting accepts only Exited; Exited is
/// permanent. Activation/restoration requests are ignored after shutdown starts. Nonterminal phases
/// are explicit backend/application reports, so unavailable intermediate native states are not invented.
/// Activation requests with identical payloads are still distinct user requests.
/// </para>
/// <para>
/// The owner disposes this publisher on its UI thread. Disposal clears queued payloads, subscribers
/// and the retained activation; it also stops delivery of an in-progress notification. This class
/// owns no native host, timer, process, worker thread or storage operation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var lifecycle = new PlatformApplicationLifecyclePublisher(dispatcher.VerifyAccess);
/// lifecycle.Publish(new PlatformApplicationLifecycleSnapshot(
///     PlatformApplicationPhase.Running, PlatformApplicationLifecycleState.Foreground,
///     isActive: true, hostCount: 1, hostGeneration: 1));
/// </code>
/// </example>
public class PlatformApplicationLifecyclePublisher : IPlatformApplicationLifecycleController, IDisposable
{
    /// <summary>Gets the maximum queued notifications and the maximum operations in one drain.</summary>
    public const int MaximumNotificationsPerDrain = 256;

    private readonly Queue<Action<List<Exception>>> pending = new();
    private Action? verifyAccess;
    private PlatformApplicationLifecycleSnapshot snapshot;
    private PlatformApplicationActivation? lastActivation;
    private EventHandler<PlatformApplicationLifecycleChangedEventArgs>? stateChanged;
    private EventHandler<PlatformApplicationLifecycleEventArgs>? lifecycleChanged;
    private EventHandler<PlatformApplicationActivationEventArgs>? activationReceived;
    private EventHandler<PlatformApplicationStateSavingEventArgs>? stateSaving;
    private EventHandler<PlatformApplicationStateRestoringEventArgs>? stateRestoring;
    private long sequence;
    private bool publishing;
    private volatile bool disposed;

    /// <summary>Creates a publisher for an existing UI thread and optional initial snapshot.</summary>
    /// <param name="verifyAccess">A callback that throws unless invoked on the owning UI thread.</param>
    /// <param name="initialSnapshot">An initial state; null means NotStarted/Unknown with no hosts.</param>
    public PlatformApplicationLifecyclePublisher(
        Action verifyAccess, PlatformApplicationLifecycleSnapshot? initialSnapshot = null)
    {
        this.verifyAccess = verifyAccess ?? throw new ArgumentNullException(nameof(verifyAccess));
        verifyAccess();
        snapshot = initialSnapshot ?? new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.NotStarted, PlatformApplicationLifecycleState.Unknown);
    }

    /// <inheritdoc/>
    /// <remarks>This immutable snapshot can be read from any thread until disposal.</remarks>
    public PlatformApplicationLifecycleSnapshot Snapshot
    {
        get { ThrowIfDisposed(); return Volatile.Read(ref snapshot); }
    }
    /// <inheritdoc/>
    public PlatformApplicationLifecycleState State => Snapshot.State;
    /// <inheritdoc/>
    public bool IsPublishing
    {
        get { VerifyAccess(); return publishing; }
    }
    /// <inheritdoc/>
    /// <remarks>The retained immutable payload can be read from any thread until disposal.</remarks>
    public PlatformApplicationActivation? LastActivation
    {
        get { ThrowIfDisposed(); return Volatile.Read(ref lastActivation); }
    }
    /// <inheritdoc/>
    public event EventHandler<PlatformApplicationLifecycleChangedEventArgs>? StateChanged
    {
        add { VerifyAccess(); stateChanged += value; }
        remove { if (!disposed) { VerifyAccess(); stateChanged -= value; } }
    }
    /// <inheritdoc/>
    public event EventHandler<PlatformApplicationLifecycleEventArgs>? LifecycleChanged
    {
        add { VerifyAccess(); lifecycleChanged += value; }
        remove { if (!disposed) { VerifyAccess(); lifecycleChanged -= value; } }
    }
    /// <inheritdoc/>
    public event EventHandler<PlatformApplicationActivationEventArgs>? ActivationReceived
    {
        add { VerifyAccess(); activationReceived += value; }
        remove { if (!disposed) { VerifyAccess(); activationReceived -= value; } }
    }
    /// <inheritdoc/>
    public event EventHandler<PlatformApplicationStateSavingEventArgs>? StateSaving
    {
        add { VerifyAccess(); stateSaving += value; }
        remove { if (!disposed) { VerifyAccess(); stateSaving -= value; } }
    }
    /// <inheritdoc/>
    public event EventHandler<PlatformApplicationStateRestoringEventArgs>? StateRestoring
    {
        add { VerifyAccess(); stateRestoring += value; }
        remove { if (!disposed) { VerifyAccess(); stateRestoring -= value; } }
    }

    /// <inheritdoc/>
    public void Publish(PlatformApplicationLifecycleSnapshot snapshot)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(snapshot);
        Enqueue(failures =>
        {
            PlatformApplicationLifecycleSnapshot previous = this.snapshot;
            if (previous.Phase == PlatformApplicationPhase.Exited ||
                previous.Phase == PlatformApplicationPhase.Exiting && snapshot.Phase != PlatformApplicationPhase.Exited ||
                snapshot.HostGeneration < previous.HostGeneration || snapshot == previous)
                return;

            Volatile.Write(ref this.snapshot, snapshot);
            long currentSequence = NextSequence();
            // Capture both subscriber sets for this committed transition. Reentrant subscription
            // changes affect subsequent notifications and cannot reorder the current legacy/rich pair.
            var legacy = stateChanged;
            var rich = lifecycleChanged;
            if (previous.State != snapshot.State)
                Invoke(legacy, new PlatformApplicationLifecycleChangedEventArgs(previous.State, snapshot.State), failures);
            Invoke(rich, new PlatformApplicationLifecycleEventArgs(previous, snapshot, currentSequence), failures);
        });
    }

    /// <inheritdoc/>
    public void Activate(PlatformApplicationActivation activation)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(activation);
        Enqueue(failures =>
        {
            if (IsTerminating) return;
            Volatile.Write(ref lastActivation, activation);
            Invoke(activationReceived, new PlatformApplicationActivationEventArgs(activation, NextSequence()), failures);
        });
    }

    /// <inheritdoc/>
    public PlatformApplicationStateData? RequestSaveState(PlatformApplicationStateReason reason)
    {
        VerifyAccess();
        ValidateReason(reason);
        if (publishing)
            throw new InvalidOperationException("Synchronous state saving cannot run reentrantly from a lifecycle notification.");
        PlatformApplicationStateData? result = null;
        Enqueue(failures =>
        {
            if (snapshot.Phase == PlatformApplicationPhase.Exited) return;
            var args = new PlatformApplicationStateSavingEventArgs(reason, NextSequence());
            try { Invoke(stateSaving, args, failures); }
            finally { args.Seal(); result = args.Data; }
        });
        return result;
    }

    /// <inheritdoc/>
    public void RestoreState(PlatformApplicationStateData data, PlatformApplicationStateReason reason)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(data);
        ValidateReason(reason);
        Enqueue(failures =>
        {
            if (IsTerminating) return;
            Invoke(stateRestoring, new PlatformApplicationStateRestoringEventArgs(data, reason, NextSequence()), failures);
        });
    }

    /// <summary>Clears subscriptions, queued notifications and retained activation on the owner UI thread.</summary>
    /// <remarks>Disposal is idempotent. An already disposed publisher permits event unsubscription.</remarks>
    public void Dispose()
    {
        if (disposed) return;
        VerifyAccess();
        disposed = true;
        pending.Clear();
        stateChanged = null;
        lifecycleChanged = null;
        activationReceived = null;
        stateSaving = null;
        stateRestoring = null;
        Volatile.Write(ref lastActivation, null);
        verifyAccess = null;
        GC.SuppressFinalize(this);
    }

    private bool IsTerminating => snapshot.Phase is PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited;
    private long NextSequence() => checked(++sequence);
    private static void ValidateReason(PlatformApplicationStateReason reason)
    {
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    private void VerifyAccess()
    {
        ThrowIfDisposed();
        verifyAccess!();
    }

    private void Enqueue(Action<List<Exception>> notification)
    {
        if (pending.Count >= MaximumNotificationsPerDrain)
            throw new InvalidOperationException("The lifecycle notification queue exceeded its bounded capacity.");
        pending.Enqueue(notification);
        if (publishing) return;
        publishing = true;
        var failures = new List<Exception>();
        int processed = 0;
        try
        {
            while (!disposed && pending.Count > 0)
            {
                if (processed++ == MaximumNotificationsPerDrain)
                {
                    pending.Clear();
                    failures.Add(new InvalidOperationException("Lifecycle notifications replenished the queue beyond its bounded drain."));
                    break;
                }
                try { pending.Dequeue()(failures); }
                catch (Exception exception) { failures.Add(exception); }
            }
        }
        finally { publishing = false; }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Lifecycle observers reported failures after notification cleanup.", failures);
    }

    private void Invoke<T>(EventHandler<T>? handlers, T args, ICollection<Exception> failures) where T : EventArgs
    {
        if (handlers is null) return;
        foreach (EventHandler<T> handler in handlers.GetInvocationList())
        {
            if (disposed) break;
            try { handler(this, args); }
            catch (Exception exception) { failures.Add(exception); }
        }
    }
}
