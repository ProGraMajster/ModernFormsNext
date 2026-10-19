using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Input;
using SkiaSharp;

namespace ModernFormsNext;

/// <summary>Retains pages in one canonical host and selects them through actual scrollable tab buttons.</summary>
/// <remarks>
/// Use on the UI thread. Inactive pages stay owned, hidden and alive; removal transfers ownership.
/// The first enabled addition is selected when selection is empty. Disabling a selected page retains
/// its selection and presents its normal disabled state. Disabled pages cannot become a new selection.
/// Callbacks may supersede synchronous operations; at most 32 nested mutation calls are admitted.
/// Constructors are inert. No TabControl item semantics or second focus/accessibility tree is used.
/// </remarks>
/// <example><code>
/// var tabs = new TabbedPage();
/// tabs.Pages.Add(new ContentPage { Title = "Documents", Content = new TextBox() });
/// tabs.Pages.Add(new NavigationPage(new ContentPage { Title = "Settings" }) { Title = "Settings" });
/// form.Controls.Add(new PageHost { Page = tabs, Dock = DockStyle.Fill });
/// </code></example>
public class TabbedPage : Page
{
    private readonly HeaderRow headers;
    private readonly PageHost host;
    private readonly Dictionary<Page, HeaderButton> buttons = [];
    private readonly HashSet<Page> reservations = [];
    private long selectionVersion, collectionVersion, selectionNotificationVersion;
    private int mutationDepth;
    private bool retiring;
    private FlowDirection direction;

    /// <summary>Creates an empty, detached tab container without native activity or appearance events.</summary>
    public TabbedPage()
    {
        Pages = new(this);
        headers = Controls.AddImplicitControl(new HeaderRow { AutoScroll = true, WrapContents = false, AccessibleName = "Page tabs" });
        host = Controls.AddImplicitControl(new PageHost());
        host.PageLost += PageLost;
        host.HostChanged += HostChanged;
        Bind(this, Keys.Tab, KeyModifiers.Control, () => SelectRelative(true));
        Bind(this, Keys.Tab, KeyModifiers.Control | KeyModifiers.Shift, () => SelectRelative(false));
        Bind(this, Keys.PageDown, KeyModifiers.Control, () => SelectRelative(true));
        Bind(this, Keys.PageUp, KeyModifiers.Control, () => SelectRelative(false));
    }

    /// <summary>Gets the ordered retained pages. Removal transfers pages without disposal.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public PageCollection Pages { get; }
    /// <summary>Gets or attempts to select a retained page; null clears selection.</summary>
    /// <remarks>Validation or a newer callback can retain another selection. Use TrySelectPage for a result.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Page? SelectedPage { get => host.Page; set => TrySelectPage(value); }
    /// <summary>Gets or attempts to set the logical selected index; minus one clears selection.</summary>
    [DefaultValue(-1)]
    public int SelectedIndex
    {
        get => SelectedPage is { } page ? Pages.IndexOf(page) : -1;
        set { VerifyPageAccess(); if (value < -1 || value >= Pages.Count) throw new ArgumentOutOfRangeException(nameof(value)); TrySelectPage(value < 0 ? null : Pages[value]); }
    }
    /// <summary>Gets or sets the explicit horizontal direction of tab headers.</summary>
    /// <remarks>
    /// LeftToRight by default; only horizontal FlowDirection values are accepted. This local setting
    /// does not change page collection order, child text direction or another nested container.
    /// </remarks>
    [DefaultValue(FlowDirection.LeftToRight)]
    public FlowDirection FlowDirection
    {
        get => direction;
        set
        {
            VerifyPageAccess();
            if (value is not (FlowDirection.LeftToRight or FlowDirection.RightToLeft)) throw new ArgumentOutOfRangeException(nameof(value));
            if (direction == value) return;
            direction = value;
            SynchronizeHeaderOrder();
            PerformLayout();
            RevealSelectedHeader();
        }
    }
    /// <summary>Occurs before canonical validation or a logical selection change commits.</summary>
    public event EventHandler<PageSelectionChangingEventArgs>? SelectedPageChanging;
    /// <summary>Occurs after selection and lifecycle changes commit, including mandatory external removal.</summary>
    /// <remarks>Observer exceptions do not roll back committed selection or ownership.</remarks>
    public event EventHandler? SelectedPageChanged;
    /// <summary>Occurs after collection membership/order commits and normal ownership cleanup settles.</summary>
    /// <remarks>Callbacks may mutate again. The sender's current collection is authoritative.</remarks>
    public event EventHandler? PagesChanged;

