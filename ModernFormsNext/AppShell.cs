using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext;

/// <summary>Visits instance-registered routes through an ordinary retained navigation page.</summary>
/// <remarks>
/// Create and use on the UI thread. A shell is a Page inside an existing Form or surface, not an
/// application runtime. Absolute logical routes resolve from the route root; both absolute and
/// relative visits push the existing navigation history. Older unfinished route requests are
/// superseded by a newer request, Back or host retirement. A factory result stays caller-owned
/// until canonical page admission. Removal transfers pages without disposal, as NavigationPage does.
/// </remarks>
/// <example><code>
/// var shell = new AppShell();
/// shell.Routes.Register("documents/{id}", route =&gt;
///     new ContentPage { Title = route.Parameters["id"], Content = new TextBox() });
/// form.Controls.Add(new PageHost { Page = shell, Dock = DockStyle.Fill });
/// await shell.GoToAsync("/documents/42?mode=edit");
/// </code></example>
public partial class AppShell : Page
{
    /// <summary>Gets the maximum number of unfinished application factory/navigation requests.</summary>
    /// <remarks>Superseded application tasks must still finish cooperatively before releasing their slot.</remarks>
    public const int MaximumPendingRequests = 128;

    private readonly PageHost host;
    private readonly NavigationPage navigator;
    private readonly Dictionary<Page, RouteContext> visits = new(ReferenceEqualityComparer.Instance);
    private readonly List<Request> requests = [];
    private readonly List<IDisposable> lifecycleBindings = [];
    private Request? active;
    private long version, mountVersion, userIntentVersion;
    private int pendingCount, requestDepth;
    private bool retiring;

    /// <summary>Creates an empty, detached shell with inert routes and no lifecycle subscriptions.</summary>
    public AppShell()
    {
        Routes = new RouteTable();
        host = Controls.AddImplicitControl(new PageHost());
        navigator = new NavigationPage();
        host.Page = navigator;
        navigator.StackChanged += SynchronizeVisits;
        navigator.Navigating += OnNavigatorNavigating;
        navigator.Navigated += OnNavigatorNavigated;
        host.HostChanged += OnHostChanged;
        Routes.Changed += OnRoutesChanged;
        NavigateCommand = new AsyncCommand(async (parameter, token) =>
        {
            if (parameter is not string route) throw new ArgumentException("The route command requires a string parameter.", nameof(parameter));
            await GoToAsync(route, cancellationToken: token).ConfigureAwait(false);
        }, CanNavigate);
        BackCommand = new AsyncCommand(async (_, token) => { await GoBackAsync(cancellationToken: token).ConfigureAwait(false); },
            _ => !retiring && Enabled && (HasPendingAdmission || navigator.CanGoBack));
        var cancelPending = new RoutedCommand("CancelPendingShellRoute");
        CommandBindings.Add(new CommandBinding(cancelPending, (_, args) =>
        {
            args.Handled = true;
            userIntentVersion++;
            RetireRequest();
            UpdateCommands();
        }, (_, args) => args.CanExecute = !retiring && Enabled && HasPendingAdmission));
        // Inner navigation/flyout bindings retain nearest-owner priority. This outer binding
        // consumes Back only when an uncommitted factory has no closer navigation owner.
        InputBindings.Add(new KeyBinding(cancelPending, new KeyGesture(Keys.BrowserBack)));
        InputBindings.Add(new KeyBinding(cancelPending, new KeyGesture(Keys.Left, KeyModifiers.Alt)));
        InputBindings.Add(new KeyBinding(cancelPending, new KeyGesture(Keys.Escape)));
    }

    /// <summary>Gets this shell's owned route table; it is retired with the shell.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RouteTable Routes { get; }

    /// <summary>Gets the actual current page, or null before the first committed visit.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Page? CurrentPage => navigator.CurrentPage;

    /// <summary>Gets immutable data for the current retained visit, or null for an empty shell.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RouteContext? CurrentRoute => CurrentPage is { } page && visits.TryGetValue(page, out var context) ? context : null;

    /// <summary>Gets a borrowed immutable snapshot of the canonical root-to-current page stack.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<Page> NavigationStack => navigator.NavigationStack;

    /// <summary>Gets whether Back can cancel an uncommitted request or reveal a predecessor.</summary>
    public bool CanGoBack => HasPendingAdmission || navigator.CanGoBack;

    /// <summary>Gets whether a current route request or canonical navigation operation is unfinished.</summary>
    public bool IsNavigating => Volatile.Read(ref active) is not null || navigator.IsNavigating;

