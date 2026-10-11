using System.Drawing;
using ModernFormsNext.Layout;

namespace ModernFormsNext.Virtualization;

/// <summary>Connects virtualization geometry to the existing framework layout engine.</summary>
/// <remarks>
/// Layout is requested through Control.PerformLayout and its existing suspension/reentrancy rules.
/// Geometry queries allocate no containers and may be reused by drawn-item controls. A real layout pass requires
/// an IVirtualizingLayoutHost; it does not introduce another layout tree, focus scope, dispatcher or scroll state.
/// Pure queries have no UI affinity when the borrowed metric map is not being mutated concurrently.
/// </remarks>
public abstract class VirtualizingLayout : LayoutEngine
{
    /// <summary>Initializes a geometry strategy without creating controls, dispatchers or native resources.</summary>
    protected VirtualizingLayout() { }

    /// <summary>Gets the full logical content extent, including unrealized items.</summary>
    /// <param name="context">The current validated source/viewport context.</param>
    /// <returns>The extent in logical pixels. Overflow beyond float geometry is reported explicitly.</returns>
    public abstract SizeF GetExtent(in VirtualizationContext context);
    /// <summary>Gets the viewport/cache range without realizing or enumerating source items.</summary>
    /// <param name="context">The current source/viewport context.</param>
    /// <returns>A contiguous half-open range clamped to the source count.</returns>
    public abstract RealizedRange GetRealizedRange(in VirtualizationContext context);
    /// <summary>Gets an item's unscrolled content bounds, including unrealized items.</summary>
    /// <param name="context">The current source/viewport context.</param>
    /// <param name="index">The logical occurrence index, smaller than the source count.</param>
    /// <returns>The cell bounds in logical pixels. The host applies its padding, margins and scroll translation.</returns>
    public abstract RectangleF GetItemBounds(in VirtualizationContext context, int index);

    private protected sealed override bool LayoutCore(IArrangedElement container, LayoutEventArgs layoutEventArgs)
    {
        if (container is not IVirtualizingLayoutHost host)
            throw new NotSupportedException("A virtualizing layout requires an IVirtualizingLayoutHost.");
        var context = host.GetVirtualizationContext();
        context.Validate();
        return host.ArrangeVirtualizedItems(this, in context);
    }

    internal static float Coordinate(double value)
    {
        if (!double.IsFinite(value) || value > float.MaxValue || value < -float.MaxValue)
            throw new OverflowException("Virtualized content exceeds the supported logical geometry range.");
        return (float)value;
    }
    internal static RectangleF Bounds(RectangleF bounds)
    {
        // Individually finite coordinates can still overflow when RectangleF computes its far edges.
        if (!float.IsFinite(bounds.Right) || !float.IsFinite(bounds.Bottom))
            throw new OverflowException("Virtualized item bounds exceed the supported logical geometry range.");
        return bounds;
    }
    internal static void ValidateIndex(in VirtualizationContext context, int index)
    {
        context.Validate();
        if ((uint)index >= (uint)context.ItemCount) throw new ArgumentOutOfRangeException(nameof(index));
    }
}

/// <summary>Adapts an existing Control layout/scroll host to a virtualizing LayoutEngine.</summary>
/// <remarks>
/// Methods run on the owning UI thread during the canonical layout path. Implementations use existing scroll,
/// focus and binding contracts, publish layout bounds through the normal host path, and own their generator.
/// Callbacks may dispose or mutate the host; implementations must revalidate lifetime before continuing.
/// </remarks>
public interface IVirtualizingLayoutHost
{
    /// <summary>Gets a viewport/source snapshot without materializing the logical source.</summary>
    VirtualizationContext GetVirtualizationContext();
    /// <summary>Realizes and arranges the bounded range using the supplied production layout strategy.</summary>
    /// <param name="layout">The borrowed layout engine. Do not dispose or retain host resources in it.</param>
    /// <param name="context">The current logical-pixel source/viewport snapshot.</param>
    /// <returns>True if the existing parent layout must run, following LayoutEngine.Layout semantics.</returns>
    bool ArrangeVirtualizedItems(VirtualizingLayout layout, in VirtualizationContext context);
}
