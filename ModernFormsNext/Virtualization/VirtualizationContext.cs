using System.Drawing;

namespace ModernFormsNext.Virtualization;

/// <summary>Describes a finite indexed source and its unscrolled-content viewport for a virtualization layout pass.</summary>
/// <remarks>
/// Coordinates and sizes use logical pixels, before backend DPI conversion. Viewport X/Y are content scroll offsets;
/// the host translates returned item bounds into its existing padded/scrolled DisplayRectangle. A context owns no
/// source or controls and must not be used concurrently with changes to its optional borrowed metric map.
/// The default struct value is invalid; construct a context explicitly, including for an empty source.
/// </remarks>
public readonly struct VirtualizationContext
{
    /// <summary>Creates a validated layout context without enumerating or realizing the source.</summary>
    /// <param name="itemCount">The finite, nonnegative indexed source count.</param>
    /// <param name="viewport">The finite content viewport in logical pixels, with nonnegative dimensions.</param>
    /// <param name="estimatedItemSize">Positive finite cell extents including primary-axis spacing, in logical pixels.</param>
    /// <param name="cacheItemCount">The nonnegative number of extra logical items on each side of the visible range.</param>
    /// <param name="metrics">An optional borrowed single-axis metric map, with the same count, for stack layout.</param>
    public VirtualizationContext(int itemCount, RectangleF viewport, SizeF estimatedItemSize,
        int cacheItemCount = 2, VirtualizingItemMetrics? metrics = null)
    {
        ItemCount = itemCount;
        Viewport = viewport;
        EstimatedItemSize = estimatedItemSize;
        CacheItemCount = cacheItemCount;
        Metrics = metrics;
        Validate();
    }

    /// <summary>Gets the logical source count. No item lookup occurs while reading this property.</summary>
    public int ItemCount { get; }
    /// <summary>Gets the viewport in content coordinates, in logical pixels.</summary>
    public RectangleF Viewport { get; }
    /// <summary>Gets the positive estimated cell size in logical pixels.</summary>
    public SizeF EstimatedItemSize { get; }
    /// <summary>Gets the extra realization buffer on each side, measured in logical items.</summary>
    public int CacheItemCount { get; }
    /// <summary>Gets the borrowed optional stack metric map. Wrap layouts use fixed cells and reject a metric map.</summary>
    public VirtualizingItemMetrics? Metrics { get; }

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(CacheItemCount);
        if (!float.IsFinite(Viewport.X) || !float.IsFinite(Viewport.Y) || !float.IsFinite(Viewport.Width)
            || !float.IsFinite(Viewport.Height) || Viewport.Width < 0 || Viewport.Height < 0
            || !float.IsFinite(Viewport.Right) || !float.IsFinite(Viewport.Bottom))
            throw new ArgumentOutOfRangeException(nameof(Viewport));
        if (!float.IsFinite(EstimatedItemSize.Width) || !float.IsFinite(EstimatedItemSize.Height)
            || EstimatedItemSize.Width <= 0 || EstimatedItemSize.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(EstimatedItemSize));
        if (Metrics is not null && Metrics.Count != ItemCount)
            throw new ArgumentException("The metric map count must match the source count.", nameof(Metrics));
    }

    internal RealizedRange AddCache(int start, int end)
    {
        int bufferedStart = (int)Math.Max(0L, (long)start - CacheItemCount);
        int bufferedEnd = (int)Math.Min(ItemCount, (long)end + CacheItemCount);
        return new(bufferedStart, bufferedEnd - bufferedStart);
    }
}