    /// <summary>Gets the number of unfinished requests, including superseded application factories.</summary>
    public int PendingRequestCount => Volatile.Read(ref pendingCount);

    /// <summary>Gets or sets the canonical incoming transition duration; default 180 milliseconds.</summary>
    /// <remarks>UI thread only. Non-negative and captured when a navigation request starts; shared motion policy still applies.</remarks>
    public TimeSpan TransitionDuration
    {
        get => navigator.TransitionDuration;
        set { VerifyPageAccess(); ObjectDisposedException.ThrowIf(retiring, this); navigator.TransitionDuration = value; }
    }

    /// <summary>Gets the ordinary single-flight route action; its parameter is a logical route string.</summary>
    /// <remarks>Direct GoToAsync calls can supersede work. ExecutionTask observes command failures.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AsyncCommand NavigateCommand { get; }

    /// <summary>Gets the ordinary Back action, which remains available during a pending factory.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AsyncCommand BackCommand { get; }

    /// <summary>Occurs after canonical stack commitment and route metadata synchronization.</summary>
    /// <remarks>Callbacks may navigate again. Exceptions do not undo committed page ownership.</remarks>
    public event EventHandler? CurrentRouteChanged;

    /// <summary>Forwards canonical navigation completion and any transferred pages.</summary>
    /// <remarks>RemovedPages are borrowed transferred references; application code decides reuse or disposal.</remarks>
    public event EventHandler<NavigationChangedEventArgs>? Navigated;

    /// <summary>Resolves a logical route and explicitly invokes its borrowed page factory.</summary>
    /// <param name="route">An absolute path or a path relative to the entire current logical path.</param>
    /// <param name="animated">Whether the canonical navigator uses its incoming appearance transition.</param>
    /// <param name="cancellationToken">Cooperative cancellation; committed pages are never rolled back.</param>
    /// <returns>True when the visit remains current; false after veto or supersession.</returns>
    /// <remarks>
    /// UI thread only. Unknown/invalid routes throw before replacing current work. Reaffirming the
    /// exact canonical current route supersedes older work without invoking another factory. Factories
    /// must return a non-null detached page; unadmitted or stale results are not disposed by the shell.
    /// Application callback failures fault the task. Precommit caller cancellation cancels it; after
    /// commitment cancellation only settles the existing visual effect. At most 32 nested calls are admitted.
    /// </remarks>
    public Task<bool> GoToAsync(string route, bool animated = true, CancellationToken cancellationToken = default)
        => GoToCore(route, animated, cancellationToken, null);

    internal Task<bool> GoToGuarded(string route, bool animated, CancellationToken token, Func<bool> canContinue)
        => GoToCore(route, animated, token, canContinue);

    private Task<bool> GoToCore(string route, bool animated, CancellationToken cancellationToken, Func<bool>? canContinue)
    {
        VerifyPageAccess();
        ObjectDisposedException.ThrowIf(retiring, this);
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<bool>(cancellationToken);
        if (!(canContinue?.Invoke() ?? true)) return Task.FromResult(false);
        if (requestDepth >= 32) throw new InvalidOperationException("Shell route callbacks did not settle after 32 nested calls.");
        if (PendingRequestCount >= MaximumPendingRequests) throw new InvalidOperationException("Too many application route requests are unfinished.");
        requestDepth++;
        try
        {
            var resolved = Routes.Resolve(route, CurrentRoute?.Route);
            userIntentVersion++;
            long intent = RetireRequest();
            // Cancellation invokes application callbacks. Their newer visit owns the shell;
            // do not reserve another version after returning and overwrite that decision.
            if (retiring || version != intent || !resolved.Registration.Active) return Task.FromResult(false);
            if (StringComparer.Ordinal.Equals(CurrentRoute?.Route, resolved.Context.Route))
            { UpdateCommands(); return Task.FromResult(!retiring && version == intent && StringComparer.Ordinal.Equals(CurrentRoute?.Route, resolved.Context.Route)); }
            requests.RemoveAll(request => request.Completed);
            var request = new Request(intent, mountVersion, resolved, animated, cancellationToken, canContinue);
            requests.Add(request);
            Interlocked.Increment(ref pendingCount);
            Volatile.Write(ref active, request);
            try { UpdateCommands(); }
            catch (Exception error)
            {
                FinishRequest(request);
                request.Completion.TrySetException(error);
                return request.Completion.Task;
            }
            _ = VisitAsync(request);
            return request.Completion.Task;
        }
        finally { requestDepth--; }
    }

