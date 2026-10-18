namespace ModernFormsNext.Designing;

/// <summary>Encodes a time-only value as a bounded, data-only designer object.</summary>
/// <remarks>
/// The sole member is Ticks, in 100-nanosecond units since midnight. A time has fewer than
/// 2^53 ticks, so the existing JSON numeric model retains every tick exactly. This helper
/// never executes a converter or project constructor and carries no date/time-zone meaning.
/// </remarks>
public static class TimeOnlyDesignValue
{
    /// <summary>Creates the canonical System.TimeOnly object representation.</summary>
    /// <param name="time">The time to encode without rounding.</param>
    /// <returns>A structured object containing its exact tick count.</returns>
    public static DesignPropertyValue FromTime(TimeOnly time)
        => DesignPropertyValue.FromStructuredObject("System.TimeOnly", new Dictionary<string, DesignPropertyValue>
        { ["Ticks"] = DesignPropertyValue.FromDouble(time.Ticks) });

    /// <summary>Reads only the canonical known type and its one finite integral tick member.</summary>
    /// <param name="value">The untrusted data-only representation.</param>
    /// <param name="time">The decoded time, or default when invalid.</param>
    /// <param name="error">A validation error, or null on success.</param>
    /// <returns>Whether type, shape and range are valid. No application code is evaluated.</returns>
    public static bool TryRead(DesignPropertyValue value, out TimeOnly time, out string? error)
    {
        ArgumentNullException.ThrowIfNull(value);
        time = default;
        error = "Expected System.TimeOnly with one integral Ticks value from midnight through the last tick of the day.";
        if (value.Kind != DesignPropertyValueKind.Object || value.ObjectTypeName != "System.TimeOnly" ||
            value.ObjectProperties is not { Count: 1 } properties || !properties.TryGetValue("Ticks", out var stored)) return false;
        double ticks;
        if (stored.Kind == DesignPropertyValueKind.Int32 && stored.Value is int integer) ticks = integer;
        else if (stored.Kind == DesignPropertyValueKind.Double && stored.Value is double number) ticks = number;
        else return false;
        if (!double.IsFinite(ticks) || ticks < 0 || ticks > TimeOnly.MaxValue.Ticks || ticks != Math.Truncate(ticks)) return false;
        time = new TimeOnly((long)ticks);
        error = null;
        return true;
    }
}
