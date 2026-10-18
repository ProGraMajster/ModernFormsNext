using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext;

/// <summary>Retains an ordered stack of pages and presents its current entry in one real page host.</summary>
/// <remarks>
/// Create and navigate on the UI thread. Operations serialize in FIFO order, including requests
/// made by callbacks; at most 128 requests may wait. A pushed page must be detached and unique.
/// Pop transfers removed pages without disposal; disposing this navigator disposes retained pages.
/// Native window closing is unchanged. BrowserBack and Alt+Left use scoped input bindings; Android
/// system Back enters that same route. Focus must be inside this navigator for its binding to win.
/// Detached constructors do not start native windows, timers, transitions or appearance events.
/// </remarks>
/// <example><code>
/// var navigation = new NavigationPage(new ContentPage { Title = "Home" });
/// form.Controls.Add(new PageHost { Dock = DockStyle.Fill, Page = navigation });
/// await navigation.PushAsync(new ContentPage { Title = "Details" });
/// var removed = await navigation.PopAsync(); // Caller now owns the removed page.
/// removed?.Dispose();
/// </code></example>
public class NavigationPage : Page
{
    private const int PendingLimit = 128;
    private readonly PageHost host;
    private readonly List<Page> stack = [];
    private readonly Queue<Request> pending = new();
    private readonly RoutedCommand backInput = new("NavigationBackInput");
    private Request? active;
    private CancellationTokenSource? activeLifetime;
    private bool draining, retiring;
    private long lifetime;
    private TimeSpan transitionDuration = TimeSpan.FromMilliseconds(180);

    /// <summary>Creates an empty stack; its first successful push becomes the retained root.</summary>
    public NavigationPage()
    {
        host = Controls.AddImplicitControl(new PageHost { TabStop = false });
        host.PageLost += OnPageLost;
        host.HostChanged += OnHostChanged;
        BackCommand = new AsyncCommand(async (_, token) => { await PopAsync(cancellationToken: token).ConfigureAwait(false); },
            _ => !retiring && !IsNavigating && CanGoBack);
        CommandBindings.Add(new CommandBinding(backInput, (_, e) =>
        {
            // A second Back during a transition must be consumed, not fall through to the
            // native window-close fallback. The ordinary AsyncCommand owns single-flight work.
            e.Handled = true;
            if (!IsNavigating) BackCommand.ExecuteCore(null);
        }, (_, e) => e.CanExecute = !retiring && (CanGoBack || IsNavigating)));
        InputBindings.Add(new KeyBinding(backInput, new KeyGesture(Keys.BrowserBack)));
        InputBindings.Add(new KeyBinding(backInput, new KeyGesture(Keys.Left, KeyModifiers.Alt)));
    }

    /// <summary>Creates a stack owning one detached root without presenting it yet.</summary>
    /// <param name="rootPage">The non-null detached root, retained until external removal or disposal.</param>
    public NavigationPage(Page rootPage) : this()
    {
        ArgumentNullException.ThrowIfNull(rootPage);
        if (host.PreparePage(rootPage))
        {
            stack.Add(rootPage);
            host.PresentPage(rootPage);
        }
    }

    /// <summary>Gets an immutable snapshot of retained entries, from root to current.</summary>
    /// <remarks>UI thread only. The pages are borrowed references, not cloned or frozen controls.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<Page> NavigationStack => Array.AsReadOnly(stack.ToArray());

    /// <summary>Gets the retained root, or null for an empty stack.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Page? RootPage => stack.Count == 0 ? null : stack[0];
    /// <summary>Gets the current retained entry, or null for an empty stack.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Page? CurrentPage => stack.Count == 0 ? null : stack[^1];
    /// <summary>Gets whether at least one predecessor can be revealed.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanGoBack => stack.Count > 1;
    /// <summary>Gets whether a navigation request or its visual completion currently owns the queue.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsNavigating => active is not null;
    /// <summary>Gets the ordinary single-flight Back action for buttons and application observation.</summary>
    /// <remarks>Its ExecutionTask exposes faults; ICommand execution uses the existing dispatcher exception path.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AsyncCommand BackCommand { get; }

