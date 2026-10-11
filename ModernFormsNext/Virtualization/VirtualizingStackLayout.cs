using System.Drawing;

namespace ModernFormsNext.Virtualization;

/// <summary>Plans a virtualized vertical or horizontal stack with fixed estimates or measured variable extents.</summary>
/// <remarks>Only geometry is stored in the optional metric map. Source values, selection, focus and containers remain owned by the host.</remarks>
public sealed class VirtualizingStackLayout : VirtualizingLayout
{
    /// <summary>Creates a stack strategy with an immutable primary direction.</summary>
    /// <param name="orientation">Vertical stacks advance downwards; horizontal stacks advance rightwards.</param>
    public VirtualizingStackLayout(Orientation orientation = Orientation.Vertical)
    {
        if (orientation is not Orientation.Vertical and not Orientation.Horizontal) throw new ArgumentOutOfRangeException(nameof(orientation));
        Orientation = orientation;
    }
    /// <summary>Gets the primary stack direction.</summary>
    public Orientation Orientation { get; }

    /// <inheritdoc/>
    public override SizeF GetExtent(in VirtualizationContext context)
    {
        context.Validate();
        if (context.ItemCount == 0) return SizeF.Empty;
        float primary = Coordinate(context.Metrics?.TotalExtent ?? context.ItemCount * Estimate(in context));
        float cross = Cross(in context);
        return Orientation == Orientation.Vertical ? new(cross, primary) : new(primary, cross);
    }

    /// <inheritdoc/>
    public override RealizedRange GetRealizedRange(in VirtualizationContext context)
    {
        context.Validate();
        if (context.ItemCount == 0 || context.Viewport.Width == 0 || context.Viewport.Height == 0) return RealizedRange.Empty;
        float crossStart = Orientation == Orientation.Vertical ? context.Viewport.Left : context.Viewport.Top;
        float crossEnd = Orientation == Orientation.Vertical ? context.Viewport.Right : context.Viewport.Bottom;
        if (crossEnd <= 0 || crossStart >= Cross(in context)) return RealizedRange.Empty;
        double start = Orientation == Orientation.Vertical ? context.Viewport.Top : context.Viewport.Left;
        double end = Orientation == Orientation.Vertical ? context.Viewport.Bottom : context.Viewport.Right;
        double extent = context.Metrics?.TotalExtent ?? context.ItemCount * Estimate(in context);
        if (end <= 0 || start >= extent) return RealizedRange.Empty;
        start = Math.Max(0, start);
        end = Math.Min(extent, end);
        int first = Find(in context, start);
        // A half-open viewport ending exactly at a boundary must not realize the next item.
        int exclusiveEnd = Find(in context, Math.BitDecrement(end)) + 1;
        return context.AddCache(first, exclusiveEnd);
    }

    /// <inheritdoc/>
    public override RectangleF GetItemBounds(in VirtualizationContext context, int index)
    {
        ValidateIndex(in context, index);
        float offset = Coordinate(context.Metrics?.GetOffset(index) ?? index * Estimate(in context));
        float length = Coordinate(context.Metrics?.GetExtent(index) ?? Estimate(in context));
        float cross = Cross(in context);
        return Bounds(Orientation == Orientation.Vertical ? new(0, offset, cross, length) : new(offset, 0, length, cross));
    }

    private double Estimate(in VirtualizationContext context)
        => Orientation == Orientation.Vertical ? context.EstimatedItemSize.Height : context.EstimatedItemSize.Width;
    private float Cross(in VirtualizationContext context)
        => Orientation == Orientation.Vertical ? Math.Max(context.EstimatedItemSize.Width, context.Viewport.Width)
            : Math.Max(context.EstimatedItemSize.Height, context.Viewport.Height);
    private int Find(in VirtualizationContext context, double offset)
        => context.Metrics?.FindIndex(offset) ?? (int)Math.Min(context.ItemCount - 1, Math.Floor(offset / Estimate(in context)));
}
