using System.Diagnostics;
using System.Drawing;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.Layout;
using ModernFormsNext.Virtualization;

namespace ModernFormsNext;

public abstract partial class ItemsControl
{
    private SizeF contentExtent;
    private long lastLayoutTicks, itemLayoutPasses;

    /// <inheritdoc/>
    VirtualizationContext IVirtualizingLayoutHost.GetVirtualizationContext() => GetItemsContext();

    private VirtualizationContext GetItemsContext(int? cache = null)
    {
        var display = DisplayRectangle;
        return new(sourceCount, new(TouchScrollPosition.X, TouchScrollPosition.Y,
            Math.Max(0, display.Width), Math.Max(0, display.Height)), itemSize, cache ?? cacheItemCount, metrics);
    }

    /// <inheritdoc/>
    bool IVirtualizingLayoutHost.ArrangeVirtualizedItems(VirtualizingLayout layout, in VirtualizationContext context)
    {
        if (!virtualizationEnabled || disposingItems || IsDisposed) return false;
        var currentGenerator = generator!;
        if (currentGenerator.IsProcessing)
        {
            // A template can change size/viewport before its row is attached. The generator
            // deliberately coalesces recursive Realize calls; arranging its partial mapping
            // here would mistake that valid in-progress row for an ownership violation.
            itemLayoutRequested = true;
            return false;
        }
        long version = itemsVersion;
        long started = Stopwatch.GetTimestamp();
        using var activity = PerformanceRecorder.Measure(PerformanceActivityKind.Custom, this);
        itemLayoutPasses++;
        try
        {
            int observedCount = itemsSource?.Count ?? 0;
            if (!IsItemsPassCurrent(version, currentGenerator)) return false;
            if (observedCount != sourceCount)
            {
                HandleItemsChanged(itemsSource, new(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
                return false;
            }
            contentExtent = layout.GetExtent(in context);
            var origin = LogicalScrollViewport.Location;
            CommonProperties.SetLayoutBounds(this, sourceCount == 0 ? Size.Empty : new(
                ScrollCoordinate(Math.Ceiling(contentExtent.Width) + origin.X + PresentationPadding.Left),
                ScrollCoordinate(Math.Ceiling(contentExtent.Height) + origin.Y + PresentationPadding.Top)));
            var range = layout.GetRealizedRange(in context);
            CaptureFocusedOccurrence();
            currentGenerator.Realize(range, sourceCount);
            if (!IsItemsPassCurrent(version, currentGenerator)) return false;
            var anchor = CaptureAnchor();
            bool measuredChange = false;
            for (int index = range.StartIndex; index < range.EndIndex; index++)
            {
                if (!currentGenerator.TryGetContainer(index, out var row)) continue;
                if (!ReferenceEquals(row.Parent, this) || row.IsDisposed)
                    throw new InvalidOperationException("An owned item container was disposed or reparented by application code.");
                if (metrics is not null)
                {
                    var measuredItem = GetItem(index);
                    if (!IsItemsPassCurrent(version, currentGenerator)) return false;
                    double extent = MeasureItemContainer(row, measuredItem, index, itemSize);
                    if (!IsItemsPassCurrent(version, currentGenerator)) return false;
                    measuredChange |= metrics.SetMeasuredExtent(index, extent);
                }
                var bounds = layout.GetItemBounds(in context, index);
                bounds.Offset(DisplayRectangle.Location);
                row.SetBounds(ScrollCoordinate(Math.Round(bounds.X)), ScrollCoordinate(Math.Round(bounds.Y)),
                    ScrollCoordinate(Math.Ceiling(bounds.Width)), ScrollCoordinate(Math.Ceiling(bounds.Height)));
                if (!IsItemsPassCurrent(version, currentGenerator)) return false;
                var arrangedItem = GetItem(index);
                if (!IsItemsPassCurrent(version, currentGenerator)) return false;
                OnItemContainerArranged(row, arrangedItem, index);
                if (!IsItemsPassCurrent(version, currentGenerator)) return false;
            }
            if (measuredChange)
            {
                // Measurements change only prefix metadata. Preserve the leading logical occurrence and
                // intra-cell offset before the existing layout/scrollbar path republishes corrected extent.
                RestoreAnchor(anchor);
                RequestItemsLayout();
            }
            else measurementAnchor = null;
            RestoreItemFocus();
            return false;
        }
        finally { lastLayoutTicks = Stopwatch.GetTimestamp() - started; }
    }

    private bool IsItemsPassCurrent(long version, ItemContainerGenerator<Control> active)
    {
        if (version == itemsVersion && ReferenceEquals(active, generator) && !disposingItems && !IsDisposed && !active.IsDisposed) return true;
        if (!disposingItems && !IsDisposed && generator?.IsDisposed == false) RequestItemsLayout();
        return false;
    }

    /// <summary>Scrolls an unrealized occurrence into view through the existing scrollbar state.</summary>
    /// <param name="index">The logical occurrence index.</param>
    /// <remarks>Requires enabled virtualization and the owning UI thread. Uses estimated bounds until measured;
    /// a following layout corrects extents while retaining the viewport anchor. Does not select or steal focus.</remarks>
    public void ScrollToItem(int index)
    {
        VerifyItemsAccess();
        if (!virtualizationEnabled) throw new InvalidOperationException("Enable virtualization before scrolling to an item.");
        if ((uint)index >= (uint)sourceCount) throw new ArgumentOutOfRangeException(nameof(index));
        long version = itemsVersion;
        PerformLayout();
        if (version != itemsVersion || IsDisposed || disposingItems || index >= sourceCount) return;
        var context = GetItemsContext();
        var bounds = itemLayout!.GetItemBounds(in context, index);
        var viewport = context.Viewport;
        static double Target(double start, double end, double current, double length)
            => start < current ? start : end > current + length ? Math.Min(start, end - length) : current;
        horizontalScroll.Value = Math.Clamp(ScrollCoordinate(Math.Round(Target(bounds.Left, bounds.Right, viewport.X, viewport.Width))),
            horizontalScroll.Minimum, horizontalScroll.Maximum);
        if (version != itemsVersion || IsDisposed || disposingItems || index >= sourceCount) return;
        verticalScroll.Value = Math.Clamp(ScrollCoordinate(Math.Round(Target(bounds.Top, bounds.Bottom, viewport.Y, viewport.Height))),
            verticalScroll.Minimum, verticalScroll.Maximum);
        if (version != itemsVersion || IsDisposed || disposingItems) return;
        PerformLayout();
        if (version == itemsVersion && !IsDisposed && !disposingItems && GetItemContainer(index) is { } row)
            ControlAccessibleObject.RevealControlIntoView(row);
    }

    /// <summary>Invalidates retained measurements after template content changes and requests layout.</summary>
    /// <remarks>Call on the UI thread after an application-specific metric policy changes. Font/theme/DPI hooks call this path automatically.</remarks>
    public void InvalidateItemMeasurements()
    {
        VerifyItemsAccess();
        ResetMetrics(CaptureAnchor());
    }

    /// <summary>Gets observed runtime counts, content extent and the most recent layout duration.</summary>
    /// <remarks>UI-thread snapshot; no realization, source enumeration or profiler is created. Counters describe the current generator lifetime.</remarks>
    public ItemVirtualizationSnapshot GetVirtualizationSnapshot()
    {
        VerifyItemsAccess();
        return new(sourceCount, RealizedRange, generator!, metrics?.MeasuredCount ?? 0, contentExtent,
            TimeSpan.FromSeconds((double)lastLayoutTicks / Stopwatch.Frequency), itemLayoutPasses);
    }

    private (int Index, double Offset) CaptureAnchor()
    {
        if (measurementAnchor is { } retained && retained.Index >= 0 && retained.Index < sourceCount) return retained;
        if (sourceCount == 0 || itemLayout is null) return (-1, 0);
        var context = GetItemsContext(0);
        var range = itemLayout.GetRealizedRange(in context);
        if (range.Count == 0) return (-1, 0);
        var bounds = itemLayout.GetItemBounds(in context, range.StartIndex);
        return (range.StartIndex, itemOrientation == Orientation.Vertical ? context.Viewport.Y - bounds.Y : context.Viewport.X - bounds.X);
    }

    private void RestoreAnchor((int Index, double Offset) anchor)
    {
        if (anchor.Index < 0 || anchor.Index >= sourceCount || !virtualizationEnabled) return;
        var context = GetItemsContext();
        var bounds = itemLayout!.GetItemBounds(in context, anchor.Index);
        var axis = itemOrientation == Orientation.Vertical ? verticalScroll : horizontalScroll;
        double coordinate = itemOrientation == Orientation.Vertical ? bounds.Y : bounds.X;
        // Range growth is published by ScrollableControl.OnLayout after this pass. Defer an anchor
        // beyond the old maximum until the corrected range exists rather than clamp it prematurely.
        pendingAnchor = (anchor.Index, anchor.Offset);
        double offset = anchor.Offset;
        if (measurementAnchor is not null && metrics is not null)
            offset = Math.Min(offset, Math.Max(0, metrics.GetExtent(anchor.Index) - 1));
        int target = Math.Max(0, ScrollCoordinate(Math.Round(coordinate + offset)));
        if (target <= axis.Maximum) { pendingAnchor = null; axis.Value = Math.Clamp(target, axis.Minimum, axis.Maximum); }
    }

    private (int Index, double Offset)? pendingAnchor;
    private (int Index, double Offset)? measurementAnchor;

    /// <inheritdoc/>
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if (width == Width && height == Height) { base.SetBoundsCore(x, y, width, height, specified); return; }
        var anchor = CaptureAnchor();
        long version = ++itemsVersion;
        SuspendLayout();
        try
        {
            base.SetBoundsCore(x, y, width, height, specified);
            if (version == itemsVersion && !disposingItems && !IsDisposed) RestoreAnchor(anchor);
        }
        finally { ResumeLayout(!disposingItems); }
    }

    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (pendingAnchor is { } anchor && !disposingItems && !IsDisposed)
        {
            pendingAnchor = null;
            RestoreAnchor(anchor);
            // An extent beyond representable scroll range is rejected during extent publication.
            // A removed final item may leave an anchor outside the corrected maximum: clamp once.
            if (pendingAnchor is not null)
            {
                pendingAnchor = null;
                var axis = itemOrientation == Orientation.Vertical ? verticalScroll : horizontalScroll;
                axis.Value = axis.Maximum;
            }
        }
    }