    /// <summary>Cancels older uncommitted route work, then attempts canonical Back navigation.</summary>
    /// <param name="animated">Whether to animate a revealed predecessor.</param>
    /// <param name="cancellationToken">Canonical navigation cancellation.</param>
    /// <returns>The transferred page, or null when only a pending factory was canceled, at root, or after veto.</returns>
    /// <remarks>UI thread only. The application owns a returned page and may reuse or dispose it.</remarks>
    public Task<Page?> GoBackAsync(bool animated = true, CancellationToken cancellationToken = default)
    {
        VerifyPageAccess();
        ObjectDisposedException.ThrowIf(retiring, this);
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<Page?>(cancellationToken);
        if (requestDepth >= 32) throw new InvalidOperationException("Shell Back callbacks did not settle after 32 nested calls.");
        requestDepth++;
        try
        {
            userIntentVersion++;
            long intent = RetireRequest();
            if (retiring || version != intent) return Task.FromResult<Page?>(null);
            UpdateCommands();
            if (retiring || version != intent) return Task.FromResult<Page?>(null);
            return navigator.PopAsync(animated, cancellationToken);
        }
        finally { requestDepth--; }
    }

    private async AnimationCompletion<bool> VisitAsync(Request request)
    {
        var scheduler = AnimationScheduler.Default;
        Exception? failure = null;
        Page? target = null;
        bool applied = false, canceled = false;
        try
        {
            var factory = request.Resolved.Registration.Factory;
            if (IsCurrent(request) && factory is not null)
            {
                Page page = await InvokeFactory(factory, request.Resolved.Context, request.Token).On(scheduler);
                if (!HasAccess(scheduler)) throw new InvalidOperationException("The shell UI dispatcher is unavailable.");
                if (IsCurrent(request) && request.Resolved.Registration.Active)
                {
                    request.Token.ThrowIfCancellationRequested();
                    if (page is null) throw new InvalidOperationException("A route factory returned a null page.");
                    target = page;
                    Volatile.Write(ref request.Candidate, page);
                    bool accepted = await Observe(navigator.PushAsync(page, request.Animated, request.Token)).On(scheduler);
                    if (!HasAccess(scheduler)) throw new InvalidOperationException("The shell UI dispatcher is unavailable.");
                    applied = accepted && IsCurrent(request) && ReferenceEquals(CurrentPage, page);
                }
            }
        }
        catch (OperationCanceledException error)
        {
            if (request.CallerToken.IsCancellationRequested && !request.Committed) canceled = true;
            else if (!request.Token.IsCancellationRequested) failure = error;
        }
        catch (Exception error) { failure = error; }
        finally
        {
            FinishRequest(request);
            if (HasAccess(scheduler) && !retiring)
                try { UpdateCommands(); } catch (Exception error) { failure ??= error; }
        }
        // Completion observers can synchronously navigate or retire the just-committed page.
        // Publish the result only after those callbacks, using the actual canonical current page.
        if (applied) applied = HasAccess(scheduler) && IsCurrent(request) && ReferenceEquals(CurrentPage, target);
        if (failure is not null) request.Completion.TrySetException(failure);
        else if (canceled) request.Completion.TrySetCanceled(request.CallerToken);
        else request.Completion.TrySetResult(applied);
        return applied;
    }

    private static async AnimationCompletion<Page> InvokeFactory(Func<RouteContext, CancellationToken, Task<Page>> factory, RouteContext context, CancellationToken token)
        => await (factory(context, token) ?? throw new InvalidOperationException("A route factory returned a null task.")).ConfigureAwait(false);
    internal static async AnimationCompletion<T> Observe<T>(Task<T> task) => await task.ConfigureAwait(false);
    private bool IsCurrent(Request request)
    {
        bool Reserved() => !retiring && request.Version == version && request.MountVersion == mountVersion;
        // A borrowed lifecycle guard may rebind an application provider. Recheck the reservation
        // after it, just as after other application-controlled callbacks.
        return Reserved() && (request.CanContinue?.Invoke() ?? true) && Reserved();
    }
    private bool HasPendingAdmission => Volatile.Read(ref active) is { Completed: false, Committed: false };

    private long RetireRequest()
    {
        long intent = ++version;
        var previous = Interlocked.Exchange(ref active, null);
        previous?.Cancel();
        return intent;
    }

    private void FinishRequest(Request request)
    {
        Interlocked.CompareExchange(ref active, null, request);
        Volatile.Write(ref request.Candidate, null);
        request.Complete();
        Interlocked.Decrement(ref pendingCount);
    }

