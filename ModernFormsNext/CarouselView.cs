using System.Collections;
using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Virtualization;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext;

/// <summary>Displays viewport-sized pages through one owned, recyclable CollectionView.</summary>
/// <remarks>
/// Use on the owning UI thread. Source values and the runtime factory are borrowed; generated
/// controls are owned by the existing CollectionView generator. Selection is that generator's
/// logical occurrence, not a second page index. Navigation snaps immediately, without animation.
/// Interactive page descendants retain their own input and canonical accessibility providers.
/// </remarks>
/// <example><code>
/// var carousel = new CarouselView { Dock = DockStyle.Fill, ItemsSource = new[] { "One", "Two" } };
/// carousel.MoveNext();
/// carousel.SelectedIndexChanged += (_, _) =&gt; Console.WriteLine(carousel.SelectedItem);
/// </code></example>
[DefaultProperty(nameof(PageOrientation)), DefaultEvent(nameof(SelectedIndexChanged))]
public class CarouselView : Control
{
    private readonly PageCollection pages;
    private long requestVersion;
    // Distinguish explicit source/selection requests from the canonical deselection event
    // emitted during replacement. Only explicit newer requests suppress initial selection.
    private long explicitSelectionRequest;
    private bool snapping;

    /// <summary>Creates an empty horizontal carousel using shared collection virtualization.</summary>
    public CarouselView()
    {
        pages = new PageCollection(this);
        Controls.AddImplicitControl(pages);
        pages.SelectedIndexChanged += HandleSelectionChanged;
        pages.ItemInvoked += (_, e) => ItemInvoked?.Invoke(this, e);
        SetControlBehavior(ControlBehaviors.Selectable, true);
        InputBindings.Add(new KeyBinding(new DelegateCommand(() => MovePrevious()), new(Keys.PageUp, KeyModifiers.Control)));
        InputBindings.Add(new KeyBinding(new DelegateCommand(() => MoveNext()), new(Keys.PageDown, KeyModifiers.Control)));
    }

    /// <summary>Gets or sets the borrowed indexed page source, or null for an empty carousel.</summary>
    /// <remarks>
    /// UI thread only. A nonempty replacement selects its first page unless a callback makes a
    /// newer selection/source request. Indexed notifications use CollectionView occurrence
    /// mappings; remove/replace/reset may clear selection. Call RefreshItems for a plain IList.
    /// The source/values are not disposed. This runtime property is not serialized by Designer.
    /// </remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IList? ItemsSource
    {
        get { VerifyAccess(); return pages.ItemsSource; }
        set
        {
            VerifyAccess();
            if (ReferenceEquals(pages.ItemsSource, value)) return;
            long request = ++explicitSelectionRequest;
            requestVersion++;
            var lifetime = new AccessibilityControlLifetime(this);
            pages.ItemsSource = value;
            if (lifetime.IsCurrent && request == explicitSelectionRequest && ReferenceEquals(pages.ItemsSource, value) &&
                pages.ItemCount > 0 && pages.SelectedIndex < 0) SelectedIndex = 0;
        }
    }

    /// <summary>Gets or sets the borrowed runtime factory for recyclable page controls.</summary>
    /// <remarks>
    /// UI thread only. Null uses focusable text pages. CollectionView owns cleanup, descendant
    /// bindings and original-factory discard ordering. The factory is never disposed. Runtime-only.
    /// </remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IItemContainerFactory<Control>? ItemTemplate
    {
        get { VerifyAccess(); return pages.ItemTemplate; }
        set { VerifyAccess(); requestVersion++; pages.ItemTemplate = value; SnapSelectedPage(); }
    }

