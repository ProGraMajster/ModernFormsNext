using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Input;
using SkiaSharp;

namespace ModernFormsNext;

/// <summary>Composes retained flyout and detail pages in responsive split or overlay presentation.</summary>
/// <remarks>
/// All access belongs on the UI thread. Each slot is an actual PageHost in the ordinary control,
/// resource, focus and semantic tree. Replacing a slot transfers its previous page without disposal;
/// disposing this container owns both remaining pages. Detached constructors start no native activity.
/// Overlay open intent survives split presentation. Split always shows both slots. A focused flyout
/// stays open when resizing from Split into Overlay, preserving its editing transaction.
/// </remarks>
/// <example><code>
/// var navigation = new FlyoutPage
/// {
///     Flyout = new ContentPage { Title = "Navigation", Content = new Button { Text = "Documents" } },
///     Detail = new ContentPage { Title = "Documents", Content = new TextBox() }
/// };
/// form.Controls.Add(new PageHost { Page = navigation, Dock = DockStyle.Fill });
/// await navigation.SetFlyoutOpenAsync(true);
/// </code></example>
public class FlyoutPage : Page
{
    private readonly PageHost detailHost;
    private readonly Backdrop backdrop;
    private readonly PageHost flyoutHost;
    private readonly RoutedCommand dismissInput = new("DismissFlyout");
    private CancellationTokenSource? effectLifetime;
    private FocusTransfer? focusTransfer;
    private long version;
    private int requestDepth;
    private bool open, retiring, refreshing, refreshPending, publishedOpen;
    private int flyoutWidth = 280, splitBreakpoint = 720;
    private FlyoutDisplayMode mode = FlyoutDisplayMode.Auto, effectiveMode, publishedMode;
    private FlowDirection direction;
    private TimeSpan transitionDuration = TimeSpan.FromMilliseconds(180);

    /// <summary>Creates empty slots without active lifecycle, windows or transitions.</summary>
    public FlyoutPage()
    {
        detailHost = Controls.AddImplicitControl(new PageHost());
        backdrop = Controls.AddImplicitControl(new Backdrop(this));
        flyoutHost = Controls.AddImplicitControl(new PageHost { Visible = false });
        detailHost.PageLost += SlotPageLost;
        flyoutHost.PageLost += SlotPageLost;
        detailHost.HostChanged += HostChanged;
        flyoutHost.HostChanged += HostChanged;
        ToggleFlyoutCommand = new AsyncCommand(async (_, token) => { await SetFlyoutOpenAsync(!open, cancellationToken: token).ConfigureAwait(false); },
            _ => !retiring && Enabled && effectiveMode == FlyoutDisplayMode.Overlay && Flyout is not null);
        CloseFlyoutCommand = new DelegateCommand(() => TrySetFlyoutOpen(false), CanDismiss);
        CommandBindings.Add(new CommandBinding(dismissInput, (_, e) =>
        {
            e.Handled = true; // A validation veto must not become a native window-close fallback.
            CloseFlyoutCommand.ExecuteCore(null);
        }, (_, e) => e.CanExecute = CanDismiss()));
        InputBindings.Add(new KeyBinding(dismissInput, new KeyGesture(Keys.Escape)));
        InputBindings.Add(new KeyBinding(dismissInput, new KeyGesture(Keys.BrowserBack)));
        InputBindings.Add(new KeyBinding(new DelegateCommand(() => TraverseFlyout(true), CanTraverse), new KeyGesture(Keys.Tab)));
        InputBindings.Add(new KeyBinding(new DelegateCommand(() => TraverseFlyout(false), CanTraverse), new KeyGesture(Keys.Tab, KeyModifiers.Shift)));
        RefreshPresentation();
    }