    private void ResetMetrics((int Index, double Offset) anchor, bool requestLayout = true)
    {
        itemsVersion++;
        pendingAnchor = null;
        measurementAnchor = variableItemSize && anchor.Index >= 0 ? anchor : null;
        if (variableItemSize)
        {
            double estimate = itemOrientation == Orientation.Vertical ? itemSize.Height : itemSize.Width;
            if (metrics is null) metrics = new(sourceCount, estimate);
            else metrics.Reset(sourceCount, estimate);
        }
        else metrics = null;
        if (requestLayout)
        {
            RestoreAnchor(anchor);
            RequestItemsLayout();
            Invalidate();
        }
    }

    private static int ScrollCoordinate(double value)
    {
        if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue)
            throw new OverflowException("Item content exceeds the supported finite integer scroll coordinates. Use a bounded source window.");
        return (int)value;
    }

    private bool itemLayoutRequested, completingItemLayout;

    private void RequestItemsLayout()
    {
        if (disposingItems || IsDisposed) return;
        itemLayoutRequested = IsLayoutSuspended;
        PerformLayout();
    }

    internal override void OnLayoutCompleted()
    {
        base.OnLayoutCompleted();
        if (completingItemLayout || !itemLayoutRequested || disposingItems || IsDisposed || generator?.IsProcessing == true) return;
        completingItemLayout = true;
        try
        {
            // Control clears deferred flags after an active pass. Consume this host's pending
            // metric/source/viewport correction only after that canonical pass has unsuspended.
            // Every iteration still runs the existing LayoutEngine/ScrollableControl path.
            for (int pass = 0; itemLayoutRequested && !disposingItems && !IsDisposed; pass++)
            {
                if (pass == 64) throw new InvalidOperationException("Item layout callbacks did not stabilize after 64 passes.");
                itemLayoutRequested = false;
                PerformLayout();
            }
        }
        finally { completingItemLayout = false; }
    }
}

