using System.Collections;
using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Layout;
using ModernFormsNext.Virtualization;

namespace ModernFormsNext;

/// <summary>Chooses the shared geometry strategy for an <see cref="ItemsControl"/>.</summary>
public enum ItemsLayout
{
    /// <summary>A single vertical or horizontal stack, optionally with measured item extents.</summary>
    Stack,
    /// <summary>Fixed cells in rows or columns that wrap across the current viewport.</summary>
    Wrap
}

/// <summary>Hosts a borrowed indexed source with bounded, owned and recycled visual item containers.</summary>
/// <remarks>
/// Derive to expose a source or template API. SetItemsSource subscribes to indexed collection notifications;
/// source objects remain application-owned. All source mutations, properties and template hooks require the
/// owning UI thread. Layout, scrolling, binding, input and focus use the existing Control contracts.
/// Numeric metrics may scale with item count; live controls scale with the viewport/cache and a 64-container pool.
/// Counts and content extent must fit the framework's finite integer scroll coordinates. Async/paged providers
/// can expose a finite cached IList window and publish changes on the UI thread; this host performs no I/O.
/// </remarks>
/// <example><code>
/// sealed class LogList : ItemsControl
/// {
///     public void SetEntries(System.Collections.IList entries) => SetItemsSource(entries);
///     protected override void PrepareItemContainer(Control row, object? item, int index)
///         => row.Text = item?.ToString() ?? string.Empty;
/// }
/// </code></example>
public abstract partial class ItemsControl : ScrollableControl, IVirtualizingLayoutHost
{
    private IList? itemsSource;
    private ItemContainerGenerator<Control>? generator;
    private VirtualizingLayout? itemLayout;
    private VirtualizingItemMetrics? metrics;
    private Size itemSize = new(160, 32);
    private ItemsLayout itemsLayout;
    private Orientation itemOrientation = Orientation.Vertical;
    private int cacheItemCount = 2, sourceCount, selectedIndex = -1, focusedIndex = -1;
    private bool virtualizationEnabled, variableItemSize, disposingItems, restoreFocus;
    private long itemsVersion;
    private readonly Dictionary<Control, ItemBinding> itemBindings = new();
    private readonly ScrollProperties horizontalScroll, verticalScroll;

    private readonly record struct ItemBinding(object? Item, int Index);

    /// <summary>Gets the borrowed indexed source currently committed by <see cref="SetItemsSource"/>.</summary>
    /// <remarks>UI thread only. Expose this state from a concrete consumer instead of retaining a second source reference.</remarks>
    protected IList? ItemsSourceCore { get { VerifyItemsAccess(); return itemsSource; } }

    // Derived framework consumers use the same generation as layout/input when a callback
    // can replace the source or template. This is not another selection/lifecycle epoch.
    internal long ItemsGeneration => itemsVersion;

    /// <summary>Retires all owned containers after changing a runtime template, preserving logical selection.</summary>
    /// <remarks>
    /// UI thread only. Old bindings are cleared and containers discarded through their original hooks;
    /// variable measurements are reset and layout is requested. Reentrant replacement/disposal wins.
    /// This does not reset the borrowed source or its logical occurrence identity.
    /// </remarks>
    protected void ResetItemContainers()
    {
        VerifyItemsAccess();
        var anchor = CaptureAnchor();
        CaptureFocusedOccurrence();
        ResetMetrics(anchor, requestLayout: false);
        long version = itemsVersion;
        ReplaceGenerator();
        if (version != itemsVersion || IsDisposed || disposingItems) return;
        RestoreAnchor(anchor);
        if (version == itemsVersion && !IsDisposed && !disposingItems) { RequestItemsLayout(); Invalidate(); }
    }

    /// <summary>Initializes a virtualized host without enumerating or owning a source.</summary>
    protected ItemsControl()
    {
        horizontalScroll = HorizontalScrollProperties;
        verticalScroll = VerticalScrollProperties;
        itemLayout = new VirtualizingStackLayout();
        generator = CreateGenerator();
        virtualizationEnabled = true;
        AutoScroll = true;
        SetControlBehavior(ControlBehaviors.Selectable, true);
    }

