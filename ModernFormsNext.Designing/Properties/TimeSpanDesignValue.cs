using System.Globalization;

namespace ModernFormsNext.Designing;

/// <summary>Encodes an exact TimeSpan as a bounded data-only designer value.</summary>
/// <remarks>
/// The sole Ticks member is a canonical invariant Int64 string in 100-nanosecond units.
/// A string preserves the entire signed range without rounding through JSON double values.
/// This helper never invokes a converter, project constructor or executable expression.
/// </remarks>
public static class TimeSpanDesignValue
{
    /// <summary>Copies a duration into its exact, culture-independent representation.</summary>
    /// <param name="duration">The signed duration, including either TimeSpan boundary.</param>
    /// <returns>A System.TimeSpan object with exactly one canonical Ticks string.</returns>
    public static DesignPropertyValue FromDuration(TimeSpan duration)
        => DesignPropertyValue.FromStructuredObject("System.TimeSpan", new Dictionary<string, DesignPropertyValue>
        { ["Ticks"] = DesignPropertyValue.FromString(duration.Ticks.ToString(CultureInfo.InvariantCulture)) });

    /// <summary>Reads only the exact known type, member shape and canonical signed tick string.</summary>
    /// <param name="value">The untrusted data-only value.</param>
    /// <param name="duration">The decoded duration, or zero on failure.</param>
    /// <param name="error">A validation message, or null on success.</param>
    /// <returns>Whether the value represents an exact TimeSpan without evaluation or numeric rounding.</returns>
    public static bool TryRead(DesignPropertyValue value, out TimeSpan duration, out string? error)
    {
        ArgumentNullException.ThrowIfNull(value);
        duration = default;
        error = "Expected System.TimeSpan with one canonical invariant Int64 Ticks string.";
        if (value.Kind != DesignPropertyValueKind.Object || value.ObjectTypeName != "System.TimeSpan" ||
            value.ObjectProperties is not { Count: 1 } properties || !properties.TryGetValue("Ticks", out var stored) ||
            stored is null || stored.Kind != DesignPropertyValueKind.String || stored.Value is not string text ||
            text.Length is < 1 or > 20 || !long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long ticks) ||
            ticks.ToString(CultureInfo.InvariantCulture) != text) return false;
        duration = TimeSpan.FromTicks(ticks);
        error = null;
        return true;
    }
}