    /// <summary>Gets or attempts to replace the flyout slot with a detached page, or null.</summary>
    /// <remarks>Canonical validation may reject replacement. Use TrySetFlyout when the result is needed.</remarks>
    [DefaultValue(null)]
    public Page? Flyout { get => flyoutHost.Page; set => TrySetFlyout(value); }
    /// <summary>Gets or attempts to replace the detail slot with a detached page, or null.</summary>
    /// <remarks>Replacement transfers the previous detail without disposal; the new page cannot occupy both slots.</remarks>
    [DefaultValue(null)]
    public Page? Detail { get => detailHost.Page; set => TrySetDetail(value); }
    /// <summary>Gets the committed overlay open intent; Split presents the flyout independently of this value.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsFlyoutOpen => open;
    /// <summary>Gets or sets Overlay, Split or responsive Auto presentation; default Auto.</summary>
    /// <remarks>UI thread only. A change cancels older visual work and requests ordinary layout.</remarks>
    [DefaultValue(FlyoutDisplayMode.Auto)]
    public FlyoutDisplayMode DisplayMode
    {
        get => mode;
        set
        {
            VerifyPageAccess();
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (mode == value) return;
            mode = value; RetireEffect(); PerformLayout();
        }
    }
    /// <summary>Gets the last committed effective mode, always Overlay or Split.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FlyoutDisplayMode EffectiveDisplayMode => effectiveMode;
    /// <summary>Gets or sets the preferred flyout width in logical pixels; default 280, positive only.</summary>
    /// <remarks>Actual width is clamped to the available padded width; page padding remains independent.</remarks>
    [DefaultValue(280)]
    public int FlyoutWidth
    {
        get => flyoutWidth;
        set { VerifyPageAccess(); ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); if (flyoutWidth != value) { flyoutWidth = value; PerformLayout(); } }
    }
    /// <summary>Gets or sets the positive logical padded width at which Auto chooses Split; default 720.</summary>
    [DefaultValue(720)]
    public int SplitBreakpoint
    {
        get => splitBreakpoint;
        set { VerifyPageAccess(); ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); if (splitBreakpoint != value) { splitBreakpoint = value; PerformLayout(); } }
    }
    /// <summary>Gets or sets the explicit horizontal leading-edge direction; default LeftToRight.</summary>
    /// <remarks>RightToLeft places the flyout at the right edge. This does not alter nested page order or text direction.</remarks>
    [DefaultValue(FlowDirection.LeftToRight)]
    public FlowDirection FlowDirection
    {
        get => direction;
        set
        {
            VerifyPageAccess();
            if (value is not (FlowDirection.LeftToRight or FlowDirection.RightToLeft)) throw new ArgumentOutOfRangeException(nameof(value));
            if (direction != value) { direction = value; PerformLayout(); }
        }
    }
    /// <summary>Gets or sets the incoming overlay fade duration; default 180 milliseconds.</summary>
    /// <remarks>Non-negative, captured per request. Zero, animated:false and reduced motion settle immediately.</remarks>
    public TimeSpan TransitionDuration
    {
        get => transitionDuration;
        set { VerifyPageAccess(); ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero); transitionDuration = value; }
    }
    /// <summary>Gets the ordinary single-flight toggle action, including animated opening.</summary>
    /// <remarks>CloseFlyoutCommand remains available while opening; ExecutionTask observes toggle failures.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AsyncCommand ToggleFlyoutCommand { get; }
    /// <summary>Gets the ordinary immediate dismissal action for Back, Escape, backdrop and application buttons.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DelegateCommand CloseFlyoutCommand { get; }
    /// <summary>Occurs after open intent commits and current presentation updates.</summary>
    /// <remarks>A newer callback may change intent again; exceptions do not roll back committed state.</remarks>
    public event EventHandler? FlyoutOpenChanged;
    /// <summary>Occurs after an effective responsive layout mode commits and presentation updates.</summary>
    public event EventHandler? EffectiveDisplayModeChanged;

    /// <summary>Attempts to replace the flyout slot through its canonical PageHost admission.</summary>
    /// <param name="page">A detached page, the current flyout, or null.</param>
    /// <returns>Whether this replacement remained current through callbacks.</returns>
    public bool TrySetFlyout(Page? page) => SetSlot(flyoutHost, detailHost, page);
    /// <summary>Attempts to replace the detail slot through canonical focus validation.</summary>
    /// <param name="page">A detached page, the current detail, or null.</param>
    /// <returns>Whether this replacement remained current through callbacks.</returns>
    public bool TrySetDetail(Page? page) => SetSlot(detailHost, flyoutHost, page);
    /// <summary>Attempts an immediate opening or closing without blocking the UI thread.</summary>
    /// <param name="isOpen">The requested overlay open intent.</param>
    /// <returns>Whether the intent survived validation and callbacks.</returns>
    /// <remarks>Closing retires an older incoming effect. Veto preserves the edited page and focus.</remarks>
    public bool TrySetFlyoutOpen(bool isOpen)
    {
        var task = SetFlyoutOpenAsync(isOpen, animated: false);
        if (!task.IsCompleted) throw new InvalidOperationException("An immediate flyout change did not settle synchronously.");
        return task.GetAwaiter().GetResult();
    }
    /// <summary>Attempts to change overlay intent and awaits its shared-scheduler incoming effect.</summary>
    /// <param name="isOpen">The requested overlay intent. An empty flyout cannot be opened.</param>
    /// <param name="animated">Whether to fade the incoming overlay using the shared scheduler.</param>
    /// <param name="cancellationToken">Precommit cancellation changes no intent; after commit it only settles the visual effect.</param>
    /// <returns>Whether this request remained current through completion; a newer request supersedes older work.</returns>
    /// <remarks>
    /// Start on the UI thread. Validation precedes state commitment. Outside focus is preserved.
    /// Postcommit callback failures fault the task without undoing application effects. Closing is
    /// immediate. Existing layout and the shared scheduler perform the work; authored page opacity is preserved.
    /// </remarks>
    public Task<bool> SetFlyoutOpenAsync(bool isOpen, bool animated = true, CancellationToken cancellationToken = default)
    {
        VerifyPageAccess();
        ObjectDisposedException.ThrowIf(retiring, this);
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<bool>(cancellationToken);
        if (requestDepth >= 32) throw new InvalidOperationException("Flyout requests did not settle after 32 nested calls.");
        requestDepth++;
        try
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = ChangeOpenAsync(isOpen, animated ? transitionDuration : TimeSpan.Zero, cancellationToken, completion);
            return completion.Task;
        }
        finally { requestDepth--; }
    }

    private async AnimationCompletion<bool> ChangeOpenAsync(bool requested, TimeSpan duration, CancellationToken token, TaskCompletionSource<bool> completion)
    {
        var scheduler = AnimationScheduler.Default;
        long intent = ++version;
        CancellationTokenSource? effect = null;
        PageHost? admitted = null;
        bool restore = false, committed = false;
        bool Current() => !retiring && !IsDisposed && version == intent;
        try
        {
            CancelEffect();
            if (!Current() || requested && Flyout is null) { completion.TrySetResult(false); return false; }
            if (open == requested) { RefreshPresentation(); completion.TrySetResult(Current()); return Current(); }
            var scope = GetFocusScope();
            var focused = scope.Owner;
            if (effectiveMode == FlyoutDisplayMode.Overlay)
            {
                var leaving = requested ? detailHost : flyoutHost;
                if (IsWithin(focused, leaving))
                {
                    admitted = leaving;
                    if (!leaving.AdmitDeparture(leaving.Page, out restore) || !Current()) { completion.TrySetResult(false); return false; }
                }
                else
                {
                    var destination = requested ? flyoutHost : detailHost;
                    restore = IsWithin(focused, this) && (!IsWithin(focused, destination) || ReferenceEquals(focused, destination));
                }
            }
            token.ThrowIfCancellationRequested();
            if (!Current()) { completion.TrySetResult(false); return false; }
            effect = CancellationTokenSource.CreateLinkedTokenSource(token);
            effectLifetime = effect;
            bool animate = requested && effectiveMode == FlyoutDisplayMode.Overlay && duration > TimeSpan.Zero;
            if (animate) flyoutHost.DeferCurrentAppearance();
            open = requested; // State commits before lifecycle and observer callbacks.
            committed = true;
            if (restore && scope.Owner is { } anchor) focusTransfer = new(intent, scope, scope.Version, anchor, requested ? flyoutHost : detailHost);
            RefreshPresentation();
            bool settled = true;
            if (animate && Current() && open)
                settled = await flyoutHost.AnimateCurrentAsync(duration, effect.Token, Current).On(scheduler);
            if (!HasAccess(scheduler)) throw new InvalidOperationException("The flyout UI dispatcher is unavailable.");
            if (Current()) UpdateCommands();
            bool applied = settled && Current() && open == requested;
            completion.TrySetResult(applied);
            return applied;
        }
        catch (Exception failure)
        {
            if (HasAccess(scheduler) && Current())
            {
                try { if (!committed && restore) admitted?.RestorePageFocus(); else flyoutHost.SettleCurrentAppearance(); }
                catch (Exception cleanup) { failure = new AggregateException("Flyout admission/completion cleanup failed.", failure, cleanup); }
            }
            if (failure is OperationCanceledException && !committed) completion.TrySetCanceled();
            else completion.TrySetException(failure);
            return false;
        }
        finally
        {
            effect?.Dispose();
            if (ReferenceEquals(effectLifetime, effect)) effectLifetime = null;
        }
    }

    private bool SetSlot(PageHost target, PageHost other, Page? page)
    {
        VerifyPageAccess();
        if (page is not null && ReferenceEquals(other.Page, page)) throw new ArgumentException("A page cannot occupy both flyout slots.", nameof(page));
        long intent = ++version;
        CancelEffect();
        if (retiring || intent != version) return false;
        bool applied = false;
        List<Exception>? failures = null;
        try { applied = target.TrySetPage(page); } catch (Exception error) { failures = [error]; }
        try { RefreshPresentation(); } catch (Exception error) { (failures ??= []).Add(error); }
        ThrowFailures(failures);
        return !retiring && applied && ReferenceEquals(target.Page, page);
    }

    private void SlotPageLost(Page page) { if (!retiring) { RetireEffect(); RefreshPresentation(); } }
    private void HostChanged() { if (!retiring) RetireEffect(); }
    private void RetireEffect() { version++; focusTransfer = null; CancelEffect(); }
    private void CancelEffect()
    {
        var previous = effectLifetime; effectLifetime = null;
        List<Exception>? failures = null;
        try { previous?.Cancel(); } catch (Exception error) { failures = [error]; }
        if (!retiring && flyoutHost is not null)
        {
            try { flyoutHost.SettleCurrentAppearance(); } catch (Exception error) { (failures ??= []).Add(error); }
        }
        ThrowFailures(failures);
    }

    private void RefreshPresentation()
    {
        if (retiring || IsDisposed || flyoutHost is null || detailHost is null || backdrop is null) return;
        if (refreshing) { refreshPending = true; return; }
        refreshing = true;
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        try
        {
            int passes = 0;
            do
            {
                refreshPending = false;
                if (++passes > 64) throw new InvalidOperationException("Flyout layout callbacks did not settle after 64 changes.");
                var area = LogicalPaddedClientRectangle;
                var requestedMode = mode;
                var requestedDirection = direction;
                int requestedWidth = flyoutWidth, requestedBreakpoint = splitBreakpoint;
                var nextMode = requestedMode == FlyoutDisplayMode.Auto
                    ? area.Width >= requestedBreakpoint ? FlyoutDisplayMode.Split : FlyoutDisplayMode.Overlay : requestedMode;
                bool modeChanged = nextMode != effectiveMode;
                bool preserveSplitFocus = modeChanged && effectiveMode == FlyoutDisplayMode.Split && nextMode == FlyoutDisplayMode.Overlay;
                if (modeChanged) { effectiveMode = nextMode; Attempt(RetireEffect); }
                var scope = GetFocusScope();
                if (open && Flyout is null)
                {
                    open = false; version++;
                    if (ReferenceEquals(scope.Owner, flyoutHost)) focusTransfer = new(version, scope, scope.Version, flyoutHost, detailHost);
                }
                // Responsive layout cannot hide an actively edited split pane merely because
                // the viewport crossed a breakpoint. Preserve that current editor as an overlay.
                if (preserveSplitFocus && !open && Flyout is not null && IsWithin(scope.Owner, flyoutHost))
                { open = true; version++; }
                long passVersion = version;
                bool Restart()
                {
                    if (retiring || IsDisposed) return true;
                    // PerformLayout can be deferred by an outer layout transaction. Compare
                    // its actual inputs too: a completion callback can request another mode
                    // before that queued layout has entered this method again.
                    if (passVersion == version && requestedMode == mode && requestedDirection == direction &&
                        requestedWidth == flyoutWidth && requestedBreakpoint == splitBreakpoint && area == LogicalPaddedClientRectangle) return false;
                    refreshPending = true;
                    return true;
                }
                if (Restart()) continue;
                bool overlay = effectiveMode == FlyoutDisplayMode.Overlay && open && Flyout is not null;
                bool showFlyout = Flyout is not null && (effectiveMode == FlyoutDisplayMode.Split || overlay);
                int width = Math.Min(requestedWidth, area.Width);
                bool right = requestedDirection == FlowDirection.RightToLeft;
                var flyoutBounds = new Rectangle(right ? area.Right - width : area.X, area.Y, width, area.Height);
                var detailBounds = effectiveMode == FlyoutDisplayMode.Split && Flyout is not null
                    ? new Rectangle(right ? area.X : area.X + width, area.Y, Math.Max(0, area.Width - width), area.Height) : area;
                Attempt(() => detailHost.Bounds = detailBounds);
                if (Restart()) continue;
                Attempt(() => backdrop.Bounds = area);
                if (Restart()) continue;
                Attempt(() => flyoutHost.Bounds = flyoutBounds);
                if (Restart()) continue;
                Attempt(() => detailHost.Visible = true);
                if (Restart()) continue;
                if (!overlay) Attempt(() => detailHost.Enabled = true);
                if (Restart()) continue;
                if (showFlyout) Attempt(() => flyoutHost.Visible = true);
                if (Restart()) continue;
                var transfer = focusTransfer;
                focusTransfer = null;
                if (transfer is { } move && move.Version == version && move.Scope.Version == move.FocusVersion &&
                    ReferenceEquals(move.Scope.Owner, move.Anchor) && move.Target.CanSelect)
                {
                    Attempt(move.Target.Select);
                    if (Restart()) continue;
                    if (move.Version == version && ReferenceEquals(move.Scope.Owner, move.Target)) Attempt(move.Target.RestorePageFocus);
                }
                if (Restart()) continue;
                // Appearing/focus callbacks may intentionally return to an editor in the
                // detail before the overlay disables it. That newer focus owns admission;
                // abandon this older opening instead of forcibly retiring the new editor.
                if (overlay && IsWithin(scope.Owner, detailHost))
                { open = false; version++; refreshPending = true; continue; }
                Attempt(() => detailHost.Enabled = !overlay);
                if (Restart()) continue;
                Attempt(() => backdrop.Visible = overlay);
                if (Restart()) continue;
                if (!showFlyout) Attempt(() => flyoutHost.Visible = false);
                if (Restart()) continue;
                Attempt(UpdateCommands);
                if (Restart()) continue;
                if (publishedMode != effectiveMode)
                {
                    publishedMode = effectiveMode;
                    Attempt(() => EffectiveDisplayModeChanged?.Invoke(this, EventArgs.Empty));
                    if (Restart()) continue;
                }
                if (publishedOpen != open)
                {
                    publishedOpen = open;
                    Attempt(() => FlyoutOpenChanged?.Invoke(this, EventArgs.Empty));
                    if (Restart()) continue;
                }
            } while (refreshPending && !retiring && !IsDisposed);
        }
        finally { refreshing = false; }
        ThrowFailures(failures);
    }

    private bool CanDismiss() => !retiring && Enabled && effectiveMode == FlyoutDisplayMode.Overlay && open && Flyout is not null;
    private bool CanTraverse() => CanDismiss() && GetFocusScope().Owner is { WantsTabKey: false } focused && IsWithin(focused, flyoutHost);
    private void TraverseFlyout(bool forward)
    {
        if (Flyout is not { } page || !CanTraverse()) return;
        var focused = GetFocusScope().Owner;
        if (!page.SelectNextControl(IsWithin(focused, page) ? focused : null, forward, true, true, true)) page.Select();
    }
    private void UpdateCommands() { ToggleFlyoutCommand?.RaiseCanExecuteChanged(); CloseFlyoutCommand?.RaiseCanExecuteChanged(); }
    private static bool HasAccess(AnimationScheduler scheduler) { try { return scheduler.CheckFrameworkAccess(); } catch { return false; } }
    private static bool IsWithin(Control? control, Control ancestor)
    { for (var current = control; current is not null; current = current.Parent) if (ReferenceEquals(current, ancestor)) return true; return false; }
    private static void ThrowFailures(List<Exception>? failures)
    {
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null) throw new AggregateException("Flyout callbacks failed after commitment.", failures);
    }
    private sealed record FocusTransfer(long Version, ControlFocusScope Scope, long FocusVersion, Control Anchor, PageHost Target);

    /// <inheritdoc/>
    protected override Size DefaultSize => new(800, 600);
    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); RefreshPresentation(); }
    /// <inheritdoc/>
    protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); RefreshPresentation(); }
    /// <inheritdoc/>
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); RefreshPresentation(); }
    /// <inheritdoc/>
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!retiring) UpdateCommands(); }
    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new FlyoutAccessibleObject(this);
    private sealed class FlyoutAccessibleObject(FlyoutPage owner) : ControlAccessibleObject(owner)
    {
        internal override bool HasViewVisibility => true;
        public override AccessibleControlType ControlType => AccessibleControlType.Pane;
        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (Owner is FlyoutPage { retiring: false } current)
            {
                yield return current.detailHost.AccessibilityObject;
                if (current.backdrop.Visible) yield return current.backdrop.AccessibilityObject;
                if (current.flyoutHost.Visible) yield return current.flyoutHost.AccessibilityObject;
            }
            foreach (var child in base.GetAccessibilityChildren()) yield return child;
        }
    }
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (retiring) return;
        retiring = true; version++; focusTransfer = null;
        List<Exception>? failures = null;
        try { CancelEffect(); } catch (Exception error) { failures = [error]; }
        detailHost.PageLost -= SlotPageLost; flyoutHost.PageLost -= SlotPageLost;
        detailHost.HostChanged -= HostChanged; flyoutHost.HostChanged -= HostChanged;
        try { base.Dispose(true); } catch (Exception error) { (failures ??= []).Add(error); }
        ThrowFailures(failures);
    }

    private sealed class Backdrop : Control
    {
        internal FlyoutPage PageOwner { get; }
        internal Backdrop(FlyoutPage owner)
        {
            PageOwner = owner; TabStop = false; Visible = false;
            SetControlBehavior(ControlBehaviors.Selectable, false);
            Style.BackgroundColor = SKColors.Transparent;
            AccessibleName = "Dismiss navigation pane";
        }
        protected override void OnClick(MouseEventArgs e) { base.OnClick(e); if (e.Button == MouseButtons.Left && PageOwner.CanDismiss()) PageOwner.TrySetFlyoutOpen(false); }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); e.Canvas.FillRectangle(ClientRectangle, Theme.ControlLowColor.WithAlpha(128)); }
        protected override AccessibleObject CreateAccessibilityInstance() => new BackdropAccessibleObject(this);
        private sealed class BackdropAccessibleObject(Backdrop owner) : ControlAccessibleObject(owner)
        {
            internal override bool HasViewVisibility => true;
            private Backdrop? Current => Owner is Backdrop { IsDisposed: false, Visible: true } control && !control.PageOwner.retiring ? control : null;
            public override AccessibleControlType ControlType => AccessibleControlType.Button;
            public override AccessibleRole Role => AccessibleRole.PushButton;
            public override AccessibleActions SupportedActions => Current is { } control && control.PageOwner.CanDismiss() ? AccessibleActions.Invoke : AccessibleActions.None;
            public override bool PerformAction(AccessibleActions action, object? parameter = null)
                => action == AccessibleActions.Invoke && parameter is null && Current is { } control && control.PageOwner.CanDismiss() && control.PageOwner.TrySetFlyoutOpen(false);
        }
    }
}
