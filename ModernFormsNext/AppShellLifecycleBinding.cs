using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext;

/// <summary>Owns explicit, bounded subscriptions connecting one shell to an existing lifecycle provider.</summary>
/// <remarks>
/// Create through AppShell.AttachLifecycle and dispose on its UI thread. No service is installed,
/// replaced or disposed. URI activation accepts only the explicit scheme/authority/path base. One
/// latest data notification is coalesced through the existing dispatcher; newer user navigation
/// retires queued data. Background/suspension defers newly received data and cancels in-flight work
/// without automatically replaying it. Retained user history is not reset by state restoration.
/// LastOperation exposes automatic failures, which are observed internally and optionally reported
/// by OperationFailed. Human/native URI transport and persistence remain application/backend concerns.
/// </remarks>
public sealed class AppShellLifecycleBinding : IDisposable
{
    private AppShell? owner;
    private ApplicationLifecycle? lifecycle;
    private IPlatformApplicationLifecycleNotifications? provider;
    private readonly Dispatcher dispatcher;
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private readonly object runtimeIdentity;
    private readonly bool includeState;
    private readonly string basePath;
    private readonly string statePrefix;
    private object? dispatchTicket;
    private DataRequest? pending;
    private ActiveOperation? running;
    private Task<bool>? lastOperation;
    private long generation, lastDataSequence;
    private volatile bool disposed;
    private bool seenPresentation;

