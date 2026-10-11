namespace ModernFormsNext.Virtualization;

/// <summary>Identifies a contiguous, half-open range of logical item occurrences.</summary>
/// <remarks>Indices identify occurrences, not containers or object equality. This immutable value has no thread affinity.</remarks>
public readonly record struct RealizedRange
{
    /// <summary>Creates a range. A zero count represents an empty range at <paramref name="startIndex"/>.</summary>
    /// <param name="startIndex">The nonnegative first logical index.</param>
    /// <param name="count">The nonnegative number of items; the exclusive end must fit in <see cref="int"/>.</param>
    public RealizedRange(int startIndex, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if ((long)startIndex + count > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(count));
        StartIndex = startIndex;
        Count = count;
    }

    /// <summary>Gets the first logical occurrence index.</summary>
    public int StartIndex { get; }
    /// <summary>Gets the number of logical occurrences.</summary>
    public int Count { get; }
    /// <summary>Gets the exclusive end index.</summary>
    public int EndIndex => StartIndex + Count;
    /// <summary>Gets an empty range at index zero.</summary>
    public static RealizedRange Empty => default;
    /// <summary>Returns whether an index belongs to this half-open range.</summary>
    public bool Contains(int index) => index >= StartIndex && index < EndIndex;

    internal RealizedRange Clamp(int count)
    {
        int start = Math.Min(StartIndex, count);
        return new(start, Math.Min(Count, count - start));
    }
}