    /// <summary>Gets or sets whether the shared host realizes bounded item containers.</summary>
    /// <remarks>Changing this value retires old containers and requests layout. Disabling leaves legacy rendering to the derived control.</remarks>
    [DefaultValue(true)]
    public virtual bool VirtualizationEnabled
    {
        get => virtualizationEnabled;
        set
        {
            VerifyItemsAccess();
            if (virtualizationEnabled == value) return;
            SuspendLayout();
            try
            {
                // Commit the related mode/scroll/extent state before retiring containers can
                // call a template. A nested mode change must remain the newest complete state.
                virtualizationEnabled = value;
                itemsVersion++;
                AutoScroll = value;
                CommonProperties.SetLayoutBounds(this, Size.Empty);
                ReplaceGenerator();
                if (!disposingItems && !IsDisposed) { RequestItemsLayout(); Invalidate(); }
            }
            finally { ResumeLayout(!disposingItems && !IsDisposed); }
        }
    }

    /// <summary>Gets or sets the fixed/estimated cell size, in positive logical pixels.</summary>
    /// <remarks>Includes cell spacing. Changing it resets measurements, retains the viewport occurrence anchor and requests layout.</remarks>
    [DefaultValue(typeof(Size), "160, 32")]
    public virtual Size ItemSize
    {
        get => itemSize;
        set
        {
            VerifyItemsAccess();
            if (value.Width <= 0 || value.Height <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (itemSize == value) return;
            var anchor = CaptureAnchor();
            itemSize = value;
            ResetMetrics(anchor);
        }
    }

    /// <summary>Gets or sets stack or fixed-cell wrap geometry.</summary>
    /// <remarks>Requests layout without recreating the source. Wrap geometry does not support variable extents.</remarks>
    [DefaultValue(ItemsLayout.Stack)]
    public virtual ItemsLayout ItemsLayout
    {
        get => itemsLayout;
        set
        {
            VerifyItemsAccess();
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (value == ItemsLayout.Wrap && variableItemSize) throw new InvalidOperationException("Wrap layout requires fixed item sizes.");
            if (itemsLayout == value) return;
            itemsLayout = value;
            UpdateItemLayout();
        }
    }

    /// <summary>Gets or sets the primary stacking direction; vertical wrap advances by rows.</summary>
    /// <remarks>Changing direction resets measurements and requests layout; no device coordinates are stored.</remarks>
    [DefaultValue(Orientation.Vertical)]
    public Orientation ItemOrientation
    {
        get => itemOrientation;
        set
        {
            VerifyItemsAccess();
            if (value is not Orientation.Vertical and not Orientation.Horizontal) throw new ArgumentOutOfRangeException(nameof(value));
            if (itemOrientation == value) return;
            itemOrientation = value;
            UpdateItemLayout();
        }
    }

    /// <summary>Gets or sets the number of additional logical occurrences realized on each viewport side.</summary>
    /// <remarks>Nonnegative; changing it requests layout. Wrap geometry realizes complete intersecting rows/columns before adding this buffer.</remarks>
    [DefaultValue(2)]
    public int CacheItemCount
    {
        get => cacheItemCount;
        set
        {
            VerifyItemsAccess();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (cacheItemCount == value) return;
            cacheItemCount = value; itemsVersion++; RequestItemsLayout();
        }
    }

    /// <summary>Gets or sets whether stack cells use retained measured primary extents.</summary>
    /// <remarks>MeasureItemContainer returns positive logical extents. Unknown items use ItemSize; corrections preserve the leading viewport occurrence.</remarks>
    [DefaultValue(false)]
    public bool VariableItemSize
    {
        get => variableItemSize;
        set
        {
            VerifyItemsAccess();
            if (value && itemsLayout == ItemsLayout.Wrap) throw new InvalidOperationException("Wrap layout requires fixed item sizes.");
            if (variableItemSize == value) return;
            var anchor = CaptureAnchor();
            variableItemSize = value;
            ResetMetrics(anchor);
        }
    }

    /// <summary>Gets the indexed logical count without enumerating source values.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ItemCount => sourceCount;

    /// <summary>Gets the last fully realized range, including cache items.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RealizedRange RealizedRange => generator?.RealizedRange ?? RealizedRange.Empty;

    /// <summary>Gets or sets the selected logical occurrence, or -1 for no selection.</summary>
    /// <remarks>Selection survives visual recycling. Indexed changes map occurrences; remove/replace/reset clear selection. Setting does not change focus or scroll.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            VerifyItemsAccess();
            if (value < -1 || value >= sourceCount) throw new ArgumentOutOfRangeException(nameof(value));
            if (selectedIndex == value) return;
            selectedIndex = value;
            OnSelectedIndexChanged(EventArgs.Empty);
            if (!IsDisposed && !disposingItems) Invalidate();
        }
    }

    /// <summary>Occurs on the UI thread after the logical selection is committed, including collection mappings.</summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>Raises SelectedIndexChanged after the current occurrence has been committed.</summary>
    /// <remarks>Call base to notify observers. Observers may replace the source, select again or dispose the host.</remarks>
    protected virtual void OnSelectedIndexChanged(EventArgs e)
    {
        SelectedIndexChanged?.Invoke(this, e);
        if (!IsDisposed && !disposingItems)
        {
            // A default template has a cached child backbuffer. Refresh only bounded realized
            // children; logical selection does not enumerate or invalidate unrealized items.
            foreach (var row in Controls.ToArray())
                if (!row.IsDisposed && ReferenceEquals(row.Parent, this)) row.Invalidate();
            Invalidate();
        }
    }

    /// <inheritdoc/>
    public override LayoutEngine LayoutEngine => virtualizationEnabled && itemLayout is not null ? itemLayout : base.LayoutEngine;

    /// <summary>Gets a realized container without creating one or changing selection/focus.</summary>
    /// <param name="index">The logical occurrence index.</param>
    /// <returns>The currently realized container, or null.</returns>
    /// <remarks>Borrowed UI-thread reference; do not dispose/reparent it or retain it as item identity.</remarks>
    public Control? GetItemContainer(int index)
    {
        VerifyItemsAccess();
        return generator is not null && generator.TryGetContainer(index, out var container) ? container : null;
    }

    /// <summary>Gets a source value without materializing other occurrences.</summary>
    /// <param name="index">An index smaller than ItemCount.</param>
    /// <returns>The borrowed source object; null is a valid value.</returns>
    protected object? GetItem(int index)
    {
        if ((uint)index >= (uint)sourceCount) throw new ArgumentOutOfRangeException(nameof(index));
        return itemsSource![index];
    }

    private void VerifyItemsAccess()
    {
        ObjectDisposedException.ThrowIf(IsDisposed || disposingItems, this);
        GetFocusScope().VerifyAccess();
    }

    internal void VerifyItemMutationAccess() => VerifyItemsAccess();

    private void UpdateItemLayout()
    {
        itemLayout = itemsLayout == ItemsLayout.Stack ? new VirtualizingStackLayout(itemOrientation) : new VirtualizingWrapLayout(itemOrientation);
        ResetMetrics((-1, 0));
    }

    private void ReplaceGenerator()
    {
        SuspendLayout();
        try
        {
            var old = generator;
            generator = CreateGenerator();
            generator.Realize(RealizedRange.Empty, sourceCount);
            old?.Dispose();
        }
        finally { ResumeLayout(!disposingItems); }
    }

    /// <inheritdoc/>
    protected override void OnScrollViewportChanged(EventArgs e)
    {
        base.OnScrollViewportChanged(e);
        if (virtualizationEnabled && !disposingItems && !IsDisposed) RequestItemsLayout();
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(EventArgs e)
    {
        itemsVersion++;
        restoreFocus = false;
        base.OnParentChanged(e);
        if (!disposingItems && !IsDisposed) RequestItemsLayout();
    }

    /// <inheritdoc/>
    protected override void OnFontChanged(EventArgs e)
    {
        var anchor = CaptureAnchor();
        base.OnFontChanged(e);
        if (!disposingItems && !IsDisposed) ResetMetrics(anchor);
    }

    /// <inheritdoc/>
    protected internal override void OnThemeChanged(EventArgs e)
    {
        var anchor = CaptureAnchor();
        base.OnThemeChanged(e);
        if (!disposingItems && !IsDisposed) ResetMetrics(anchor);
    }

    /// <inheritdoc/>
    protected override void OnDpiChanged(EventArgs e)
    {
        var anchor = CaptureAnchor();
        if (!disposingItems && !IsDisposed) ResetMetrics(anchor);
        base.OnDpiChanged(e);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (disposingItems) return;
        disposingItems = true; itemsVersion++;
        UnsubscribeSource(); itemsSource = null;
        try { generator?.Dispose(); }
        finally { base.Dispose(true); }
    }
}
