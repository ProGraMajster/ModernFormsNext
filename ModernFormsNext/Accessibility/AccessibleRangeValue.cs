namespace ModernFormsNext.Accessibility;

/// <summary>
/// Describes a platform-neutral numeric range exposed by an <see cref="AccessibleObject"/>.
/// </summary>
/// <remarks>
/// Values use logical control units rather than pixels. A read-only range can report progress but
/// must not advertise <see cref="AccessibleActions.SetValue"/>.
/// </remarks>
public readonly struct AccessibleRangeValue
{
    /// <summary>
    /// Initializes range metadata.
    /// </summary>
    /// <param name="value">The current value, between <paramref name="minimum"/> and <paramref name="maximum"/>.</param>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    /// <param name="smallChange">The amount used for a small increment or decrement.</param>
    /// <param name="largeChange">The amount used for a page-sized increment or decrement.</param>
    /// <param name="isReadOnly">Whether callers may change the value through semantic actions.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A value is not finite, the range is inverted, the current value is outside the range, or a
    /// change amount is negative.
    /// </exception>
    public AccessibleRangeValue(
        double value,
        double minimum,
        double maximum,
        double smallChange,
        double largeChange,
        bool isReadOnly)
    {
        if (!double.IsFinite(minimum))
            throw new ArgumentOutOfRangeException(nameof(minimum));
        if (!double.IsFinite(maximum) || maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (!double.IsFinite(smallChange) || smallChange < 0)
            throw new ArgumentOutOfRangeException(nameof(smallChange));
        if (!double.IsFinite(largeChange) || largeChange < 0)
            throw new ArgumentOutOfRangeException(nameof(largeChange));

        Value = value;
        Minimum = minimum;
        Maximum = maximum;
        SmallChange = smallChange;
        LargeChange = largeChange;
        IsReadOnly = isReadOnly;
    }

    /// <summary>
    /// Gets the current numeric value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Gets the inclusive minimum value.
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    /// Gets the inclusive maximum value.
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    /// Gets the amount used for a small increment or decrement.
    /// </summary>
    public double SmallChange { get; }

    /// <summary>
    /// Gets the amount used for a page-sized increment or decrement.
    /// </summary>
    public double LargeChange { get; }

    /// <summary>
    /// Gets whether semantic consumers must treat the range as read-only.
    /// </summary>
    public bool IsReadOnly { get; }
}
