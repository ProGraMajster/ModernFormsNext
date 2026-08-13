namespace ModernFormsNext.Designing;

/// <summary>Encodes a detached <c>LayoutTransition</c> configuration in a design document.</summary>
public static class LayoutTransitionDesignValue
{
    /// <summary>Gets the design-document property name.</summary>
    public const string PropertyName = "LayoutTransition";

    /// <summary>Gets the structured runtime type name.</summary>
    public const string TypeName = "ModernFormsNext.Animations.LayoutTransition";

    /// <summary>Creates a validated layout-transition value.</summary>
    /// <param name="enabled">Whether animated layout is enabled.</param>
    /// <param name="durationMilliseconds">Duration in milliseconds.</param>
    /// <param name="easing">Stable built-in easing identifier.</param>
    /// <returns>The detached structured value.</returns>
    public static DesignPropertyValue Create(bool enabled, double durationMilliseconds, string easing)
    {
        Validate(durationMilliseconds, easing);
        return DesignPropertyValue.FromStructuredObject(
            TypeName,
            new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["DurationMilliseconds"] = DesignPropertyValue.FromDouble(durationMilliseconds),
                ["Easing"] = DesignPropertyValue.FromString(easing),
                ["Enabled"] = DesignPropertyValue.FromBoolean(enabled)
            });
    }

    /// <summary>Reads and validates a detached layout-transition value.</summary>
    public static bool TryRead(
        DesignPropertyValue? value,
        out bool enabled,
        out double durationMilliseconds,
        out string easing,
        out string? error)
    {
        enabled = true;
        durationMilliseconds = 250d;
        easing = "EaseOut";
        error = null;

        if (value is null)
            return true;
        if (value.Kind != DesignPropertyValueKind.Object
            || value.ObjectProperties is null
            || !IsType(value.ObjectTypeName))
        {
            error = "LayoutTransition must be a structured LayoutTransition value.";
            return false;
        }

        foreach (string name in value.ObjectProperties.Keys)
        {
            if (name is not ("Enabled" or "DurationMilliseconds" or "Easing"))
            {
                error = $"LayoutTransition property '{name}' is not supported.";
                return false;
            }
        }

        if (value.ObjectProperties.TryGetValue("Enabled", out var enabledValue))
        {
            if (enabledValue.Kind != DesignPropertyValueKind.Boolean || enabledValue.Value is not bool parsedEnabled)
            {
                error = "LayoutTransition.Enabled must be a Boolean.";
                return false;
            }
            enabled = parsedEnabled;
        }

        if (value.ObjectProperties.TryGetValue("DurationMilliseconds", out var durationValue))
        {
            if (!TryReadNumber(durationValue, out durationMilliseconds) || durationMilliseconds < 0d)
            {
                error = "LayoutTransition.DurationMilliseconds must be a finite non-negative number.";
                return false;
            }
        }

        if (value.ObjectProperties.TryGetValue("Easing", out var easingValue))
        {
            if (easingValue.Kind != DesignPropertyValueKind.String
                || easingValue.Value is not string parsedEasing
                || !KnownEasingDesignValue.IsKnown(parsedEasing))
            {
                error = "LayoutTransition.Easing must be a known built-in easing identifier.";
                return false;
            }
            easing = parsedEasing;
        }

        return true;
    }

    private static void Validate(double durationMilliseconds, string easing)
    {
        if (!double.IsFinite(durationMilliseconds) || durationMilliseconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
        if (!KnownEasingDesignValue.IsKnown(easing))
            throw new ArgumentException("The easing identifier is not supported by the Designer.", nameof(easing));
    }

    private static bool TryReadNumber(DesignPropertyValue value, out double number)
    {
        number = value.Kind switch
        {
            DesignPropertyValueKind.Int32 => Convert.ToDouble(value.Value, System.Globalization.CultureInfo.InvariantCulture),
            DesignPropertyValueKind.Double => Convert.ToDouble(value.Value, System.Globalization.CultureInfo.InvariantCulture),
            _ => double.NaN
        };
        return double.IsFinite(number);
    }

    private static bool IsType(string? typeName)
        => string.Equals(typeName, TypeName, StringComparison.Ordinal)
        || string.Equals(typeName, "LayoutTransition", StringComparison.Ordinal);
}