    private void SynchronizeVisits(object? sender, EventArgs args)
    {
        if (retiring) return;
        var stack = navigator.NavigationStack;
        foreach (var page in visits.Keys.ToArray())
            if (!stack.Any(retained => ReferenceEquals(retained, page))) visits.Remove(page);
        foreach (var page in stack)
            foreach (var request in requests)
                if (ReferenceEquals(Volatile.Read(ref request.Candidate), page))
                { request.Committed = true; visits[page] = request.Resolved.Context; break; }
        UpdateCommands();
        CurrentRouteChanged?.Invoke(this, EventArgs.Empty);
    }
    private void OnNavigatorNavigating(object? sender, NavigationChangingEventArgs args)
    {
        if (args.Kind != NavigationKind.Push)
        {
            userIntentVersion++;
            if (active is not null) RetireRequest();
        }
    }
    private void OnNavigatorNavigated(object? sender, NavigationChangedEventArgs args) => Navigated?.Invoke(this, args);
    private void OnHostChanged() { if (!retiring) { mountVersion++; RetireRequest(); UpdateCommands(); } }
    private void OnRoutesChanged()
    {
        if (active is { } request && !request.Resolved.Registration.Active) RetireRequest();
        UpdateCommands();
    }
    private bool CanNavigate(object? parameter)
    {
        if (retiring || !Enabled || parameter is not string route) return false;
        try { Routes.Match(route, CurrentRoute?.Route); return true; }
        catch (ArgumentException) { return false; }
        catch (KeyNotFoundException) { return false; }
    }
    private void UpdateCommands()
    {
        if (retiring) return;
        Exception? failure = null;
        try { NavigateCommand?.RaiseCanExecuteChanged(); } catch (Exception error) { failure = error; }
        try { if (!retiring) BackCommand?.RaiseCanExecuteChanged(); } catch (Exception error) { failure ??= error; }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static bool HasAccess(AnimationScheduler scheduler) { try { return scheduler.CheckFrameworkAccess(); } catch { return false; } }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(600, 400);
    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); if (host is not null && !retiring) host.Bounds = LogicalPaddedClientRectangle; }
    /// <inheritdoc/>
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!retiring) UpdateCommands(); }
    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new ShellAccessibleObject(this);
    private sealed class ShellAccessibleObject(AppShell owner) : ControlAccessibleObject(owner)
    {
        internal override bool HasViewVisibility => true;
        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (Owner is AppShell { retiring: false } shell) yield return shell.host.AccessibilityObject;
            foreach (var child in base.GetAccessibilityChildren()) yield return child;
        }
    }
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (retiring) return;
        retiring = true; version++; mountVersion++;
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        var previous = Interlocked.Exchange(ref active, null);
        if (previous is not null) Attempt(previous.Cancel);
        foreach (var binding in lifecycleBindings.ToArray()) Attempt(binding.Dispose);
        lifecycleBindings.Clear();
        Routes.Changed -= OnRoutesChanged;
        Attempt(Routes.Retire);
        navigator.StackChanged -= SynchronizeVisits;
        navigator.Navigating -= OnNavigatorNavigating;
        navigator.Navigated -= OnNavigatorNavigated;
        host.HostChanged -= OnHostChanged;
        visits.Clear();
        requests.Clear();
        Attempt(() => base.Dispose(true));
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null) throw new AggregateException("Shell cleanup failed.", failures);
    }

    private sealed class Request
    {
        private readonly object sync = new();
        private readonly CancellationTokenSource source;
        private bool cancelling;
        internal readonly long Version, MountVersion;
        internal readonly RouteTable.ResolvedRoute Resolved;
        internal readonly bool Animated;
        internal readonly CancellationToken CallerToken;
        internal readonly Func<bool>? CanContinue;
        internal readonly TaskCompletionSource<bool> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Page? Candidate;
        internal volatile bool Committed, Completed;
        internal CancellationToken Token { get; }

        internal Request(long version, long mountVersion, RouteTable.ResolvedRoute resolved, bool animated, CancellationToken callerToken, Func<bool>? canContinue)
        {
            Version = version; MountVersion = mountVersion; Resolved = resolved; Animated = animated;
            CallerToken = callerToken;
            CanContinue = canContinue;
            source = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            Token = source.Token;
        }

        // Cancellation invokes borrowed application callbacks outside the lock. Completion may
        // arrive on another thread, so disposal waits until a concurrent Cancel has unwound.
        internal void Cancel()
        {
            lock (sync) { if (Completed || cancelling) return; cancelling = true; }
            try { source.Cancel(); }
            finally { lock (sync) { cancelling = false; if (Completed) source.Dispose(); } }
        }
        internal void Complete()
        {
            lock (sync) { Completed = true; if (!cancelling) source.Dispose(); }
        }
    }
}