    /// <summary>Attempts an immediate page switch through the existing focus/validation transaction.</summary>
    /// <param name="page">A retained enabled page, the current page, or null.</param>
    /// <returns>Whether the requested selection remained current through callbacks.</returns>
    /// <remarks>
    /// UI thread only. Outside focus is preserved. Page lifecycle uses the actual host; no private
    /// transition clock is started. Postcommit callback errors propagate without undoing their effects.
    /// </remarks>
    public bool TrySelectPage(Page? page)
    {
        using var operation = EnterMutation();
        if (page is not null && !Pages.Contains(page)) throw new ArgumentException("The page is not retained by this tab container.", nameof(page));
        var previous = SelectedPage;
        // Reaffirming the current page from a changing callback is a newer decision too.
        long intent = ++selectionVersion;
        if (ReferenceEquals(previous, page)) return true;
        if (page is not null && (!page.Enabled || page.IsRetiringPage)) return false;
        bool Current() => !retiring && !IsDisposed && selectionVersion == intent && ReferenceEquals(SelectedPage, previous) &&
            (page is null || Pages.Contains(page) && host.Owns(page) && page.Enabled && !page.IsRetiringPage);
        var changing = new PageSelectionChangingEventArgs(previous, page);
        SelectedPageChanging?.Invoke(this, changing);
        if (changing.Cancel || !Current() || !host.AdmitDeparture(previous, out bool restore) || !Current()) return false;
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        Attempt(() => host.PresentPage(page));
        Attempt(UpdateHeaders);
        if (!retiring && selectionVersion == intent && ReferenceEquals(SelectedPage, page))
        {
            Attempt(() => NotifySelectionChanged(previous));
            if (restore && selectionVersion == intent) Attempt(host.RestorePageFocus);
            if (selectionVersion == intent) Attempt(RevealSelectedHeader);
        }
        ThrowFailures(failures);
        return !retiring && selectionVersion == intent && ReferenceEquals(SelectedPage, page);
    }

