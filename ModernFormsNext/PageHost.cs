using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Animations;

namespace ModernFormsNext;

/// <summary>Presents one current page inside an existing window or neutral control surface.</summary>
/// <remarks>
/// UI thread only. The host owns attached pages through real implicit presentation controls.
/// Assigning Page detaches the previous page without disposing it. Disposing the host disposes
/// pages still owned by it. A page must be detached before admission. Native focus loss and
/// application pause do not change the current page. Page events start only in a shown window
/// or an attached neutral surface; detached constructors remain inert.
/// A visible design-mode host paints its selected page through the same real presentation frame,
/// while suppressing appearance events and runtime lifecycle activation.
/// </remarks>
/// <example><code>
/// var host = new PageHost { Dock = DockStyle.Fill };
/// host.Page = new ContentPage { Title = "Home", Content = new Label { Text = "Home" } };
/// form.Controls.Add(host);
/// </code></example>
[DefaultProperty(nameof(Page))]
public class PageHost : Control
{
    private readonly List<PresentationFrame> frames = new();
    private PresentationFrame? current;
    private WindowBase? observedWindow;
    private Control? observedRoot;
    private readonly List<Control> observedAncestors = [];
    private AnimationHandle? animation;
    private long version;
    private int changing;
    private bool refreshing, refreshPending, retiring;
    internal event Action<Page>? PageLost;
    internal event Action? HostChanged;

    /// <summary>Creates an empty host without starting a native window or animation.</summary>
    public PageHost() => TabStop = false;

    /// <summary>Gets or replaces the current page; null clears its presentation.</summary>
    /// <remarks>
    /// Canonical focus validation can reject departure, in which case the previous page remains.
    /// Use TrySetPage when the caller needs an admission result. Replacement transfers the old
    /// page to its caller without disposal and raises normal page lifecycle notifications.
    /// </remarks>
    [DefaultValue(null)]
    public Page? Page { get => current?.Page; set => TrySetPage(value); }

    /// <summary>Attempts a synchronous page replacement through canonical focus validation.</summary>
    /// <param name="page">A detached page, the existing current page, or null.</param>
    /// <returns>Whether this replacement remained current through application callbacks.</returns>
    /// <remarks>UI thread only. An exception does not reclaim a control reparented by application code.</remarks>
    public bool TrySetPage(Page? page)
    {
        VerifyAccess();
        if (ReferenceEquals(Page, page)) return true;
        ValidatePage(page);
        var previous = Page;
        var previousFrame = current;
        long intent = version;
        if (!AdmitDeparture(previous, out bool restore)) return false;
        if (version != intent || !ReferenceEquals(Page, previous)) return false;
        bool changed = false;
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        Attempt(() => { if (page is null || PreparePage(page)) changed = PresentPage(page); });
        if (previous is not null && previousFrame is not null && frames.Contains(previousFrame) && !ReferenceEquals(current, previousFrame)) Attempt(() => ReleasePage(previous));
        if (page is not null && !ReferenceEquals(Page, page) && Owns(page)) Attempt(() => ReleasePage(page));
        if (restore) Attempt(RestorePageFocus);
        ThrowFailures(failures);
        return changed && ReferenceEquals(Page, page);
    }

