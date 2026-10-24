namespace ModernFormsNext.Designing;

public sealed partial class DesignDocumentValidator
{
    /// <summary>Gets the maximum number of property values and event entries in one document.</summary>
    /// <remarks>Nested values and repeated references each consume the budget; controls have a separate node budget.</remarks>
    public const int MaximumPropertyValueCount = 10_000;

    /// <summary>Gets the maximum combined number of UTF-16 characters in authored data strings.</summary>
    public const int MaximumTextLength = 4 * 1024 * 1024;

    /// <summary>Gets the maximum UTF-16 length of one authored type, member or identifier name.</summary>
    public const int MaximumTypeNameLength = 16_384;

    /// <summary>Validates one property value without constructing controls or invoking converters.</summary>
    /// <param name="value">The borrowed value; a null entry is invalid, while FromNull represents a valid null literal.</param>
    /// <returns>Errors for malformed values, geometry, cycles or exceeded data budgets.</returns>
    public DesignDocumentValidationResult ValidatePropertyValue(DesignPropertyValue? value)
        => ValidatePropertyValues([value]);

    /// <summary>Validates a bounded sequence of borrowed property values with one shared budget.</summary>
    /// <param name="values">The root values. Nested values are visited iteratively and are never modified.</param>
    /// <param name="maximumValueCount">A positive value budget, at most MaximumPropertyValueCount.</param>
    /// <param name="maximumTextLength">A positive combined UTF-16 text budget, at most MaximumTextLength.</param>
    /// <returns>Errors without evaluating types, constructors, factories, bindings or converters.</returns>
    /// <exception cref="ArgumentNullException">The sequence is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A requested budget is outside the supported range.</exception>
    /// <remarks>Hosts can request smaller preview budgets while retaining the same data rules.</remarks>
    public DesignDocumentValidationResult ValidatePropertyValues(IEnumerable<DesignPropertyValue?> values,
        int maximumValueCount = MaximumPropertyValueCount, int maximumTextLength = MaximumTextLength)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumValueCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumValueCount, MaximumPropertyValueCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumTextLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumTextLength, MaximumTextLength);
        var result = new DesignDocumentValidationResult();
        var context = new ValueValidationContext(result, maximumValueCount, maximumTextLength);
        foreach (var value in values)
        {
            context.ValidateValue(value);
            if (!result.IsValid) break;
        }
        return result;
    }

    // One context spans all root/node properties. A limit is checked before admitting work,
    // including pending children; shared immutable values remain legal, but active cycles do not.
    private sealed class ValueValidationContext(DesignDocumentValidationResult result, int maximumValues, int maximumText)
    {
        private int valueCount;
        private int textLength;

        internal void AddText(string? text, int maximumLength = MaximumTextLength)
        {
            if (!result.IsValid || text is null) return;
            if (text.Length > maximumLength || text.Length > maximumText - textLength)
                result.AddError("Designer data exceeds the supported text or identifier length budget.");
            else textLength += text.Length;
        }

        internal void ValidateProperties(IReadOnlyDictionary<string, DesignPropertyValue> properties)
        {
            if (!result.IsValid) return;
            if (properties.Count > maximumValues - valueCount)
            { result.AddError("Designer data exceeds the property value budget."); return; }
            foreach (var property in properties)
            {
                AddText(property.Key, MaximumTypeNameLength);
                ValidateValue(property.Value);
                if (!result.IsValid) break;
            }
        }

        internal void ValidateEvents(IReadOnlyDictionary<string, string?> events)
        {
            if (!result.IsValid) return;
            if (events.Count > maximumValues - valueCount)
            { result.AddError("Designer data exceeds the property value budget."); return; }
            foreach (var entry in events)
            {
                valueCount++;
                AddText(entry.Key, MaximumTypeNameLength);
                AddText(entry.Value, MaximumTypeNameLength);
                if (!result.IsValid) break;
            }
        }

        internal void ValidateValue(DesignPropertyValue? value)
        {
            if (!result.IsValid) return;
            var pending = new Stack<(DesignPropertyValue? Value, int Depth, bool Exit)>();
            var ancestors = new HashSet<DesignPropertyValue>(ReferenceEqualityComparer.Instance);
            pending.Push((value, 0, false));
            int scheduled = 1;
            while (result.IsValid && pending.TryPop(out var item))
            {
                if (item.Exit) { ancestors.Remove(item.Value!); continue; }
                scheduled--;
                if (++valueCount > maximumValues || item.Depth > MaximumTreeDepth)
                { result.AddError("Designer property data exceeds the supported value count or nesting depth."); break; }
                if (item.Value is not { } current || !ancestors.Add(current))
                { result.AddError("Designer property data contains a null entry or a cycle."); break; }
                bool valid = current.Kind switch
                {
                    DesignPropertyValueKind.Null => current.Value is null,
                    DesignPropertyValueKind.String => current.Value is string,
                    DesignPropertyValueKind.Boolean => current.Value is bool,
                    DesignPropertyValueKind.Int32 => current.Value is int,
                    DesignPropertyValueKind.Double => current.Value is double number && double.IsFinite(number),
                    DesignPropertyValueKind.Enum => current.Value is string,
                    DesignPropertyValueKind.Object => current.Value is null && current.ObjectProperties is not null
                        && !string.IsNullOrWhiteSpace(current.ObjectTypeName),
                    _ => false
                };
                if (!valid) { result.AddError("Designer property data has an invalid primitive kind, CLR value or non-finite number."); break; }
                if (current.Value is string text)
                    AddText(text, current.Kind == DesignPropertyValueKind.Enum ? MaximumTypeNameLength : MaximumTextLength);
                AddText(current.EnumTypeName, MaximumTypeNameLength);
                AddText(current.ObjectTypeName, MaximumTypeNameLength);
                if (!result.IsValid) break;
                pending.Push((current, item.Depth, true));
                if (current.Kind != DesignPropertyValueKind.Object) continue;
                var properties = current.ObjectProperties!;
                if (properties.Count > maximumValues - valueCount - scheduled)
                { result.AddError("Designer property data exceeds the supported value count."); break; }
                if (ValidateStructuredShape(current) is { } error)
                { result.AddError(error); break; }
                scheduled += properties.Count;
                foreach (var child in properties)
                {
                    AddText(child.Key, MaximumTypeNameLength);
                    pending.Push((child.Value, item.Depth + 1, false));
                }
            }
        }
    }
}