    internal bool InsertPage(int index, Page page)
    {
        using var operation = EnterMutation();
        ArgumentNullException.ThrowIfNull(page);
        if (index < 0 || index > Pages.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (Pages.Contains(page) || !reservations.Add(page)) throw new ArgumentException("The page is already retained or being admitted.", nameof(page));
        long intent = ++collectionVersion, selection = selectionVersion;
        bool committed = false;
        try
        {
            host.ValidatePage(page);
            if (!host.PreparePage(page) || retiring || collectionVersion != intent || !host.Owns(page)) return false;
            Pages.Entries.Insert(index, page);
            committed = true;
            var button = new HeaderButton(this, page);
            buttons.Add(page, button);
            page.TextChanged += PageMetadataChanged;
            page.EnabledChanged += PageMetadataChanged;
            headers.Controls.Add(button);
            SynchronizeHeaderOrder();
            List<Exception>? failures = null;
            void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
            Attempt(UpdateHeaders);
            Attempt(() => PagesChanged?.Invoke(this, EventArgs.Empty));
            if (!retiring && selectionVersion == selection && SelectedPage is null && Pages.Contains(page) && page.Enabled)
                Attempt(() => TrySelectPage(page));
            ThrowFailures(failures);
            return !retiring && Pages.Contains(page) && host.Owns(page);
        }
        finally
        {
            reservations.Remove(page);
            if (!committed && host.Owns(page) && !Pages.Contains(page)) host.ReleasePage(page);
        }
    }

    internal bool RemovePage(Page page)
    {
        using var operation = EnterMutation();
        ArgumentNullException.ThrowIfNull(page);
        int index = Pages.IndexOf(page);
        if (index < 0) return false;
        var parent = page.HostParent;
        long intent = ++collectionVersion;
        if (ReferenceEquals(SelectedPage, page) && !TrySelectPage(FindReplacement(page, index))) return false;
        if (retiring || collectionVersion != intent || !Pages.Contains(page) || !ReferenceEquals(parent, page.HostParent)) return false;
        RemoveEntry(page, release: true);
        return !Pages.Contains(page);
    }

    internal bool ClearPages()
    {
        using var operation = EnterMutation();
        var entries = Pages.Entries.ToArray();
        long intent = ++collectionVersion;
        if (!TrySelectPage(null) || retiring || collectionVersion != intent) return false;
        List<Exception>? failures = null;
        foreach (var page in entries)
        {
            if (retiring || collectionVersion != intent) break;
            try { RemoveEntry(page, release: true); }
            catch (Exception error) { (failures ??= []).Add(error); }
        }
        ThrowFailures(failures);
        return !retiring && Pages.Count == 0;
    }

    internal void MovePage(int oldIndex, int newIndex)
    {
        using var operation = EnterMutation();
        if (oldIndex < 0 || oldIndex >= Pages.Count) throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if (newIndex < 0 || newIndex >= Pages.Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        if (oldIndex == newIndex) return;
        collectionVersion++;
        var page = Pages.Entries[oldIndex];
        Pages.Entries.RemoveAt(oldIndex);
        Pages.Entries.Insert(newIndex, page);
        SynchronizeHeaderOrder();
        RevealSelectedHeader();
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    private Page? FindReplacement(Page removed, int index)
    {
        for (int i = index + 1; i < Pages.Count; i++) if (Pages[i].Enabled && !ReferenceEquals(Pages[i], removed)) return Pages[i];
        for (int i = index - 1; i >= 0; i--) if (Pages[i].Enabled && !ReferenceEquals(Pages[i], removed)) return Pages[i];
        return null;
    }

    private void RemoveEntry(Page page, bool release)
    {
        if (!Pages.Entries.Remove(page)) return;
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        page.TextChanged -= PageMetadataChanged;
        page.EnabledChanged -= PageMetadataChanged;
        if (buttons.Remove(page, out var button))
        {
            Attempt(() => headers.Controls.Remove(button));
            Attempt(button.Dispose);
        }
        if (release && host.Owns(page)) Attempt(() => host.ReleasePage(page));
        Attempt(UpdateHeaders);
        Attempt(() => PagesChanged?.Invoke(this, EventArgs.Empty));
        ThrowFailures(failures);
    }

    private void PageLost(Page page)
    {
        if (retiring || !Pages.Contains(page)) return;
        bool needsSelection = SelectedPage is null;
        int index = Pages.IndexOf(page);
        var next = FindReplacement(page, index);
        collectionVersion++;
        long selection = ++selectionVersion, notice = selectionNotificationVersion;
        List<Exception>? failures = null;
        try { RemoveEntry(page, release: false); } catch (Exception error) { failures = [error]; }
        if (needsSelection && !retiring && SelectedPage is null)
        {
            if (selectionVersion == selection && next is not null && Pages.Contains(next))
            {
                try { TrySelectPage(next); }
                catch (Exception error) { (failures ??= []).Add(error); }
            }
            // The old page is already lost. A veto/newer explicit empty choice can prevent
            // fallback, but cannot erase the mandatory transition to an empty selection.
            if (!retiring && SelectedPage is null && selectionNotificationVersion == notice)
            {
                try { NotifySelectionChanged(page); }
                catch (Exception error) { (failures ??= []).Add(error); }
            }
        }
        ThrowFailures(failures);
    }

    private void PageMetadataChanged(object? sender, EventArgs e) { if (!retiring) UpdateHeaders(); }
    private void NotifySelectionChanged(Page? previous)
    {
        long notice = ++selectionNotificationVersion;
        List<Exception>? failures = null;
        try { SelectedPageChanged?.Invoke(this, EventArgs.Empty); }
        catch (Exception error) { failures = [error]; }
        // Preserve canonical native delivery even when an application observer throws.
        // A newer nested selection publishes its own notice and retires this older one.
        if (!retiring && selectionNotificationVersion == notice)
        {
            try
            {
                if (previous is not null && buttons.TryGetValue(previous, out var old)) old.NotifyAccessibilityClients(AccessibleEvents.SelectionRemove);
            }
            catch (Exception error) { (failures ??= []).Add(error); }
            try
            {
                if (!retiring && selectionNotificationVersion == notice && SelectedPage is { } selected && buttons.TryGetValue(selected, out var current))
                    current.NotifyAccessibilityClients(AccessibleEvents.Selection);
            }
            catch (Exception error) { (failures ??= []).Add(error); }
            try { if (!retiring && selectionNotificationVersion == notice) headers.NotifyAccessibilityClients(AccessibleEvents.SelectionWithin); }
            catch (Exception error) { (failures ??= []).Add(error); }
        }
        ThrowFailures(failures);
    }
    private void HostChanged() => selectionVersion++;
    private void UpdateHeaders()
    {
        var selected = SelectedPage;
        var stop = selected?.Enabled == true ? selected : Pages.FirstOrDefault(page => page.Enabled);
        foreach (var pair in buttons.ToArray())
        {
            if (retiring || pair.Value.IsDisposed || !Pages.Contains(pair.Key)) continue;
            pair.Value.Text = pair.Key.Title;
            pair.Value.TabStop = ReferenceEquals(pair.Key, stop);
            pair.Value.Action.RaiseCanExecuteChanged();
            pair.Value.Invalidate();
        }
    }
    private void SynchronizeHeaderOrder()
    {
        // Keep canonical scrolling in its positive coordinate space. Header controls are
        // physically reversed for RTL, while page membership and keyboard order stay logical.
        // General inherited RTL/negative-extent FlowLayout behavior belongs to issue #89.
        long membership = collectionVersion;
        var requestedDirection = direction;
        var ordered = requestedDirection == FlowDirection.LeftToRight ? Pages.Entries.ToArray() : Pages.Entries.AsEnumerable().Reverse().ToArray();
        // Address final indices rather than rotating every header through the front.
        // An ordinary LTR append now leaves all existing native/layout positions intact;
        // an RTL append moves only the new leading header.
        for (int index = 0; index < ordered.Length; index++)
        {
            // Layout/native observers can mutate again. Their newer ordering owns the row.
            if (retiring || membership != collectionVersion || requestedDirection != direction) return;
            if (buttons.TryGetValue(ordered[index], out var button)) headers.Controls.SetChildIndex(button, index);
        }
        if (retiring || membership != collectionVersion || requestedDirection != direction) return;
        headers.PerformLayout();
    }
    private void RevealSelectedHeader()
    {
        if (SelectedPage is { } page && buttons.TryGetValue(page, out var button)) ControlAccessibleObject.RevealControlIntoView(button);
    }

    private void SelectRelative(bool forward, bool end = false)
    {
        if (Pages.Count == 0) return;
        bool fromHeader = GetFocusScope().Owner is HeaderButton button && ReferenceEquals(button.TabOwner, this);
        int start = end ? forward ? -1 : Pages.Count : SelectedIndex < 0 ? forward ? -1 : 0 : SelectedIndex;
        for (int step = 1; step <= Pages.Count; step++)
        {
            int index = end ? forward ? step - 1 : Pages.Count - step
                : (start + (forward ? step : -step) + Pages.Count * 2) % Pages.Count;
            var page = Pages[index];
            if (!page.Enabled || page.IsRetiringPage) continue;
            if (TrySelectPage(page) && fromHeader && ReferenceEquals(SelectedPage, page) && buttons.TryGetValue(page, out var target)) target.Select();
            break;
        }
    }
    private void Bind(Control control, Keys key, KeyModifiers modifiers, Action execute)
    {
        var command = new DelegateCommand(execute, () => !retiring && Enabled && Pages.Any(page => page.Enabled));
        control.InputBindings.Add(new KeyBinding(command, new KeyGesture(key, modifiers)));
    }
    private Mutation EnterMutation()
    {
        VerifyPageAccess();
        ObjectDisposedException.ThrowIf(retiring, this);
        if (mutationDepth >= 32) throw new InvalidOperationException("Tab mutations did not settle after 32 nested calls.");
        mutationDepth++;
        return new(this);
    }
    private readonly struct Mutation(TabbedPage owner) : IDisposable { public void Dispose() => owner.mutationDepth--; }
    private static void ThrowFailures(List<Exception>? failures)
    {
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null) throw new AggregateException("Tab page callbacks failed after commitment.", failures);
    }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(600, 400);
    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (host is null || headers is null || retiring) return;
        var area = LogicalPaddedClientRectangle;
        int height = Math.Min(56, area.Height);
        headers.Bounds = new(area.X, area.Y, area.Width, height);
        host.Bounds = new(area.X, area.Y + height, area.Width, Math.Max(0, area.Height - height));
        host.RefreshPagePresentation();
    }
    /// <inheritdoc/>
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (host is not null && !retiring) UpdateHeaders(); }
    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new TabContainerAccessibleObject(this);
    private sealed class TabContainerAccessibleObject(TabbedPage owner) : ControlAccessibleObject(owner)
    {
        internal override bool HasViewVisibility => true;
        public override AccessibleControlType ControlType => AccessibleControlType.Pane;
        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (Owner is TabbedPage { retiring: false } current) { yield return current.headers.AccessibilityObject; yield return current.host.AccessibilityObject; }
            foreach (var child in base.GetAccessibilityChildren()) yield return child;
        }
    }
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (retiring) return;
        retiring = true;
        selectionVersion++; collectionVersion++;
        host.PageLost -= PageLost;
        host.HostChanged -= HostChanged;
        foreach (var page in Pages.Entries) { page.TextChanged -= PageMetadataChanged; page.EnabledChanged -= PageMetadataChanged; }
        Pages.Entries.Clear();
        reservations.Clear();
        buttons.Clear();
        base.Dispose(true);
    }

    private sealed class HeaderRow : FlowLayoutPanel
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new HeaderRowAccessibleObject(this);
        private sealed class HeaderRowAccessibleObject(HeaderRow owner) : ControlAccessibleObject(owner)
        {
            internal override bool HasViewVisibility => true;
            public override AccessibleRole Role => AccessibleRole.PageTabList;
            public override AccessibleControlType ControlType => AccessibleControlType.Tab;
        }
    }
    private sealed class HeaderButton : Button
    {
        internal TabbedPage TabOwner { get; }
        internal Page Page { get; }
        internal DelegateCommand Action { get; }
        internal HeaderButton(TabbedPage owner, Page page)
        {
            TabOwner = owner; Page = page;
            Width = 144; Height = 34; Margin = new Padding(2);
            Action = new DelegateCommand(() => owner.TrySelectPage(page), () => !owner.retiring && owner.Pages.Contains(page) && page.Enabled && !page.IsRetiringPage);
            Command = Action;
            owner.Bind(this, Keys.Right, KeyModifiers.None, () => owner.SelectRelative(owner.direction == FlowDirection.LeftToRight));
            owner.Bind(this, Keys.Left, KeyModifiers.None, () => owner.SelectRelative(owner.direction != FlowDirection.LeftToRight));
            owner.Bind(this, Keys.Home, KeyModifiers.None, () => owner.SelectRelative(true, end: true));
            owner.Bind(this, Keys.End, KeyModifiers.None, () => owner.SelectRelative(false, end: true));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ReferenceEquals(TabOwner.SelectedPage, Page))
            {
                int thickness = e.LogicalToDeviceUnits(2);
                e.Canvas.FillRectangle(new Rectangle(0, ClientRectangle.Bottom - thickness, ClientRectangle.Width, thickness), Theme.ForegroundColor);
            }
        }
        protected override AccessibleObject CreateAccessibilityInstance() => new HeaderAccessibleObject(this);
        private sealed class HeaderAccessibleObject(HeaderButton owner) : ControlAccessibleObject(owner)
        {
            internal override bool HasViewVisibility => true;
            // A retained platform peer must neither keep the page graph alive nor activate
            // a removed header. Reuse the canonical peer's weak Control owner and membership.
            private HeaderButton? Current => Owner is HeaderButton { IsDisposed: false } button && !button.TabOwner.retiring &&
                button.TabOwner.buttons.TryGetValue(button.Page, out var retained) && ReferenceEquals(retained, button) &&
                button.TabOwner.host.Owns(button.Page) ? button : null;
            public override AccessibleRole Role => AccessibleRole.PageTab;
            public override AccessibleControlType ControlType => AccessibleControlType.TabItem;
            public override AccessibilityView View => Current is null ? AccessibilityView.Hidden : base.View;
            public override Rectangle Bounds => Current is null ? Rectangle.Empty : base.Bounds;
            public override AccessibleObject? Parent => Current is null ? null : base.Parent;
            public override AccessibleStates State => base.State | AccessibleStates.Selectable |
                (Current is { } button && ReferenceEquals(button.TabOwner.SelectedPage, button.Page) ? AccessibleStates.Selected : AccessibleStates.None);
            public override AccessibleActions SupportedActions => Current is { } button
                ? base.SupportedActions | (button.Enabled ? AccessibleActions.Select : AccessibleActions.None) : AccessibleActions.None;
            public override bool PerformAction(AccessibleActions action, object? parameter = null)
            {
                if (Current is not { } button) return false;
                return action == AccessibleActions.Select && parameter is null && button.Enabled
                    ? button.TabOwner.TrySelectPage(button.Page) : base.PerformAction(action, parameter);
            }
        }
    }
}
