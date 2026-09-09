using System.Diagnostics;
using System.Threading.Channels;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.Automation;

public sealed partial class AutomationSession
{
    private event Action? lifetimeChanged;
    private int activeWaits;

    /// <summary>Waits for a current semantic predicate using canonical wake hints and bounded reconciliation.</summary>
    /// <param name="rootId">The exact registration identity; an ended registration satisfies only RootEnded.</param>
    /// <param name="condition">One handle/query predicate, or a root lifetime predicate.</param>
    /// <param name="options">Immutable timeout/reconciliation bounds; null selects defaults.</param>
    /// <param name="cancellationToken">Cancels queued reads and waiting; returns Cancelled after UI subscription cleanup.</param>
    /// <returns>Detached evidence of satisfaction or an explicit unsuccessful status.</returns>
    /// <remarks>
    /// Safe to call from any thread; await without blocking the owning dispatcher. At most 64 waits
    /// can observe a session concurrently. The initial read has no delay. Observation is registered
    /// on the UI thread before a second read, so a change between the initial read and subscription
    /// cannot be lost. Notifications only trigger fresh reads; missing notifications are reconciled.
    /// Individual application getters cannot be preempted. Dispose the session before its dispatcher.
    /// Redacted values cannot be tested for equality. Accepted actions do not imply completed commands.
    /// </remarks>
    /// <example><code>
    /// var completed = await automation.WaitForConditionAsync(root.RootId, new()
    /// {
    ///     Kind = AutomationWaitKind.ValueEquals,
    ///     Query = new() { AutomationId = "status" }, Value = "Completed"
    /// });
    /// </code></example>
    public async Task<AutomationWaitResult> WaitForConditionAsync(string rootId, AutomationWaitCondition condition,
        AutomationWaitOptions? options = null, CancellationToken cancellationToken = default)
    {
        long start = Stopwatch.GetTimestamp();
        options ??= new();
        AutomationWaitResult End(AutomationWaitStatus status, AutomationErrorCode error = AutomationErrorCode.None,
            AutomationNodeSnapshot? snapshot = null) => new(status, error, snapshot, Stopwatch.GetElapsedTime(start));
        if (!Guid.TryParseExact(rootId, "N", out _) || condition is null || !Enum.IsDefined(condition.Kind)
            || (condition.Kind == AutomationWaitKind.RootEnded ? condition.Handle is not null || condition.Query is not null
                : (condition.Handle is null) == (condition.Query is null))
            || condition.Value is { Length: > SemanticTraversal.MaxTextLength }
            || (condition.Kind == AutomationWaitKind.ValueEquals && condition.Value is null)
            || options.Timeout < TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(60)
            || options.ReconciliationInterval < TimeSpan.FromMilliseconds(20) || options.ReconciliationInterval > TimeSpan.FromSeconds(1))
            return End(AutomationWaitStatus.Failed, AutomationErrorCode.InvalidArgument);

        WaitObservation? observation = null;
        using var deadline = new CancellationTokenSource();
        // Zero timeout still admits the initial check, without scheduling any observation/timer.
        if (options.Timeout > TimeSpan.Zero) deadline.CancelAfter(options.Timeout);
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            var evaluated = await EvaluateWait(rootId, condition, combined.Token).ConfigureAwait(false);
            if (evaluated is not null) return evaluated with { Elapsed = Stopwatch.GetElapsedTime(start) };
            if (options.Timeout == TimeSpan.Zero) return End(AutomationWaitStatus.TimedOut);
            observation = await OnWaitDispatcher(() =>
            {
                if (activeWaits >= 64) return null;
                var value = new WaitObservation(this);
                activeWaits++;
                return value;
            }).ConfigureAwait(false);
            if (observation is null) return End(AutomationWaitStatus.Failed, AutomationErrorCode.LimitExceeded);
            while (true)
            {
                combined.Token.ThrowIfCancellationRequested();
                // Subscribe before reading again, on the same dispatcher. Rebuild only this wait's
                // weak observation set from the existing bounded traversal, never a node index.
                await OnWaitDispatcher(() =>
                {
                    observation.Refresh(rootId, condition.Handle is null && condition.Query is not null
                        ? AutomationCapability.Query : AutomationCapability.Inspect, combined.Token);
                    return true;
                }).ConfigureAwait(false);
                evaluated = await EvaluateWait(rootId, condition, combined.Token).ConfigureAwait(false);
                if (evaluated is not null) return evaluated with { Elapsed = Stopwatch.GetElapsedTime(start) };
                TimeSpan remaining = options.Timeout - Stopwatch.GetElapsedTime(start);
                if (remaining <= TimeSpan.Zero) return End(AutomationWaitStatus.TimedOut);
                await observation.WaitAsync(remaining < options.ReconciliationInterval ? remaining : options.ReconciliationInterval,
                    combined.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (combined.IsCancellationRequested)
        {
            return End(cancellationToken.IsCancellationRequested ? AutomationWaitStatus.Cancelled : AutomationWaitStatus.TimedOut);
        }
        catch (Exception) { return End(AutomationWaitStatus.Failed, AutomationErrorCode.ApplicationError); }
        finally
        {
            if (observation is not null)
                await OnWaitDispatcher(() => { observation.Dispose(); activeWaits--; return true; }).ConfigureAwait(false);
        }
    }

    /// <summary>Queues a normal-priority dispatcher turn and completes after that turn is processed.</summary>
    /// <param name="cancellationToken">Cancels the queued checkpoint; cancellation throws OperationCanceledException.</param>
    /// <returns>None when processed or SessionEnded if the semantic session has ended.</returns>
    /// <remarks>
    /// Always queues, even on the UI thread. Await without blocking that thread. Earlier normal-priority
    /// dispatcher work runs first; future timers, network, rendering/GPU work and arbitrary Tasks are
    /// outside this guarantee. This is not global application idle or AsyncCommand completion.
    /// </remarks>
    public Task<AutomationErrorCode> CheckpointAsync(CancellationToken cancellationToken = default)
        => dispatcher.InvokeAsync(() => stopped ? AutomationErrorCode.SessionEnded : AutomationErrorCode.None,
            DispatcherPriority.Default, cancellationToken).GetTask();

    private Task<T> OnWaitDispatcher<T>(Func<T> action)
        => dispatcher.CheckAccess() ? Task.FromResult(dispatcher.Invoke(action))
            : dispatcher.InvokeAsync(action).GetTask();

    private async Task<AutomationWaitResult?> EvaluateWait(string rootId, AutomationWaitCondition condition, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var rootError = await OnWaitDispatcher(() => ResolveRoot(rootId,
            condition.Handle is null && condition.Query is not null ? AutomationCapability.Query : AutomationCapability.Inspect, out _)).ConfigureAwait(false);
        if (condition.Kind == AutomationWaitKind.RootEnded)
        {
            return rootError == AutomationErrorCode.StaleNode ? new(AutomationWaitStatus.Satisfied, AutomationErrorCode.None, null, default)
                : rootError == AutomationErrorCode.None ? null : WaitFailure(rootError);
        }
        if (rootError == AutomationErrorCode.StaleNode) return new(AutomationWaitStatus.RootEnded, rootError, null, default);
        if (rootError != AutomationErrorCode.None) return WaitFailure(rootError);
        var result = condition.Handle is { } handle
            ? await InspectAsync(rootId, handle, token).ConfigureAwait(false)
            : await FindOneAsync(rootId, condition.Query!, token).ConfigureAwait(false);
        if (result.Error is AutomationErrorCode.NodeNotFound or AutomationErrorCode.NodeUnavailable)
            return condition.Kind == AutomationWaitKind.NodeNotExposed
                ? new(AutomationWaitStatus.Satisfied, AutomationErrorCode.None, null, default) : null;
        if (result.Error != AutomationErrorCode.None) return WaitFailure(result.Error);
        var node = result.Value!;
        if (condition.Kind == AutomationWaitKind.ValueEquals && node.Redaction != AutomationRedaction.None)
            return WaitFailure(AutomationErrorCode.CapabilityDenied);
        bool satisfied = condition.Kind switch
        {
            AutomationWaitKind.NodeExists or AutomationWaitKind.Exposed => true,
            AutomationWaitKind.Enabled => (node.States & AccessibleStates.Unavailable) == 0,
            AutomationWaitKind.Focused => (node.States & AccessibleStates.Focused) != 0,
            AutomationWaitKind.Selected => (node.States & AccessibleStates.Selected) != 0,
            AutomationWaitKind.ValueEquals => string.Equals(node.Value, condition.Value, StringComparison.Ordinal),
            AutomationWaitKind.StateContains => (node.States & condition.States) == condition.States,
            _ => false
        };
        return satisfied ? new(AutomationWaitStatus.Satisfied, AutomationErrorCode.None, node, default) : null;
    }

    private static AutomationWaitResult WaitFailure(AutomationErrorCode error)
        => new(error == AutomationErrorCode.SessionEnded ? AutomationWaitStatus.SessionEnded
            : AutomationWaitStatus.Failed, error, null, default);

    private sealed class WaitObservation : IDisposable
    {
        private readonly AutomationSession session;
        private readonly List<WeakReference<AccessibleObject>> peers = [];
        private readonly Channel<bool> changed = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
            { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

        internal WaitObservation(AutomationSession session)
        {
            this.session = session;
            session.lifetimeChanged += Wake;
        }

        internal void Refresh(string rootId, AutomationCapability capability, CancellationToken token)
        {
            ClearPeers();
            if (session.Begin(rootId, capability, token, out _, out var traversal) != AutomationErrorCode.None) return;
            foreach (var entry in traversal!.Entries)
            {
                entry.Peer.ClientNotification += OnNotification;
                peers.Add(new(entry.Peer));
            }
        }

        private void OnNotification(object? sender, AccessibleObjectNotificationEventArgs e) => Wake();
        private void Wake() => changed.Writer.TryWrite(true);

        internal async Task WaitAsync(TimeSpan interval, CancellationToken token)
        {
            using var reconcile = CancellationTokenSource.CreateLinkedTokenSource(token);
            reconcile.CancelAfter(interval);
            try { await changed.Reader.ReadAsync(reconcile.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
        }

        private void ClearPeers()
        {
            foreach (var weak in peers)
                if (weak.TryGetTarget(out var peer)) peer.ClientNotification -= OnNotification;
            peers.Clear();
        }

        public void Dispose() { ClearPeers(); session.lifetimeChanged -= Wake; changed.Writer.TryComplete(); }
    }
}
