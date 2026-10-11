using System.Drawing;

namespace ModernFormsNext.Virtualization;

/// <summary>Plans fixed-size virtualized cells in rows or columns fitting the viewport's cross axis.</summary>
/// <remarks>
/// Cell estimates include spacing; the host arranges actual controls within these cells. This initial wrap strategy
/// rejects variable metric maps explicitly. It realizes complete visible rows/columns plus the configured item buffer.
/// No item values or controls are enumerated to plan the range. Variable stacks use VirtualizingStackLayout.
/// </remarks>
public sealed class VirtualizingWrapLayout : VirtualizingLayout
{
    /// <summary>Creates a fixed-cell wrap strategy.</summary>
    /// <param name="orientation">Vertical wraps across rows; horizontal wraps down columns.</param>
    public VirtualizingWrapLayout(Orientation orientation = Orientation.Vertical)
    {
        if (orientation is not Orientation.Vertical and not Orientation.Horizontal) throw new ArgumentOutOfRangeException(nameof(orientation));
        Orientation = orientation;
    }
    /// <summary>Gets the direction in which wrapped groups advance.</summary>
    public Orientation Orientation { get; }

    /// <inheritdoc/>
    public override SizeF GetExtent(in VirtualizationContext context)
    {
        Validate(in context);
        if (context.ItemCount == 0) return SizeF.Empty;
        int across = Across(in context);
        long groups = ((long)context.ItemCount + across - 1) / across;
        float primary = Coordinate(groups * Primary(in context));
        float cross = Coordinate(Math.Min(across, context.ItemCount) * Cross(in context));
        return Orientation == Orientation.Vertical ? new(cross, primary) : new(primary, cross);
    }

    /// <inheritdoc/>
    public override RealizedRange GetRealizedRange(in VirtualizationContext context)
    {
        Validate(in context);
        if (context.ItemCount == 0 || context.Viewport.Width == 0 || context.Viewport.Height == 0) return RealizedRange.Empty;
        int across = Across(in context);
        double crossStart = Orientation == Orientation.Vertical ? context.Viewport.Left : context.Viewport.Top;
        double crossEnd = Orientation == Orientation.Vertical ? context.Viewport.Right : context.Viewport.Bottom;
        if (crossEnd <= 0 || crossStart >= Math.Min(across, context.ItemCount) * Cross(in context)) return RealizedRange.Empty;
        double start = Orientation == Orientation.Vertical ? context.Viewport.Top : context.Viewport.Left;
        double end = Orientation == Orientation.Vertical ? context.Viewport.Bottom : context.Viewport.Right;
        double groups = ((long)context.ItemCount + across - 1) / across;
        double extent = groups * Primary(in context);
        if (end <= 0 || start >= extent) return RealizedRange.Empty;
        int first = (int)Math.Min(context.ItemCount, Math.Floor(Math.Max(0, start) / Primary(in context)) * across);
        int last = (int)Math.Min(context.ItemCount, (Math.Floor(Math.BitDecrement(Math.Min(extent, end)) / Primary(in context)) + 1) * across);
        return context.AddCache(first, last);
    }

    /// <inheritdoc/>
    public override RectangleF GetItemBounds(in VirtualizationContext context, int index)
    {
        Validate(in context);
        ValidateIndex(in context, index);
        int across = Across(in context);
        float primary = Coordinate((index / across) * Primary(in context));
        float cross = Coordinate((index % across) * Cross(in context));
        var cell = context.EstimatedItemSize;
        return Bounds(Orientation == Orientation.Vertical ? new(cross, primary, cell.Width, cell.Height) : new(primary, cross, cell.Width, cell.Height));
    }

    private int Across(in VirtualizationContext context)
    {
        double length = Orientation == Orientation.Vertical ? context.Viewport.Width : context.Viewport.Height;
        return (int)Math.Clamp(Math.Floor(length / Cross(in context)), 1, Math.Max(1, context.ItemCount));
    }
    private double Primary(in VirtualizationContext context)
        => Orientation == Orientation.Vertical ? context.EstimatedItemSize.Height : context.EstimatedItemSize.Width;
    private double Cross(in VirtualizationContext context)
        => Orientation == Orientation.Vertical ? context.EstimatedItemSize.Width : context.EstimatedItemSize.Height;
    private static void Validate(in VirtualizationContext context)
    {
        context.Validate();
        if (context.Metrics is not null) throw new NotSupportedException("Fixed-cell wrap layout does not accept variable item metrics.");
    }
}