    /// <summary>Gets or sets the incoming fade duration, in unscaled monotonic time; default 180 milliseconds.</summary>
    /// <remarks>
    /// UI thread only. Non-negative; zero is immediate. The shared scheduler applies reduced motion
    /// and platform lifecycle policy. Changes affect later requests. Authored page opacity is retained.
    /// </remarks>
    public TimeSpan TransitionDuration
    {
        get => transitionDuration;
        set { VerifyPageAccess(); ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero); transitionDuration = value; }
    }

    /// <summary>Occurs before validation or stack commitment; Cancel leaves the old stack unchanged.</summary>
    public event EventHandler<NavigationChangingEventArgs>? Navigating;
    /// <summary>Occurs after a committed operation's visual completion has settled.</summary>
    /// <remarks>A callback failure faults that operation's task without rolling back committed ownership.</remarks>
    public event EventHandler<NavigationChangedEventArgs>? Navigated;
    /// <summary>Occurs after retained history changes, including external page removal or disposal.</summary>
    /// <remarks>State commits before callbacks. Observers must not assume a later callback preserves that state.</remarks>
    public event EventHandler? StackChanged;

    /// <summary>Queues a detached page above the current entry.</summary>
    /// <param name="page">The non-null page; duplicate retained or pending pushes are rejected.</param>
    /// <param name="animated">Whether to use the duration captured when this request starts.</param>
    /// <param name="cancellationToken">Cancels before commitment; after commitment only stops the visual effect.</param>
    /// <returns>True if the requested page remained current through completion; false after veto or supersession.</returns>
    /// <remarks>UI thread only. Callback exceptions fault the task; committed changes are not rewound.</remarks>
    public Task<bool> PushAsync(Page page, bool animated = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Enqueue(NavigationKind.Push, page, animated, cancellationToken, static result => result.Applied);
    }

    /// <summary>Queues removal of the current page, retaining at least the root.</summary>
    /// <param name="animated">Whether to animate the revealed predecessor.</param>
    /// <param name="cancellationToken">Precommit cancellation; after commitment it only stops the visual effect.</param>
    /// <returns>The transferred page, or null for an empty/root stack, a veto or precommit supersession.</returns>
    /// <remarks>UI thread only. The caller owns a returned page and decides whether to reuse or dispose it.</remarks>
    public Task<Page?> PopAsync(bool animated = true, CancellationToken cancellationToken = default)
    {
        return Enqueue(NavigationKind.Pop, null, animated, cancellationToken, static result => result.Removed.FirstOrDefault());
    }

    /// <summary>Queues removal of all entries above the retained root.</summary>
    /// <param name="animated">Whether to animate the revealed root.</param>
    /// <param name="cancellationToken">Precommit cancellation; after commitment it only stops the visual effect.</param>
    /// <returns>A read-only list of transferred pages, in former root-to-current order; empty on veto/no change.</returns>
    /// <remarks>UI thread only. Removed pages are detached without disposal; the caller owns them.</remarks>
    public Task<IReadOnlyList<Page>> PopToRootAsync(bool animated = true, CancellationToken cancellationToken = default)
    {
        return Enqueue(NavigationKind.PopToRoot, null, animated, cancellationToken, static result => result.Removed);
    }

    private Task<T> Enqueue<T>(NavigationKind kind, Page? page, bool animated, CancellationToken token, Func<NavigationResult, T> selectResult)
    {
        VerifyPageAccess();
        ObjectDisposedException.ThrowIf(retiring, this);
        if (token.IsCancellationRequested) return Task.FromCanceled<T>(token);
        if (pending.Count >= PendingLimit) throw new InvalidOperationException("The navigation queue is full (128 pending requests).");
        if (page is not null)
        {
            if (stack.Contains(page) || ReferenceEquals(active?.Page, page) || pending.Any(request => ReferenceEquals(request.Page, page)))
                throw new ArgumentException("The page is already retained or has a pending push.", nameof(page));
            host.ValidatePage(page);
        }
        var request = new Request<T>(kind, page, animated, token, lifetime, selectResult);
        pending.Enqueue(request);
        if (!draining) _ = DrainAsync();
        return request.Completion.Task;
    }

    private async AnimationCompletion<bool> DrainAsync()
    {
        var scheduler = AnimationScheduler.Default;
        draining = true;
        try
        {
            while (pending.TryDequeue(out var request))
            {
                active = request;
                NavigationResult result = NavigationResult.Empty;
                Exception? failure = null;
                try
                {
                    if (retiring || request.Lifetime != lifetime) throw new OperationCanceledException("The navigation host changed.");
                    activeLifetime = new();
                    NotifyBack();
                    result = await ApplyAsync(request, activeLifetime.Token, scheduler).On(scheduler);
                }
                catch (Exception error) { failure = error; }
                finally
                {
                    activeLifetime?.Dispose(); activeLifetime = null;
                    active = null;
                }
                // A rejected dispatcher resumes private awaits only to fault/unwind. Release
                // task reservations without touching controls or invoking UI observers there.
                if (!HasUiAccess(scheduler))
                {
                    failure ??= new InvalidOperationException("The navigation dispatcher is unavailable.");
                    request.Complete(result, failure);
                    while (pending.TryDequeue(out var abandoned)) abandoned.Complete(NavigationResult.Empty, failure);
                    return false;
                }
                try { NotifyBack(); }
                catch (Exception error) { failure ??= error; }
                request.Complete(result, failure);
            }
        }
        finally { draining = false; }
        return true;
    }

    private async AnimationCompletion<NavigationResult> ApplyAsync(Request request, CancellationToken hostToken, AnimationScheduler scheduler)
    {
        request.Token.ThrowIfCancellationRequested();
        var previous = CurrentPage;
        var next = request.Kind == NavigationKind.Push ? request.Page : stack.Count > 1
            ? request.Kind == NavigationKind.Pop ? stack[^2] : stack[0] : null;
        if (next is null) return NavigationResult.Empty;
        if (request.Kind == NavigationKind.Push) host.ValidatePage(next);
        var before = stack.ToArray();
        bool IsCurrent() => !retiring && lifetime == request.Lifetime && before.SequenceEqual(stack) && !hostToken.IsCancellationRequested;
        var args = new NavigationChangingEventArgs(request.Kind, previous, next);
        Navigating?.Invoke(this, args);
        request.Token.ThrowIfCancellationRequested();
        hostToken.ThrowIfCancellationRequested();
        if (args.Cancel || !IsCurrent()) return NavigationResult.Empty;
        bool restoreFocus = false, ready = false;
        List<Exception>? admissionFailures = null;
        try
        {
            if (host.AdmitDeparture(previous, out restoreFocus) && IsCurrent())
            {
                request.Token.ThrowIfCancellationRequested();
                if (request.Kind != NavigationKind.Push || host.PreparePage(next))
                {
                    request.Token.ThrowIfCancellationRequested();
                    hostToken.ThrowIfCancellationRequested();
                    ready = IsCurrent() && host.Owns(next);
                }
            }
        }
        catch (Exception error) { admissionFailures = [error]; }
        if (!ready)
        {
            void Cleanup(Action action) { try { action(); } catch (Exception error) { (admissionFailures ??= []).Add(error); } }
            if (request.Kind == NavigationKind.Push && host.Owns(next) && !stack.Contains(next)) Cleanup(() => host.ReleasePage(next));
            if (restoreFocus) Cleanup(host.RestorePageFocus);
            if (admissionFailures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(admissionFailures[0]).Throw();
            if (admissionFailures is not null) throw new AggregateException("Navigation admission and cleanup failed.", admissionFailures);
            return NavigationResult.Empty;
        }

        // The history change is the commit point. All following lifecycle and visual callbacks
        // observe the new stack. Errors/cancellation cannot safely roll back application effects.
        Page[] removed = request.Kind == NavigationKind.Push ? [] : request.Kind == NavigationKind.Pop
            ? [stack[^1]] : stack.Skip(1).ToArray();
        if (request.Kind == NavigationKind.Push) stack.Add(next);
        else stack.RemoveRange(request.Kind == NavigationKind.Pop ? stack.Count - 1 : 1, removed.Length);
        TimeSpan duration = request.Animated ? transitionDuration : TimeSpan.Zero;
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        Attempt(() => host.PresentPage(next, deferAppearance: duration > TimeSpan.Zero));
        foreach (var page in removed) Attempt(() => host.ReleasePage(page));
        Attempt(() => StackChanged?.Invoke(this, EventArgs.Empty));
        Attempt(NotifyBack);
        if (restoreFocus) Attempt(host.RestorePageFocus);
        bool settled = false;
        if (!retiring && lifetime == request.Lifetime && ReferenceEquals(CurrentPage, next) && host.Owns(next))
        {
            try
            {
                using var visualCancellation = CancellationTokenSource.CreateLinkedTokenSource(request.Token, hostToken);
                settled = await host.AnimateCurrentAsync(duration, visualCancellation.Token,
                    () => !retiring && lifetime == request.Lifetime).On(scheduler);
            }
            catch (Exception error)
            {
                (failures ??= []).Add(error);
                if (HasUiAccess(scheduler)) Attempt(host.SettleCurrentAppearance);
            }
        }
        if (settled && !retiring && ReferenceEquals(CurrentPage, next))
            Attempt(() => Navigated?.Invoke(this, new(request.Kind, previous, next, Array.AsReadOnly(removed))));
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null) throw new AggregateException("Navigation callbacks failed after commitment.", failures);
        return new(settled && !retiring && ReferenceEquals(CurrentPage, next) && host.Owns(next), Array.AsReadOnly(removed));
    }

    private void OnPageLost(Page page)
    {
        if (retiring || !stack.Remove(page)) return;
        if (host.Page is null && CurrentPage is { } next && host.Owns(next)) host.PresentPage(next);
        StackChanged?.Invoke(this, EventArgs.Empty);
        NotifyBack();
    }

    private void NotifyBack() { backInput.RaiseCanExecuteChanged(); BackCommand.RaiseCanExecuteChanged(); }
    private static bool HasUiAccess(AnimationScheduler scheduler)
    { try { return scheduler.CheckFrameworkAccess(); } catch { return false; } }
    private void OnHostChanged() { lifetime++; activeLifetime?.Cancel(); }

    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new NavigationAccessibleObject(this);
    private sealed class NavigationAccessibleObject(NavigationPage owner) : ControlAccessibleObject(owner)
    {
        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (!owner.retiring) yield return owner.host.AccessibilityObject;
            foreach (var child in base.GetAccessibilityChildren()) yield return child;
        }
    }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(400, 300);
    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (!host.IsDisposed) { host.Bounds = LogicalPaddedClientRectangle; host.RefreshPagePresentation(); }
    }
    /// <inheritdoc/>
    protected override void OnParentChanged(EventArgs e)
    {
        host?.RefreshPagePresentation();
        base.OnParentChanged(e);
    }
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (retiring) return;
        retiring = true;
        lifetime++;
        List<Exception> failures = [];
        try { activeLifetime?.Cancel(); } catch (Exception error) { failures.Add(error); }
        while (pending.TryDequeue(out var request)) request.Complete(NavigationResult.Empty, new OperationCanceledException());
        host.PageLost -= OnPageLost;
        host.HostChanged -= OnHostChanged;
        stack.Clear();
        try { base.Dispose(true); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Navigation disposal failed.", failures);
    }

    private abstract record Request(NavigationKind Kind, Page? Page, bool Animated, CancellationToken Token, long Lifetime)
    {
        internal abstract void Complete(NavigationResult result, Exception? failure);
    }
    private sealed record Request<T>(NavigationKind Kind, Page? Page, bool Animated, CancellationToken Token,
        long Lifetime, Func<NavigationResult, T> SelectResult) : Request(Kind, Page, Animated, Token, Lifetime)
    {
        internal TaskCompletionSource<T> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal override void Complete(NavigationResult result, Exception? failure)
        {
            if (failure is OperationCanceledException) Completion.TrySetCanceled();
            else if (failure is not null) Completion.TrySetException(failure);
            else Completion.TrySetResult(SelectResult(result));
        }
    }
    private sealed record NavigationResult(bool Applied, IReadOnlyList<Page> Removed)
    {
        internal static NavigationResult Empty { get; } = new(false, Array.Empty<Page>());
    }
}