    internal void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(retiring || IsDisposed || Disposing, this);
        GetFocusScope().VerifyAccess();
    }

    internal bool Owns(Page page)
        => ReferenceEquals(page.OwnerHost, this) && frames.Any(frame => ReferenceEquals(frame.Page, page) &&
            !frame.IsDisposed && ReferenceEquals(frame.Parent, this) && ReferenceEquals(page.Parent, frame));

    internal void ValidatePage(Page? page)
    {
        if (page is null || Owns(page)) return;
        if (page.IsRetiringPage) throw new ObjectDisposedException(nameof(page));
        if (page.OwnerHost is not null || page.Parent is not null)
            throw new InvalidOperationException("A page must be detached before another host can own it.");
        for (Control? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
            if (ReferenceEquals(ancestor, page)) throw new ArgumentException("A page cannot contain its own host.", nameof(page));
    }

    internal bool PreparePage(Page page)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(page);
        if (Owns(page)) return true;
        ValidatePage(page);
        var frame = new PresentationFrame(page) { Visible = false, TabStop = false };
        frames.Add(frame);
        try
        {
            Controls.AddDynamicImplicitControl(frame);
            if (retiring || frame.IsDisposed || frame.Parent != this) return false;
            page.AssignHost(this, frame);
            frame.Controls.Add(page);
            if (!Owns(page) || retiring) return false;
            page.PublishState(PageLifecycleState.Attached);
            PerformLayout();
            return Owns(page) && !retiring;
        }
        catch
        {
            // Parent/layout observers are application code. Finish only this still-owned
            // reservation; never detach or dispose a control transferred to another owner.
            ReleasePage(page);
            throw;
        }
    }

    internal bool AdmitDeparture(Page? previous, out bool restoreFocus)
    {
        VerifyAccess();
        var scope = GetFocusScope();
        var focused = scope.Owner;
        restoreFocus = ReferenceEquals(focused, this) || previous is not null && IsWithin(focused, previous);
        if (previous is null || !IsWithin(focused, previous)) return true;
        var frame = frames.Find(value => ReferenceEquals(value.Page, previous));
        if (frame is not null && focused is not null) frame.LastFocus = new(focused);
        // Request(null) intentionally skips validation. Selecting this actual, eligible
        // host uses the existing transaction and leaves cancellation/redirection authoritative.
        Select();
        return !retiring && Focused && ReferenceEquals(scope.Owner, this);
    }

    internal bool PresentPage(Page? page, bool deferAppearance = false)
    {
        VerifyAccess();
        if (page is not null && !Owns(page)) return false;
        var next = page is null ? null : frames.Find(frame => ReferenceEquals(frame.Page, page));
        if (ReferenceEquals(current, next)) return true;
        BindWindow();
        var previous = current;
        current = next;
        if (next is not null) next.AppearanceDeferred = deferAppearance;
        long operation = ++version;
        CancelAnimation();
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        changing++;
        try
        {
            if (previous is not null && Owns(previous.Page))
            {
                bool wasPresented = previous.Page.IsPresented;
                if (wasPresented) Attempt(() => previous.Page.PublishState(PageLifecycleState.Disappearing, PageNotification.Disappearing));
                Attempt(() => previous.Visible = false);
                previous.Opacity = 1;
                if (wasPresented && version == operation && Owns(previous.Page) && !previous.Page.IsRetiringPage)
                    Attempt(() => previous.Page.PublishState(PageLifecycleState.Attached, PageNotification.Disappeared));
            }
            if (version == operation && next is not null && Owns(next.Page))
            {
                Attempt(() => next.Visible = CanPresent());
                if (version == operation && Owns(next.Page) && next.Visible && next.Page.Visible)
                {
                    Attempt(() => next.Page.PublishState(PageLifecycleState.Appearing, PageNotification.Appearing));
                    if (!deferAppearance && version == operation && Owns(next.Page) && next.Page.IsPresented && CanPresent() && next.Page.Visible)
                        Attempt(() => next.Page.PublishState(PageLifecycleState.Appeared, PageNotification.Appeared));
                }
            }
        }
        finally { changing--; }
        Attempt(RefreshPagePresentation);
        Attempt(PerformLayout);
        ThrowFailures(failures);
        return version == operation && ReferenceEquals(current, next) && (page is null || Owns(page));
    }

    internal async AnimationCompletion<bool> AnimateCurrentAsync(TimeSpan duration, CancellationToken cancellationToken, Func<bool>? isOwnerCurrent = null)
    {
        var scheduler = AnimationScheduler.Default;
        var frame = current;
        long operation = version;
        var root = GetRoot();
        var window = FindWindow();
        bool HasAccess() { try { return scheduler.CheckFrameworkAccess(); } catch { return false; } }
        bool IsCurrent() => HasAccess() && !retiring && !IsDisposed && version == operation && ReferenceEquals(current, frame) &&
            frame is not null && Owns(frame.Page) && ReferenceEquals(GetRoot(), root) && ReferenceEquals(FindWindow(), window) && (isOwnerCurrent?.Invoke() ?? true);
        if (frame is null || !IsCurrent()) return false;
        try
        {
            if (duration > TimeSpan.Zero && frame.Page.IsPresented && CanPresent())
            {
                frame.Opacity = 0;
                var handle = scheduler.Start(this, "PageAppearance", progress =>
                {
                    if (IsCurrent()) frame.Opacity = progress;
                }, new AnimationOptions { Duration = duration, Easing = Easings.EaseOut });
                if (!IsCurrent()) { handle.Cancel(); return false; }
                animation = handle;
                using var registration = cancellationToken.Register(handle.Cancel);
                await handle.FrameworkCompletion.On(scheduler);
                if (handle.State == AnimationState.Faulted && handle.Exception is { } failure)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
            if (!IsCurrent()) return false;
            frame.Opacity = 1;
            frame.AppearanceDeferred = false;
            if (frame.Page.LifecycleState == PageLifecycleState.Appearing && CanPresent() && frame.Page.Visible) frame.Page.PublishState(PageLifecycleState.Appeared, PageNotification.Appeared);
            return IsCurrent();
        }
        finally
        {
            if (IsCurrent()) { frame.Opacity = 1; animation = null; }
        }
    }

    internal void SettleCurrentAppearance()
    {
        if (current is { } frame && Owns(frame.Page) && frame.Page.LifecycleState == PageLifecycleState.Appearing && CanPresent() && frame.Page.Visible)
        {
            frame.Opacity = 1;
            frame.AppearanceDeferred = false;
            frame.Page.PublishState(PageLifecycleState.Appeared, PageNotification.Appeared);
        }
    }

    // A composition container can reveal an already-retained page with the same incoming
    // effect as navigation. Defer its Appeared notification before making this host visible.
    internal void DeferCurrentAppearance()
    {
        VerifyAccess();
        if (current is { } frame && Owns(frame.Page)) frame.AppearanceDeferred = true;
    }

    internal void RestorePageFocus()
    {
        if (retiring || current is not { } frame || !Owns(frame.Page) || !frame.Page.IsPresented) return;
        var scope = GetFocusScope();
        if (!ReferenceEquals(scope.Owner, this)) return;
        if (frame.LastFocus?.TryGetTarget(out var previous) == true && !previous.IsDisposed && IsWithin(previous, frame.Page) && previous.CanSelect)
        { previous.Select(); return; }
        frame.Page.SelectNextControl(null, true, true, true, false);
        if (ReferenceEquals(scope.Owner, this) && Owns(frame.Page)) frame.Page.Select();
    }

    internal void ReleasePage(Page page, bool disposingPage = false)
    {
        var frame = frames.Find(value => ReferenceEquals(value.Page, page));
        if (frame is null) return;
        frames.Remove(frame);
        bool wasCurrent = ReferenceEquals(current, frame);
        bool wasPresented = page.IsPresented || page.LifecycleState == PageLifecycleState.Disappearing;
        if (wasCurrent) { current = null; version++; CancelAnimation(); }
        if (ReferenceEquals(page.OwnerHost, this)) page.AssignHost(null, null);
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        // The logical owner releases first. Consumers can settle their stack before any
        // user parent/disposal callback; subsequent cleanup targets only the old frame.
        Attempt(() => PageLost?.Invoke(page));
        if (page.OwnerHost is null && (!page.IsRetiringPage || disposingPage) && page.IsPresented)
            Attempt(() => page.PublishState(PageLifecycleState.Disappearing, PageNotification.Disappearing));
        Attempt(() => frame.Visible = false);
        if (ReferenceEquals(page.Parent, frame)) Attempt(() => frame.Controls.Remove(page));
        Attempt(() => Controls.Remove(frame));
        Attempt(frame.Dispose);
        if (page.OwnerHost is null && (!page.IsRetiringPage || disposingPage))
            Attempt(() => page.PublishState(PageLifecycleState.Detached, wasPresented ? PageNotification.Disappeared : PageNotification.None));
        ThrowFailures(failures);
    }

    internal void PageParentLost(Page page) => ReleasePage(page);

    internal void RefreshPagePresentation()
    {
        if (retiring || IsDisposed) return;
        if (changing != 0 || refreshing) { refreshPending = true; return; }
        refreshing = true;
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        try
        {
          int passes = 0;
          do
          {
            refreshPending = false;
            if (++passes > 64) throw new InvalidOperationException("Page visibility callbacks did not settle.");
            BindWindow();
            if (current is not { } frame || !Owns(frame.Page)) break;
            bool canPresent = CanPresent();
            // A design-mode host uses the same selected frame, layout and paint tree,
            // but never publishes appearance or activates runtime lifecycle bindings.
            Attempt(() => frame.Visible = canPresent || Visible && IsDesignPreview());
            bool visible = canPresent && frame.Visible && frame.Page.Visible;
            if (visible && !frame.Page.IsPresented)
            {
                long operation = ++version;
                Attempt(() => frame.Page.PublishState(PageLifecycleState.Appearing, PageNotification.Appearing));
                if (operation == version && ReferenceEquals(current, frame) && Owns(frame.Page) && !frame.AppearanceDeferred && frame.Page.IsPresented && CanPresent() && frame.Page.Visible)
                    Attempt(() => frame.Page.PublishState(PageLifecycleState.Appeared, PageNotification.Appeared));
            }
            else if (!visible && frame.Page.IsPresented)
            {
                long operation = ++version;
                frame.AppearanceDeferred = false;
                CancelAnimation();
                Attempt(() => frame.Page.PublishState(PageLifecycleState.Disappearing, PageNotification.Disappearing));
                if (operation == version && ReferenceEquals(current, frame) && Owns(frame.Page))
                    Attempt(() => frame.Page.PublishState(PageLifecycleState.Attached, PageNotification.Disappeared));
            }
            else if (visible && !frame.AppearanceDeferred && animation is null && frame.Page.LifecycleState == PageLifecycleState.Appearing)
                Attempt(() => frame.Page.PublishState(PageLifecycleState.Appeared, PageNotification.Appeared));
          } while (refreshPending && !retiring && !IsDisposed);
        }
        finally { refreshing = false; }
        ThrowFailures(failures);
    }

    private bool CanPresent()
    {
        if (retiring || IsDisposed || !Visible || Site?.DesignMode == true) return false;
        var window = FindWindow();
        // A preview can site the owning page, an ancestor or the Form rather than this
        // private/runtime host. Match the existing NativeViewHost ancestry boundary.
        if (IsDesignPreview()) return false;
        return window is not null ? window.shown && window.Visible && !window.InputBindingsClosed && !window.IsBackendClosed
            : GetRoot().IsCommandRoutingRoot && GetRoot().IsFocusRootAvailable;
    }

    private bool IsDesignPreview() => Site?.DesignMode == true || current?.Page.Site?.DesignMode == true
        || FindWindow()?.Site?.DesignMode == true
        || observedAncestors.Any(ancestor => ancestor.Site?.DesignMode == true);

    private Control GetRoot() { Control root = this; while (root.Parent is { } parent) root = parent; return root; }
    private static bool IsWithin(Control? control, Control ancestor)
    { for (var current = control; current is not null; current = current.Parent) if (ReferenceEquals(current, ancestor)) return true; return false; }

    private void BindWindow()
    {
        var window = FindWindow();
        var root = GetRoot();
        // Observe existing tree lifetime signals, including reparenting an ancestor while
        // all effective visibility values stay true. No alternate host or visual tree exists.
        int index = 0;
        var ancestor = Parent;
        while (ancestor is not null && index < observedAncestors.Count && ReferenceEquals(ancestor, observedAncestors[index]))
        { ancestor = ancestor.Parent; index++; }
        bool ancestryChanged = ancestor is not null || index != observedAncestors.Count;
        if (ancestryChanged)
        {
            foreach (var previous in observedAncestors) previous.NativePresentationChanged -= AncestorChanged;
            observedAncestors.Clear();
            for (ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
            { observedAncestors.Add(ancestor); ancestor.NativePresentationChanged += AncestorChanged; }
        }
        if (!ancestryChanged && ReferenceEquals(window, observedWindow) && ReferenceEquals(root, observedRoot)) return;
        if (observedWindow is not null)
        { observedWindow.Shown -= WindowChanged; observedWindow.VisibleChanged -= WindowChanged; observedWindow.Closed -= WindowChanged; }
        observedWindow = window;
        observedRoot = root;
        version++;
        if (current is { } pendingFrame) pendingFrame.AppearanceDeferred = false;
        HostChanged?.Invoke();
        CancelAnimation();
        if (current is { IsDisposed: false } frame) frame.Opacity = 1;
        if (window is not null)
        { window.Shown += WindowChanged; window.VisibleChanged += WindowChanged; window.Closed += WindowChanged; }
    }

    private void WindowChanged(object? sender, EventArgs e) => RefreshPagePresentation();
    private void AncestorChanged(object? sender, EventArgs e) => RefreshPagePresentation();
    private void CancelAnimation() { var previous = animation; animation = null; previous?.Cancel(); }
    private static void ThrowFailures(List<Exception>? failures)
    {
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null) throw new AggregateException("Page presentation callbacks failed.", failures);
    }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(400, 300);
    /// <inheritdoc/>
    protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); RefreshPagePresentation(); }
    /// <inheritdoc/>
    protected override void OnVisibleChanged(EventArgs e) { RefreshPagePresentation(); base.OnVisibleChanged(e); }
    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (current is { IsDisposed: false } frame) frame.Bounds = LogicalPaddedClientRectangle;
        RefreshPagePresentation();
    }
    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new PageHostAccessibleObject(this);
    private sealed class PageHostAccessibleObject(PageHost owner) : ControlAccessibleObject(owner)
    {
        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (!owner.IsDisposed && owner.current is { Visible: true } frame && owner.Owns(frame.Page)) yield return frame.AccessibilityObject;
            foreach (var child in base.GetAccessibilityChildren()) yield return child;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (retiring) return;
        retiring = true;
        version++;
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        Attempt(CancelAnimation);
        if (observedWindow is { } window)
        { window.Shown -= WindowChanged; window.VisibleChanged -= WindowChanged; window.Closed -= WindowChanged; observedWindow = null; }
        PageLost = null;
        HostChanged = null;
        foreach (var ancestor in observedAncestors) ancestor.NativePresentationChanged -= AncestorChanged;
        observedAncestors.Clear();
        current = null;
        foreach (var frame in frames.ToArray())
        {
            // Recheck each captured entry after callbacks from previous children. A sibling
            // transferred by application code belongs to its new owner and must survive.
            if (ReferenceEquals(frame.Page.OwnerHost, this) && ReferenceEquals(frame.Page.Parent, frame))
                Attempt(frame.Page.Dispose);
            Attempt(() => Controls.Remove(frame));
            Attempt(frame.Dispose);
        }
        frames.Clear();
        observedRoot = null;
        Attempt(() => base.Dispose(true));
        ThrowFailures(failures);
    }

    private sealed class PresentationFrame(Page page) : Control
    {
        internal Page Page { get; } = page;
        internal WeakReference<Control>? LastFocus;
        internal bool AppearanceDeferred;
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (ReferenceEquals(Page.Parent, this) && !Page.IsDisposed) Page.Bounds = LogicalPaddedClientRectangle;
        }
    }
}
