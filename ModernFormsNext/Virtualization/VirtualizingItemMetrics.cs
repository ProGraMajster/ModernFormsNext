namespace ModernFormsNext.Virtualization;

/// <summary>Stores measured single-axis item extents while estimating items that have not been realized.</summary>
/// <remarks>
/// Extents include the item's primary-axis spacing and use logical pixels. All extents must be positive and finite.
/// Storage is proportional to logical count, not live containers. Prefix queries and updates take logarithmic time.
/// Use on the host's owning thread; mutation does not request layout. The host must preserve its viewport anchor
/// and request its existing layout path after measurements or source changes. This type owns no controls/resources.
/// </remarks>
public sealed class VirtualizingItemMetrics
{
    private double[] measured;
    private double[] sums;
    private int leafBase;

    /// <summary>Creates an estimated metric map without realizing any items.</summary>
    /// <param name="itemCount">The nonnegative logical item count.</param>
    /// <param name="estimatedExtent">The positive finite extent used for unmeasured items, in logical pixels.</param>
    public VirtualizingItemMetrics(int itemCount, double estimatedExtent)
    {
        measured = [];
        sums = [];
        Reset(itemCount, estimatedExtent);
    }

    /// <summary>Gets the logical item count.</summary>
    public int Count { get; private set; }
    /// <summary>Gets the estimate for unmeasured items, in logical pixels.</summary>
    public double EstimatedExtent { get; private set; }
    /// <summary>Gets the estimated/measured total extent in logical pixels.</summary>
    public double TotalExtent => sums[1];
    /// <summary>Gets the number of items with retained explicit measurements.</summary>
    public int MeasuredCount { get; private set; }

    /// <summary>Discards measurements and resizes the map after a reset or metric-policy change.</summary>
    /// <remarks>This is O(itemCount) metadata work, not a normal scrolling operation. No controls are created or disposed.</remarks>
    /// <param name="itemCount">The new nonnegative count.</param>
    /// <param name="estimatedExtent">The new positive finite estimate in logical pixels.</param>
    public void Reset(int itemCount, double estimatedExtent)
    {
        Validate(itemCount, estimatedExtent);
        int newLeafBase = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, itemCount));
        if (newLeafBase <= 0 || newLeafBase > int.MaxValue / 2) throw new ArgumentOutOfRangeException(nameof(itemCount));
        if (Count == itemCount && leafBase == newLeafBase) { Array.Clear(measured); Array.Clear(sums); }
        else
        {
            // Allocate both replacements before publishing state; an allocation failure must
            // leave the previous usable map intact.
            var newMeasured = new double[itemCount];
            var newSums = new double[newLeafBase * 2];
            measured = newMeasured;
            sums = newSums;
        }
        leafBase = newLeafBase;
        Array.Fill(sums, estimatedExtent, leafBase, itemCount);
        for (int node = leafBase - 1; node > 0; node--) sums[node] = sums[node * 2] + sums[node * 2 + 1];
        Count = itemCount;
        EstimatedExtent = estimatedExtent;
        MeasuredCount = 0;
    }

    /// <summary>Gets the measured extent or the estimate at an index, in logical pixels.</summary>
    public double GetExtent(int index)
    {
        ValidateIndex(index);
        return measured[index] == 0 ? EstimatedExtent : measured[index];
    }

    /// <summary>Records an extent and returns whether the effective extent changed.</summary>
    /// <remarks>Repeated identical measurements allocate nothing and do not change offsets. The host owns anchor correction/layout.</remarks>
    /// <param name="index">The logical occurrence index.</param>
    /// <param name="extent">The positive finite measured extent in logical pixels.</param>
    public bool SetMeasuredExtent(int index, double extent)
    {
        ValidateIndex(index);
        ValidateExtent(extent);
        double oldExtent = GetExtent(index);
        if (oldExtent != extent)
        {
            SetLeaf(index, extent);
            if (!double.IsFinite(TotalExtent))
            {
                SetLeaf(index, oldExtent);
                throw new ArgumentOutOfRangeException(nameof(extent));
            }
        }
        if (measured[index] == 0) MeasuredCount++;
        measured[index] = extent;
        return oldExtent != extent;
    }

    /// <summary>Gets the start offset of an item or the total extent when <paramref name="index"/> equals Count.</summary>
    /// <param name="index">A boundary index from zero through Count, inclusive.</param>
    /// <returns>The estimated/measured prefix extent in logical pixels.</returns>
    public double GetOffset(int index)
    {
        if ((uint)index > (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        double result = 0;
        int left = leafBase, right = leafBase + index;
        while (left < right)
        {
            if ((left & 1) != 0) result += sums[left++];
            if ((right & 1) != 0) result += sums[--right];
            left /= 2; right /= 2;
        }
        return result;
    }

    /// <summary>Finds the item at an offset, clamping outside the extent; returns -1 for an empty map.</summary>
    /// <param name="offset">A finite content offset in logical pixels.</param>
    /// <returns>The containing item. An exact boundary belongs to the following item.</returns>
    public int FindIndex(double offset)
    {
        if (!double.IsFinite(offset)) throw new ArgumentOutOfRangeException(nameof(offset));
        if (Count == 0) return -1;
        if (offset <= 0) return 0;
        if (offset >= TotalExtent) return Count - 1;
        int node = 1;
        while (node < leafBase)
        {
            int left = node * 2;
            if (offset < sums[left]) node = left;
            else { offset -= sums[left]; node = left + 1; }
        }
        return Math.Min(node - leafBase, Count - 1);
    }

    private void SetLeaf(int index, double extent)
    {
        int node = leafBase + index;
        sums[node] = extent;
        // Recompute from child sums instead of adding estimated deltas. A large estimate
        // corrected to a small measurement must not cancel to zero or lose sibling extents.
        while ((node /= 2) > 0) sums[node] = sums[node * 2] + sums[node * 2 + 1];
    }

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
    }
    private static void Validate(int count, double estimate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(count));
        ValidateExtent(estimate);
        if (!double.IsFinite(count * estimate)) throw new ArgumentOutOfRangeException(nameof(estimate));
    }
    private static void ValidateExtent(double extent)
    {
        if (!double.IsFinite(extent) || extent <= 0) throw new ArgumentOutOfRangeException(nameof(extent));
    }
}
