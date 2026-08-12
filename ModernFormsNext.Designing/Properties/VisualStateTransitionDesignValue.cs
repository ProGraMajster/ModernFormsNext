namespace ModernFormsNext.Designing;

/// <summary>Represents one detached visual-state transition entry.</summary>
/// <param name="From">Source visual-state member name.</param>
/// <param name="To">Target visual-state member name.</param>
/// <param name="DurationMilliseconds">Transition duration in milliseconds.</param>
/// <param name="Easing">Stable built-in easing identifier.</param>
public sealed record DesignVisualStateTransition(
    string From,
    string To,
    double DurationMilliseconds,
    string Easing);

/// <summary>Encodes the ordered visual-state transition collection in one designer value.</summary>
public static class VisualStateTransitionDesignValue
{
    /// <summary>Gets the design-document property name mapped to <c>Control.StyleTransitions</c>.</summary>
    public const string PropertyName = "StyleTransitions";

    /// <summary>Gets the structured collection type name.</summary>
    public const string CollectionTypeName = "ModernFormsNext.Animations.VisualStateTransitionCollection";

    private const string EntryTypeName = "ModernFormsNext.Animations.VisualStateTransition";

    /// <summary>Creates a deterministic ordered collection value.</summary>
    public static DesignPropertyValue Create(IEnumerable<DesignVisualStateTransition> transitions)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        DesignVisualStateTransition[] items = transitions.ToArray();
        if (items.Length > 64)
            throw new ArgumentOutOfRangeException(nameof(transitions), "At most 64 transitions can be stored.");

        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["Count"] = DesignPropertyValue.FromInt32(items.Length)
        };
        var pairs = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < items.Length; index++)
        {
            DesignVisualStateTransition item = items[index];
            Validate(item);
            if (!pairs.Add(item.From + "\0" + item.To))
                throw new ArgumentException($"The {item.From} -> {item.To} transition is duplicated.", nameof(transitions));

            properties[$"Item{index}"] = DesignPropertyValue.FromStructuredObject(
                EntryTypeName,
                new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
                {
                    ["DurationMilliseconds"] = DesignPropertyValue.FromDouble(item.DurationMilliseconds),
                    ["Easing"] = DesignPropertyValue.FromString(item.Easing),
                    ["From"] = DesignPropertyValue.FromEnum("ModernFormsNext.Animations.VisualState", item.From),
                    ["To"] = DesignPropertyValue.FromEnum("ModernFormsNext.Animations.VisualState", item.To)
                });
        }

        return DesignPropertyValue.FromStructuredObject(CollectionTypeName, properties);
    }

    /// <summary>Reads and validates an ordered transition collection.</summary>
    public static bool TryRead(
        DesignPropertyValue? value,
        out IReadOnlyList<DesignVisualStateTransition> transitions,
        out string? error)
    {
        transitions = [];
        error = null;
        if (value is null)
            return true;
        if (value.Kind != DesignPropertyValueKind.Object
            || value.ObjectProperties is null
            || !IsCollectionType(value.ObjectTypeName))
        {
            error = "StyleTransitions must be a structured visual-state transition collection.";
            return false;
        }
        if (!value.ObjectProperties.TryGetValue("Count", out var countValue)
            || countValue.Kind != DesignPropertyValueKind.Int32
            || countValue.Value is not int count
            || count is < 0 or > 64)
        {
            error = "StyleTransitions.Count must be an integer from 0 through 64.";
            return false;
        }
        if (value.ObjectProperties.Count != count + 1
            || value.ObjectProperties.Keys.Any(name => name != "Count"
                && !Enumerable.Range(0, count).Any(index => name == $"Item{index}")))
        {
            error = "StyleTransitions contains unsupported collection properties.";
            return false;
        }

        var result = new List<DesignVisualStateTransition>(count);
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < count; index++)
        {
            if (!value.ObjectProperties.TryGetValue($"Item{index}", out var item)
                || item.Kind != DesignPropertyValueKind.Object
                || item.ObjectProperties is null
                || !string.Equals(item.ObjectTypeName, EntryTypeName, StringComparison.Ordinal)
                || item.ObjectProperties.Keys.Any(name => name is not ("From" or "To" or "DurationMilliseconds" or "Easing")))
            {
                error = $"StyleTransitions.Item{index} is missing or malformed.";
                return false;
            }

            if (!TryReadState(item.ObjectProperties, "From", out string from)
                || !TryReadState(item.ObjectProperties, "To", out string to)
                || !TryReadDuration(item.ObjectProperties, out double duration)
                || !TryReadEasing(item.ObjectProperties, out string easing))
            {
                error = $"StyleTransitions.Item{index} contains an invalid state, duration, or easing.";
                return false;
            }
            if (!pairs.Add(from + "\0" + to))
            {
                error = $"StyleTransitions contains duplicate {from} -> {to} entries.";
                return false;
            }
            result.Add(new DesignVisualStateTransition(from, to, duration, easing));
        }

        transitions = result;
        return true;
    }

    private static readonly string[] States = ["Normal", "Hover", "Pressed", "Disabled", "Focused"];

    private static void Validate(DesignVisualStateTransition item)
    {
        if (!States.Contains(item.From, StringComparer.Ordinal) || !States.Contains(item.To, StringComparer.Ordinal))
            throw new ArgumentException("Visual-state transition endpoints must be known VisualState members.");
        if (!double.IsFinite(item.DurationMilliseconds) || item.DurationMilliseconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(item), "Transition duration must be non-negative and finite.");
        if (!KnownEasingDesignValue.IsKnown(item.Easing))
            throw new ArgumentException("Transition easing must be a known built-in easing identifier.");
    }

    private static bool TryReadState(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name, out string value)
    {
        value = string.Empty;
        if (!properties.TryGetValue(name, out var stored)
            || stored.Kind != DesignPropertyValueKind.Enum
            || stored.Value is not string member
            || !States.Contains(member, StringComparer.Ordinal))
            return false;
        value = member;
        return true;
    }

    private static bool TryReadDuration(IReadOnlyDictionary<string, DesignPropertyValue> properties, out double value)
    {
        value = 150d;
        if (!properties.TryGetValue("DurationMilliseconds", out var stored))
            return true;
        if (stored.Kind is not (DesignPropertyValueKind.Int32 or DesignPropertyValueKind.Double))
            return false;
        value = Convert.ToDouble(stored.Value, System.Globalization.CultureInfo.InvariantCulture);
        return double.IsFinite(value) && value >= 0d;
    }

    private static bool TryReadEasing(IReadOnlyDictionary<string, DesignPropertyValue> properties, out string value)
    {
        value = "CubicOut";
        if (!properties.TryGetValue("Easing", out var stored))
            return true;
        if (stored.Kind != DesignPropertyValueKind.String || stored.Value is not string identifier)
            return false;
        value = identifier;
        return KnownEasingDesignValue.IsKnown(value);
    }

    private static bool IsCollectionType(string? typeName)
        => string.Equals(typeName, CollectionTypeName, StringComparison.Ordinal)
        || string.Equals(typeName, "VisualStateTransitionCollection", StringComparison.Ordinal);
}