    /// <summary>Gets or sets the logical selected occurrence, or -1 for no selection.</summary>
    /// <remarks>UI thread only. Reveals the selected page without stealing keyboard focus. Reentrant newer requests win.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get { VerifyAccess(); return pages.SelectedIndex; }
        set
        {
            VerifyAccess();
            explicitSelectionRequest++;
            requestVersion++;
            var lifetime = new AccessibilityControlLifetime(this);
            pages.SelectedIndex = value;
            if (lifetime.IsCurrent) SnapSelectedPage();
        }
    }

    /// <summary>Gets the borrowed selected value, or null for no selection or a selected null value.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem { get { VerifyAccess(); return pages.SelectedItem; } }

    /// <summary>Gets the logical page count without enumerating source values.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ItemCount => pages.ItemCount;

    /// <summary>Gets or sets the page axis. A change requests layout and preserves logical selection.</summary>
    /// <remarks>UI thread only. Page dimensions are the shared scroll viewport in logical pixels.</remarks>
    [DefaultValue(Orientation.Horizontal)]
    public Orientation PageOrientation
    {
        get => pages.ItemOrientation;
        set { VerifyAccess(); requestVersion++; pages.ItemOrientation = value; SnapSelectedPage(); }
    }

    /// <summary>Gets or sets the shared nonnegative cache size on either side of the viewport.</summary>
    /// <remarks>UI thread only. Uses the existing bounded generator pool, not a private page cache.</remarks>
    [DefaultValue(2)]
    public int CacheItemCount
    {
        get => pages.CacheItemCount;
        set { VerifyAccess(); pages.CacheItemCount = value; }
    }

    /// <summary>Gets the current fixed page dimensions in logical pixels.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Size PageSize => pages.ItemSize;

    /// <summary>Occurs after the canonical logical selection is committed, before its reveal completes.</summary>
    /// <remarks>Handlers run on the UI thread and may navigate, replace source, change focus or dispose.</remarks>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>Occurs for activation of a page root through the existing CollectionView action path.</summary>
    /// <remarks>Interactive descendants keep their own actions; the arguments are borrowed value/index snapshots.</remarks>
    public event EventHandler<CollectionViewItemEventArgs>? ItemInvoked;

    /// <summary>Selects/reveals the next occurrence, or the first page when selection is empty.</summary>
    /// <returns>Whether a valid navigation request was made; a handler may subsequently redirect it.</returns>
    public bool MoveNext()
    {
        VerifyAccess();
        if (pages.SelectedIndex + 1 >= pages.ItemCount) return false;
        SelectedIndex = pages.SelectedIndex + 1;
        return true;
    }

    /// <summary>Selects/reveals the previous occurrence without wrapping at the source boundary.</summary>
    /// <returns>Whether a valid navigation request was made; false at the first page or with no selection.</returns>
    public bool MovePrevious()
    {
        VerifyAccess();
        if (pages.SelectedIndex <= 0) return false;
        SelectedIndex = pages.SelectedIndex - 1;
        return true;
    }

    /// <summary>Publishes a conservative source reset, retiring old occurrence identities and selection.</summary>
    /// <remarks>UI thread only. Use after plain IList changes; observable indexed changes do not need this call.</remarks>
    public void RefreshItems() { VerifyAccess(); requestVersion++; pages.RefreshItems(); }

    /// <summary>Gets a borrowed currently realized page root without materializing another page.</summary>
    /// <param name="index">The logical occurrence index.</param>
    /// <returns>The live page control, or null. Do not retain it as identity, reparent it or dispose it.</returns>
    public Control? GetItemContainer(int index) { VerifyAccess(); return pages.GetItemContainer(index); }

    /// <summary>Gets detached counters from the one canonical CollectionView generator.</summary>
    /// <returns>Actual shared virtualization counts and logical content extent.</returns>
    public ItemVirtualizationSnapshot GetVirtualizationSnapshot() { VerifyAccess(); return pages.GetVirtualizationSnapshot(); }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(IsDisposed || Disposing, this);
        GetFocusScope().VerifyAccess();
    }

    private void HandleSelectionChanged(object? sender, EventArgs e)
    {
        requestVersion++;
        var lifetime = new AccessibilityControlLifetime(this);
        long request = requestVersion;
        SelectedIndexChanged?.Invoke(this, e);
        if (lifetime.IsCurrent && request == requestVersion) SnapSelectedPage();
    }

    private void SnapSelectedPage()
    {
        // Indexed source notifications suspend the existing layout while mapping occurrences.
        // Reveal only after that canonical pass resumes; a synchronous retry cannot publish
        // the new range while it is suspended. PageCollection.OnLayoutCompleted revisits it.
        if (pages is null || snapping || IsDisposed || Disposing || pages.IsDisposed ||
            pages.IsLayoutSuspended || !Visible || pages.ActiveGesture is not null || pages.DirectScroll ||
            pages.LogicalScrollViewport.Width <= 0 || pages.LogicalScrollViewport.Height <= 0) return;
        snapping = true;
        try
        {
            // Reveal can call layout/source/scroll handlers. Re-read the existing selection
            // after each pass rather than restoring an old requested index over newer work.
            for (int pass = 0; pass < 64 && !IsDisposed && Visible; pass++)
            {
                int index = pages.SelectedIndex;
                if (index < 0 || index >= pages.ItemCount || pages.ActiveGesture is not null) return;
                var axis = PageOrientation == Orientation.Horizontal ? pages.HorizontalScrollProperties : pages.VerticalScrollProperties;
                // Use the canonical layout's finite rectangle coordinates. At large page
                // indices RectangleF rounds a cell origin; recomputing index * extent as an
                // integer would disagree with ScrollToItem and could never stabilize.
                var context = ((IVirtualizingLayoutHost)pages).GetVirtualizationContext();
                var bounds = ((VirtualizingLayout)pages.LayoutEngine).GetItemBounds(in context, index);
                long target = (long)Math.Round(PageOrientation == Orientation.Horizontal ? bounds.X : bounds.Y);
                if (axis.Value == Math.Clamp(target, axis.Minimum, axis.Maximum)) return;
                pages.ScrollToItem(index);
            }
            if (!IsDisposed && Visible) throw new InvalidOperationException("Carousel navigation did not stabilize after 64 callbacks.");
        }
        finally { snapping = false; }
    }

    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (pages is not null && !pages.IsDisposed) pages.Bounds = LogicalPaddedClientRectangle;
        base.OnLayout(e);
    }

    /// <inheritdoc/>
    protected override void OnVisibleChanged(EventArgs e)
    {
        requestVersion++;
        base.OnVisibleChanged(e);
        if (!IsDisposed && Visible) SnapSelectedPage();
    }

    /// <inheritdoc/>
    protected override void OnEnabledChanged(EventArgs e)
    {
        requestVersion++;
        base.OnEnabledChanged(e);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Focused || e.Control || e.Alt || IsDisposed || !Enabled) return;
        bool next = e.KeyCode == Keys.PageDown || e.KeyCode == (PageOrientation == Orientation.Horizontal ? Keys.Right : Keys.Down);
        bool previous = e.KeyCode == Keys.PageUp || e.KeyCode == (PageOrientation == Orientation.Horizontal ? Keys.Left : Keys.Up);
        if (next) e.Handled = MoveNext();
        else if (previous) e.Handled = MovePrevious();
    }

    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new CarouselAccessibleObject(this);

    private sealed class CarouselAccessibleObject(CarouselView owner) : ControlAccessibleObject(owner)
    {
        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (Owner is CarouselView { IsDisposed: false } current) yield return current.pages.AccessibilityObject;
            foreach (var child in base.GetAccessibilityChildren()) yield return child;
        }
    }

    private sealed class PageCollection : CollectionView
    {
        private readonly CarouselView? owner;
        internal object? ActiveGesture;
        internal bool DirectScroll;
        private long gestureVersion, gestureRequest;
        private AccessibilityControlLifetime? gestureLifetime;

        internal PageCollection(CarouselView owner)
        {
            this.owner = owner;
            ItemOrientation = Orientation.Horizontal;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (owner is not null)
            {
                var viewport = LogicalScrollViewport;
                var size = new Size(Math.Max(1, viewport.Width), Math.Max(1, viewport.Height));
                if (ItemSize != size) ItemSize = size;
            }
            base.OnLayout(e);
        }

        internal override void OnLayoutCompleted()
        {
            base.OnLayoutCompleted();
            owner?.SnapSelectedPage();
        }

        internal override bool CanStartTouchScroll(Point origin, Point movement)
            => ActiveGesture is null && base.CanStartTouchScroll(origin, movement) && (ItemOrientation == Orientation.Horizontal
                ? Math.Abs((long)movement.X) > Math.Abs((long)movement.Y)
                : Math.Abs((long)movement.Y) > Math.Abs((long)movement.X));

        internal override void BeginTouchScroll(object gesture, Point origin, Point movement)
        {
            if (ActiveGesture is not null || owner is null || !CanStartTouchScroll(origin, movement)) return;
            ActiveGesture = gesture;
            gestureVersion = ItemsGeneration;
            gestureRequest = owner.requestVersion;
            gestureLifetime = new AccessibilityControlLifetime(owner);
        }

        internal override bool ContinueTouchScroll(object gesture, Point movement, Point delta)
            => ReferenceEquals(ActiveGesture, gesture) && gestureVersion == ItemsGeneration &&
               owner?.requestVersion == gestureRequest && ScrollByTouchDelta(delta);

        internal override void EndTouchScroll(object gesture, bool canceled)
        {
            if (!ReferenceEquals(ActiveGesture, gesture) || owner is null) return;
            ActiveGesture = null;
            var lifetime = gestureLifetime;
            gestureLifetime = null;
            if (!canceled && !IsDisposed && Visible && Enabled && gestureVersion == ItemsGeneration &&
                owner.requestVersion == gestureRequest && lifetime?.IsCurrent == true && ItemCount > 0)
            {
                int extent = ItemOrientation == Orientation.Horizontal ? ItemSize.Width : ItemSize.Height;
                int position = ItemOrientation == Orientation.Horizontal ? TouchScrollPosition.X : TouchScrollPosition.Y;
                owner.SelectedIndex = Math.Clamp((int)Math.Round((double)position / extent, MidpointRounding.AwayFromZero), 0, ItemCount - 1);
            }
            if (!owner.IsDisposed && owner.Visible && lifetime?.IsCurrent == true) owner.SnapSelectedPage();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (e.Handled || owner is null || !Enabled || !Visible) return;
            int movement = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            // Paging consumes a whole step. The ordinary pixel-wheel implementation would
            // move only part of a cell and the page policy would immediately snap it back.
            if (movement < 0) e.Handled = owner.MoveNext();
            else if (movement > 0) e.Handled = owner.MovePrevious();
            else base.OnMouseWheel(e);
        }

        protected override void OnScroll(ScrollEventArgs e)
        {
            var lifetime = owner is null ? null : new AccessibilityControlLifetime(owner);
            long request = owner?.requestVersion ?? 0, version = ItemsGeneration;
            base.OnScroll(e);
            if (owner is not null && !owner.snapping && !DirectScroll && ActiveGesture is null &&
                lifetime?.IsCurrent == true && request == owner.requestVersion &&
                version == ItemsGeneration && ItemCount > 0 && SelectedIndex >= 0)
                owner.SelectedIndex = NearestPage(e.NewValue);
        }

        private int NearestPage(int position)
        {
            int extent = ItemOrientation == Orientation.Horizontal ? ItemSize.Width : ItemSize.Height;
            return Math.Clamp((int)Math.Round((double)position / extent, MidpointRounding.AwayFromZero), 0, ItemCount - 1);
        }

        internal override bool RevealAccessibilityItem(int index)
        {
            if (owner is null) return false;
            var lifetime = new AccessibilityControlLifetime(owner);
            long version = ItemsGeneration;
            var scope = GetFocusScope();
            long focus = scope.Version;
            owner.SelectedIndex = index;
            if (!lifetime.IsCurrent || version != ItemsGeneration || SelectedIndex != index ||
                scope.Version != focus && !ReferenceEquals(scope.Owner, this)) return false;
            return base.RevealAccessibilityItem(index);
        }

        internal override bool PerformAccessibleScroll(AccessibleScrollRequest request)
        {
            if (owner is null) return false;
            var lifetime = new AccessibilityControlLifetime(owner);
            long version = ItemsGeneration, navigation = owner.requestVersion;
            bool handled;
            DirectScroll = true;
            try { handled = base.PerformAccessibleScroll(request); }
            finally { DirectScroll = false; }
            if (!handled || !lifetime.IsCurrent || version != ItemsGeneration || ItemCount == 0) return false;
            if (navigation != owner.requestVersion) { owner.SnapSelectedPage(); return false; }
            int position = ItemOrientation == Orientation.Horizontal ? TouchScrollPosition.X : TouchScrollPosition.Y;
            int expected = NearestPage(position);
            owner.SelectedIndex = expected;
            return lifetime.IsCurrent && version == ItemsGeneration && SelectedIndex == expected;
        }
    }
}