/// <summary>Reports actual counts/extent/time from one ItemsControl generator and its canonical layout path.</summary>
/// <remarks>Counters describe container operations, not native rendering or successful cleanup of thrown disposal callbacks.</remarks>
public readonly struct ItemVirtualizationSnapshot
{
    internal ItemVirtualizationSnapshot(int count, RealizedRange range, ItemContainerGenerator<Control> generator,
        int measured, SizeF extent, TimeSpan duration, long passes)
    {
        ItemCount = count; RealizedRange = range; RealizedContainerCount = generator.RealizedContainerCount;
        RecycledContainerCount = generator.RecycledContainerCount; CreatedContainerCount = generator.CreatedContainerCount;
        ReusedContainerCount = generator.ReusedContainerCount; DiscardedContainerCount = generator.DiscardedContainerCount;
        MeasuredItemCount = measured; ContentExtent = extent; LastLayoutDuration = duration; LayoutPassCount = passes;
    }
    /// <summary>Gets the finite logical source count.</summary>
    public int ItemCount { get; }
    /// <summary>Gets the completely realized viewport/cache range.</summary>
    public RealizedRange RealizedRange { get; }
    /// <summary>Gets the currently mapped live container count.</summary>
    public int RealizedContainerCount { get; }
    /// <summary>Gets the cleared containers retained in the bounded pool.</summary>
    public int RecycledContainerCount { get; }
    /// <summary>Gets the distinct containers created by this generator.</summary>
    public long CreatedContainerCount { get; }
    /// <summary>Gets acquisitions from the cleared container pool.</summary>
    public long ReusedContainerCount { get; }
    /// <summary>Gets discard callback invocations.</summary>
    public long DiscardedContainerCount { get; }
    /// <summary>Gets retained explicit numeric measurements.</summary>
    public int MeasuredItemCount { get; }
    /// <summary>Gets the last calculated full content extent in logical pixels.</summary>
    public SizeF ContentExtent { get; }
    /// <summary>Gets elapsed time of the last layout-host pass, including callbacks.</summary>
    public TimeSpan LastLayoutDuration { get; }
    /// <summary>Gets layout-host passes executed by this control.</summary>
    public long LayoutPassCount { get; }
}
