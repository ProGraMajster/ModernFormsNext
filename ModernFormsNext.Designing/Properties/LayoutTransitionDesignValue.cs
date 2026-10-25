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
        => Create(enabled, durationMilliseconds, easing, applyToDescendants: false);

    /// <summary>Creates a validated layout-transition value with an explicit descendant policy.</summary>
    /// <param name="enabled">Whether animated layout is enabled.</param>
    /// <param name="durationMilliseconds">Finite non-negative duration in milliseconds.</param>
    /// <param name="easing">Stable built-in easing identifier.</param>
    /// <param name="applyToDescendants">Whether descendants without a local value borrow this configuration.</param>
    /// <returns>The detached structured value. The default false scope is omitted for compatibility.</returns>
    public static DesignPropertyValue Create(bool enabled, double durationMilliseconds, string easing, bool applyToDescendants)
    {
        Validate(durationMilliseconds, easing);
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["DurationMilliseconds"] = DesignPropertyValue.FromDouble(durationMilliseconds),
            ["Easing"] = DesignPropertyValue.FromString(easing),
            ["Enabled"] = DesignPropertyValue.FromBoolean(enabled)
        };
        if (applyToDescendants)
            properties["ApplyToDescendants"] = DesignPropertyValue.FromBoolean(true);
        return DesignPropertyValue.FromStructuredObject(TypeName, properties);
    }

    /// <summary>Reads and validates a detached layout-transition value.</summary>
    public static bool TryRead(
        DesignPropertyValue? value,
        out bool enabled,
        out double durationMilliseconds,
        out string easing,
        out string? error)
        => TryRead(value, out enabled, out durationMilliseconds, out easing, out _, out error);

    /// <summary>Reads a detached layout transition, including its optional descendant policy.</summary>
    /// <param name="value">The structured value, or null for runtime defaults.</param>
    /// <param name="enabled">Receives whether animation is enabled.</param>
    /// <param name="durationMilliseconds">Receives the duration in milliseconds.</param>
    /// <param name="easing">Receives the stable built-in easing identifier.</param>
    /// <param name="applyToDescendants">Receives the scope, defaulting to false for older documents.</param>
    /// <param name="error">Receives a diagnostic when validation fails; otherwise null.</param>
    /// <returns>True when the supplied value uses the supported closed schema.</returns>
    public static bool TryRead(
        DesignPropertyValue? value,
        out bool enabled,
        out double durationMilliseconds,
        out string easing,
        out bool applyToDescendants,
        out string? error)
    {
        enabled = true;
        durationMilliseconds = 250d;
        easing = "EaseOut";
        applyToDescendants = false;
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
            if (name is not ("Enabled" or "DurationMilliseconds" or "Easing" or "ApplyToDescendants"))
            {
                error = $"LayoutTransition property '{name}' is not supported.";
                return false;
            }
        }

        if (value.ObjectProperties.TryGetValue("Enabled", out var enabledValue))
        {
            if (enabledValue is not { Kind: DesignPropertyValueKind.Boolean, Value: bool parsedEnabled })
            {
                error = "LayoutTransition.Enabled must be a Boolean.";
                return false;
            }
            enabled = parsedEnabled;
        }

        if (value.ObjectProperties.TryGetValue("ApplyToDescendants", out var scopeValue))
        {
            if (scopeValue is not { Kind: DesignPropertyValueKind.Boolean, Value: bool parsedScope })
            {
                error = "LayoutTransition.ApplyToDescendants must be a Boolean.";
                return false;
            }
            applyToDescendants = parsedScope;
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
            if (easingValue is not { Kind: DesignPropertyValueKind.String, Value: string parsedEasing }
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

    private static bool TryReadNumber(DesignPropertyValue? value, out double number)
    {
        number = value switch
        {
            { Kind: DesignPropertyValueKind.Int32, Value: int integer } => integer,
            { Kind: DesignPropertyValueKind.Double, Value: double real } => real,
            _ => double.NaN
        };
        return double.IsFinite(number);
    }

    private static bool IsType(string? typeName)
        => string.Equals(typeName, TypeName, StringComparison.Ordinal)
        || string.Equals(typeName, "LayoutTransition", StringComparison.Ordinal);
}