    internal AppShellLifecycleBinding(AppShell shell, ApplicationLifecycle facade, Uri? linkBase, string stateKey, bool includeState)
    {
        owner = shell;
        lifecycle = facade;
        if (!Application.IsCurrentLifecycle(facade)) throw new ArgumentException("A shell binding requires the current application lifecycle facade.", nameof(facade));
        provider = facade.NotificationProvider ?? throw new NotSupportedException("The current provider does not supply rich lifecycle notifications.");
        runtimeIdentity = Application.RuntimeIdentity;
        dispatcher = Dispatcher.UIThread;
        this.includeState = includeState;
        StateKey = stateKey;
        statePrefix = "mfn.shell." + stateKey + ".";
        if (linkBase is not null)
        {
            if (!linkBase.IsAbsoluteUri || linkBase.OriginalString.Length > RouteTable.MaximumRouteLength || linkBase.UserInfo.Length != 0 ||
                linkBase.Query.Length != 0 || linkBase.Fragment.Length != 0 || !linkBase.AbsolutePath.EndsWith('/'))
                throw new ArgumentException("A link base must be a bounded absolute URI ending in a slash, without credentials, query or fragment.", nameof(linkBase));
            LinkBase = new Uri(linkBase.OriginalString, UriKind.Absolute);
            basePath = RouteTable.Normalize(LinkBase.AbsolutePath);
        }
        else basePath = "/";
        seenPresentation = shell.IsPresented;
        try
        {
            facade.ActivationReceived += OnActivation;
            facade.StateSaving += OnStateSaving;
            facade.StateRestoring += OnStateRestoring;
            facade.LifecycleChanged += OnLifecycleChanged;
            shell.LifecycleStateChanged += OnShellPresentationChanged;
        }
        catch (Exception original)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException("Lifecycle binding setup and cleanup failed.", original, cleanup); }
            throw;
        }
    }

    /// <summary>Gets the copied explicit activation URI base, or null for state-only integration.</summary>
    public Uri? LinkBase { get; }
    /// <summary>Gets the caller's explicit snapshot namespace.</summary>
    public string StateKey { get; }
    /// <summary>Gets whether this lease was disposed; safe to read from any thread.</summary>
    public bool IsDisposed => disposed;
    /// <summary>Gets the most recent accepted automatic operation, or null before delivery.</summary>
    /// <remarks>
    /// Safe to read from any thread. Await/catch it to observe the outcome. Foreign URI bases and
    /// stale notification sequences do not replace this task. Superseded queued work completes false.
    /// Application factories must honor cancellation cooperatively; disposal cannot stop arbitrary code.
    /// </remarks>
    public Task<bool>? LastOperation => Volatile.Read(ref lastOperation);
    /// <summary>Reports an automatic operation failure on the owning UI thread while this lease is current.</summary>
    /// <remarks>Does not change application state or terminate the runtime. LastOperation remains faulted.</remarks>
    public event EventHandler<ShellLifecycleFailureEventArgs>? OperationFailed;

    private void OnActivation(object? sender, PlatformApplicationActivationEventArgs args)
    {
        if (!Current() || LinkBase is null || args.Activation.Kind is not (PlatformActivationKind.Uri or PlatformActivationKind.Protocol) || args.Activation.Uri is not { } uri) return;
        string? route;
        try { route = MapUri(uri); }
        catch (ArgumentException error) { Receive(args.Sequence, null, null, error); return; }
        catch (UriFormatException error) { Receive(args.Sequence, null, null, error); return; }
        if (route is not null) Receive(args.Sequence, route, null);
    }
    private void OnStateSaving(object? sender, PlatformApplicationStateSavingEventArgs args)
    {
        // Existing data belongs to an earlier application subscriber. Later subscribers can
        // still replace our handoff according to the canonical last-assignment-wins contract.
        if (includeState && Current() && args.Data is null) args.Data = owner!.CaptureState(StateKey);
    }
    private void OnStateRestoring(object? sender, PlatformApplicationStateRestoringEventArgs args)
    {
        if (includeState && Current() && args.Data.Version == AppShell.SavedStateVersion && args.Data.Values.ContainsKey(statePrefix + "count"))
            Receive(args.Sequence, null, args.Data);
    }
    private void OnLifecycleChanged(object? sender, PlatformApplicationLifecycleEventArgs args)
    {
        if (!Current()) return;
        if (args.Current.Phase is PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited)
        {
            pending?.Completion.TrySetResult(false); pending = null;
            Volatile.Read(ref running)?.Cancel();
        }
        else if (args.Current.Phase == PlatformApplicationPhase.Suspended || args.Current.State == PlatformApplicationLifecycleState.Background)
            Volatile.Read(ref running)?.Cancel();
        else TryQueue();
    }
    private void OnShellPresentationChanged(object? sender, EventArgs args)
    {
        if (!Current()) return;
        if (owner!.IsPresented) { seenPresentation = true; TryQueue(); }
        else if (seenPresentation)
        {
            pending?.Completion.TrySetResult(false); pending = null;
            Volatile.Read(ref running)?.Cancel();
        }
    }

    private void Receive(long sequence, string? route, PlatformApplicationStateData? state, Exception? preflightFailure = null)
    {
        if (!Current() || sequence > 0 && sequence <= lastDataSequence) return;
        if (sequence > 0) lastDataSequence = sequence;
        long stamp = ++generation;
        var shell = owner!;
        var request = new DataRequest(stamp, sequence, route, state, preflightFailure,
            shell.UserNavigationVersion, shell.PresentationVersion, shell.IsPresented);
        pending?.Completion.TrySetResult(false); pending = null;
        Volatile.Write(ref lastOperation, request.Completion.Task);
        try { Volatile.Read(ref running)?.Cancel(); }
        catch (Exception error) { CompleteFailure(request, error); return; }
        // Cancellation can call application code. A newer delivery or manual navigation wins.
        if (!Current() || generation != stamp || shell.UserNavigationVersion != request.UserVersion)
        { request.Completion.TrySetResult(false); return; }
        pending = request;
        TryQueue();
    }

    private bool Available()
    {
        if (!Current()) return false;
        var snapshot = lifecycle!.Snapshot;
        return snapshot.Phase is not (PlatformApplicationPhase.Suspended or PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited) &&
            snapshot.State != PlatformApplicationLifecycleState.Background;
    }
    private void TryQueue()
    {
        if (pending is null || dispatchTicket is not null || !Available()) return;
        var ticket = new object();
        dispatchTicket = ticket;
        try { dispatcher.Post(() => RunQueued(ticket)); }
        catch (Exception error)
        {
            if (!ReferenceEquals(dispatchTicket, ticket)) return;
            dispatchTicket = null;
            var rejected = pending; pending = null;
            if (rejected is not null) CompleteFailure(rejected, error);
        }
    }
    private void RunQueued(object ticket)
    {
        if (!ReferenceEquals(dispatchTicket, ticket)) return;
        dispatchTicket = null;
        if (!Current()) { pending?.Completion.TrySetResult(false); pending = null; return; }
        if (!Available() || pending is null) return;
        var request = pending; pending = null;
        var shell = owner!;
        if (request.Generation != generation || request.UserVersion != shell.UserNavigationVersion ||
            seenPresentation && !shell.IsPresented || request.WasPresented && request.PresentationVersion != shell.PresentationVersion)
        { request.Completion.TrySetResult(false); return; }
        if (request.PreflightFailure is { } failure) { CompleteFailure(request, failure); return; }
        var operation = new ActiveOperation(request);
        Volatile.Write(ref running, operation);
        _ = RunAsync(shell, operation);
    }

    private async AnimationCompletion<bool> RunAsync(AppShell shell, ActiveOperation operation)
    {
        var scheduler = AnimationScheduler.Default;
        bool applied = false, canceled = false;
        Exception? failure = null;
        try
        {
            bool CurrentOperation() => Current() && operation.Request.Generation == generation && !(seenPresentation && !shell.IsPresented);
            Task<bool> task = operation.Request.State is { } data
                ? shell.RestoreStateGuarded(data, StateKey, operation.Token, CurrentOperation)
                : shell.GoToGuarded(operation.Request.Route!, true, operation.Token, CurrentOperation);
            applied = await AppShell.Observe(task).On(scheduler);
            if (!HasAccess(scheduler)) throw new InvalidOperationException("The lifecycle binding dispatcher is unavailable.");
            // Disposal/provider retirement may reject admission before the shell needs to throw
            // from its linked token. Preserve this binding operation's cancellation outcome.
            operation.Token.ThrowIfCancellationRequested();
            applied &= CurrentOperation();
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { canceled = true; }
        catch (Exception error) { failure = error; }
        finally
        {
            Interlocked.CompareExchange(ref running, null, operation);
            operation.Complete();
        }
        if (failure is not null) CompleteFailure(operation.Request, failure);
        else if (canceled) operation.Request.Completion.TrySetCanceled(operation.Token);
        else operation.Request.Completion.TrySetResult(applied);
        return applied;
    }

    private void CompleteFailure(DataRequest request, Exception error)
    {
        if (Environment.CurrentManagedThreadId == threadId && Current())
        {
            try { OperationFailed?.Invoke(this, new(error, request.Sequence, request.State is not null)); }
            catch (Exception observer) { error = new AggregateException("Automatic shell delivery and its failure observer failed.", error, observer); }
        }
        request.Completion.TrySetException(error);
        _ = request.Completion.Task.Exception; // Automatic delivery has no async-void/unobserved supervisor.
    }

    private bool Current()
    {
        var facade = lifecycle;
        var shell = owner;
        if (disposed || facade is null || shell is null || shell.IsRetiringShell || !Application.IsCurrentLifecycle(facade) || !ReferenceEquals(Application.RuntimeIdentity, runtimeIdentity)) return false;
        try { return ReferenceEquals(facade.NotificationProvider, provider); }
        catch (ObjectDisposedException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private string? MapUri(Uri uri)
    {
        if (LinkBase is null || !uri.IsAbsoluteUri || uri.UserInfo.Length != 0 ||
            !StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, LinkBase.Scheme) || !StringComparer.OrdinalIgnoreCase.Equals(uri.IdnHost, LinkBase.IdnHost) || uri.Port != LinkBase.Port) return null;
        if (uri.Fragment.Length != 0) throw new ArgumentException("Shell activation URI fragments are not supported.");
        string normalized = RouteTable.Normalize(uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped));
        int queryIndex = normalized.IndexOf('?');
        string path = queryIndex < 0 ? normalized : normalized[..queryIndex];
        string query = queryIndex < 0 ? string.Empty : normalized[queryIndex..];
        if (path == basePath) return "/" + query;
        string prefix = basePath == "/" ? "/" : basePath + "/";
        return path.StartsWith(prefix, StringComparison.Ordinal) ? "/" + path[prefix.Length..] + query : null;
    }
    private static bool HasAccess(AnimationScheduler scheduler) { try { return scheduler.CheckFrameworkAccess(); } catch { return false; } }

    /// <summary>Detaches this lease and cancels its cooperative operation without disposing the shell, facade or provider.</summary>
    /// <remarks>Creating UI thread only; repeated disposal is harmless, including after the borrowed runtime has ended.</remarks>
    public void Dispose()
    {
        if (disposed) return;
        if (Environment.CurrentManagedThreadId != threadId) throw new InvalidOperationException("Lifecycle bindings require their creating UI thread for disposal.");
        disposed = true; generation++; dispatchTicket = null;
        pending?.Completion.TrySetResult(false); pending = null;
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        var operation = Volatile.Read(ref running);
        if (operation is not null) Attempt(operation.Cancel);
        var facade = lifecycle;
        if (facade is not null)
        {
            Attempt(() => facade.ActivationReceived -= OnActivation);
            Attempt(() => facade.StateSaving -= OnStateSaving);
            Attempt(() => facade.StateRestoring -= OnStateRestoring);
            Attempt(() => facade.LifecycleChanged -= OnLifecycleChanged);
        }
        var shell = owner;
        if (shell is not null)
        {
            Attempt(() => shell.LifecycleStateChanged -= OnShellPresentationChanged);
            shell.RemoveLifecycleBinding(this);
        }
        owner = null; lifecycle = null; provider = null;
        OperationFailed = null;
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null) throw new AggregateException("Shell lifecycle subscriptions could not all detach.", failures);
    }

    private sealed record DataRequest(long Generation, long Sequence, string? Route, PlatformApplicationStateData? State,
        Exception? PreflightFailure, long UserVersion, long PresentationVersion, bool WasPresented)
    {
        internal TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class ActiveOperation
    {
        private readonly object sync = new();
        private readonly CancellationTokenSource source = new();
        private bool completed, cancelling;
        internal DataRequest Request { get; }
        internal CancellationToken Token { get; }
        internal ActiveOperation(DataRequest request) { Request = request; Token = source.Token; }
        internal void Cancel()
        {
            lock (sync) { if (completed || cancelling) return; cancelling = true; }
            try { source.Cancel(); }
            finally { lock (sync) { cancelling = false; if (completed) source.Dispose(); } }
        }
        internal void Complete() { lock (sync) { completed = true; if (!cancelling) source.Dispose(); } }
    }
}
