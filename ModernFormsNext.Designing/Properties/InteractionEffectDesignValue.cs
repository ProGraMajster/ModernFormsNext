namespace ModernFormsNext.Designing;

/// <summary>
/// Encodes an ordered interaction-effect collection inside one deterministic designer value.
/// </summary>
/// <remarks>
/// The value uses a structured object with a <c>Count</c> member and zero-based <c>ItemN</c>
/// members. Each item is another structured value whose type name identifies the effect and whose
/// properties contain only designer-safe primitive values. This representation does not construct
/// runtime effects or start animations.
/// </remarks>
public static class InteractionEffectDesignValue
{
    /// <summary>Gets the design-document property name used for the collection.</summary>
    public const string PropertyName = "InteractionEffects";

    /// <summary>Gets the structured type discriminator used for the collection value.</summary>
    public const string CollectionTypeName = "ModernFormsNext.Animations.InteractionEffectCollection";

    /// <summary>Creates an ordered collection value from detached effect descriptions.</summary>
    /// <param name="effects">Structured effect descriptions in runtime attachment order.</param>
    /// <returns>A deterministic collection value.</returns>
    /// <exception cref="ArgumentException">An item is not a structured value with a type name.</exception>
    public static DesignPropertyValue Create(IEnumerable<DesignPropertyValue> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        DesignPropertyValue[] items = effects.ToArray();
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["Count"] = DesignPropertyValue.FromInt32(items.Length)
        };

        for (int index = 0; index < items.Length; index++)
        {
            DesignPropertyValue item = items[index]
                ?? throw new ArgumentException("Interaction effect descriptions cannot contain null.", nameof(effects));
            if (item.Kind != DesignPropertyValueKind.Object
                || string.IsNullOrWhiteSpace(item.ObjectTypeName))
            {
                throw new ArgumentException(
                    "Every interaction effect description must be a structured value with a type name.",
                    nameof(effects));
            }
            properties[$"Item{index}"] = item;
        }

        return DesignPropertyValue.FromStructuredObject(CollectionTypeName, properties);
    }

    /// <summary>Reads ordered detached effect descriptions from a collection value.</summary>
    /// <param name="value">The collection value to read.</param>
    /// <param name="effects">The ordered effect descriptions when successful.</param>
    /// <param name="error">A validation error when the value is malformed.</param>
    /// <returns><see langword="true"/> when the collection is valid; otherwise <see langword="false"/>.</returns>
    public static bool TryRead(
        DesignPropertyValue? value,
        out IReadOnlyList<DesignPropertyValue> effects,
        out string? error)
    {
        effects = [];
        error = null;

        if (value is null)
            return true;
        if (value.Kind != DesignPropertyValueKind.Object
            || !IsCollectionType(value.ObjectTypeName)
            || value.ObjectProperties is null)
        {
            error = "InteractionEffects must be a structured interaction-effect collection value.";
            return false;
        }
        if (!value.ObjectProperties.TryGetValue("Count", out DesignPropertyValue? countValue)
            || countValue.Kind != DesignPropertyValueKind.Int32
            || countValue.Value is not int count
            || count is < 0 or > 64)
        {
            error = "InteractionEffects.Count must be an integer from 0 through 64.";
            return false;
        }
        if (value.ObjectProperties.Count != count + 1
            || value.ObjectProperties.Keys.Any(name => name != "Count"
                && !Enumerable.Range(0, count).Any(index => name == $"Item{index}")))
        {
            error = "InteractionEffects contains unsupported collection properties.";
            return false;
        }

        var result = new List<DesignPropertyValue>(count);
        for (int index = 0; index < count; index++)
        {
            if (!value.ObjectProperties.TryGetValue($"Item{index}", out DesignPropertyValue? item)
                || item.Kind != DesignPropertyValueKind.Object
                || string.IsNullOrWhiteSpace(item.ObjectTypeName)
                || item.ObjectProperties is null)
            {
                error = $"InteractionEffects.Item{index} is missing or malformed.";
                return false;
            }
            result.Add(item);
        }

        effects = result;
        return true;
    }

    private static bool IsCollectionType(string? typeName)
        => string.Equals(typeName, CollectionTypeName, StringComparison.Ordinal)
        || string.Equals(typeName, "InteractionEffectCollection", StringComparison.Ordinal);
}
