using System.Globalization;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerInteractionEffectEntry
{
    public DesignerInteractionEffectEntry(
        string typeName,
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        DesignAnimationDefinitionDescriptor? descriptor = null)
    {
        TypeName = typeName;
        Descriptor = descriptor;
        Properties = Copy(properties);
    }

    public string TypeName { get; }
    public DesignAnimationDefinitionDescriptor? Descriptor { get; }
    public bool IsSupported => Descriptor is not null;
    public SortedDictionary<string, DesignPropertyValue> Properties { get; private set; }

    public void ReplaceProperties(IReadOnlyDictionary<string, DesignPropertyValue> properties)
        => Properties = Copy(properties);

    public DesignPropertyValue ToDesignValue()
        => DesignPropertyValue.FromStructuredObject(TypeName, Properties);

    public override string ToString()
        => Descriptor?.DisplayName ?? $"{TypeName.Split('.').Last()} (unavailable)";

    private static SortedDictionary<string, DesignPropertyValue> Copy(
        IReadOnlyDictionary<string, DesignPropertyValue> properties)
        => new(
            properties.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
}

internal static class InteractionEffectDesignerRegistry
{
    internal const string RippleTypeName = BuiltInAnimationDefinitionCatalog.RippleEffectTypeName;
    internal const string PressScaleTypeName = BuiltInAnimationDefinitionCatalog.PressScaleEffectTypeName;

    public static IReadOnlyList<string> SupportedTypeNames { get; } =
        BuiltInAnimationDefinitionCatalog.Definitions.Select(item => item.TypeName).ToArray();

    public static DesignerInteractionEffectEntry Create(
        string typeName,
        IEnumerable<DesignAnimationDefinitionDescriptor>? definitions = null)
    {
        DesignAnimationDefinitionDescriptor descriptor = Find(typeName, definitions)
            ?? throw new NotSupportedException($"Interaction effect type '{typeName}' is not supported by the Designer.");
        return new DesignerInteractionEffectEntry(descriptor.TypeName, new Dictionary<string, DesignPropertyValue>(), descriptor);
    }

    public static bool TryReadCollection(
        DesignPropertyValue? value,
        out List<DesignerInteractionEffectEntry> entries,
        out string? error,
        IEnumerable<DesignAnimationDefinitionDescriptor>? definitions = null)
    {
        entries = [];
        if (!InteractionEffectDesignValue.TryRead(value, out IReadOnlyList<DesignPropertyValue> effects, out error))
            return false;

        foreach (DesignPropertyValue effect in effects)
        {
            string typeName = NormalizeTypeName(effect.ObjectTypeName);
            DesignAnimationDefinitionDescriptor? descriptor = Find(typeName, definitions);
            if (descriptor is null)
            {
                // Preserve unavailable project types so a missing/renamed source file never destroys
                // an existing design value. The collection editor still permits remove and reorder.
                entries.Add(new DesignerInteractionEffectEntry(typeName, effect.ObjectProperties!));
                continue;
            }

            if (!TryNormalizeProperties(descriptor, effect.ObjectProperties!, out var properties, out error))
            {
                // A descriptor may have changed after a project refactor. Preserve the exact
                // detached value as unavailable instead of crashing or silently deleting it.
                entries.Add(new DesignerInteractionEffectEntry(typeName, effect.ObjectProperties!));
                error = null;
                continue;
            }
            entries.Add(new DesignerInteractionEffectEntry(descriptor.TypeName, properties, descriptor));
        }
        return true;
    }

    public static DesignPropertyValue WriteCollection(IEnumerable<DesignerInteractionEffectEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return InteractionEffectDesignValue.Create(entries.Select(entry => entry.ToDesignValue()));
    }

    public static string FormatEditorText(DesignerInteractionEffectEntry entry)
    {
        if (entry.Descriptor is null)
            return "This effect type is unavailable. Its serialized definition is preserved.";

        return string.Join(
            Environment.NewLine,
            entry.Descriptor.Properties.Select(definition =>
                $"{definition.Name}={Format(definition, GetEffective(entry, definition))}"));
    }

    public static bool TryApplyEditorText(
        DesignerInteractionEffectEntry entry,
        string text,
        out string? error)
    {
        if (entry.Descriptor is null)
        {
            error = null;
            return true;
        }

        var byName = entry.Descriptor.Properties.ToDictionary(item => item.Name, StringComparer.Ordinal);
        var parsed = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string rawLine in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                error = $"'{line}' must use Property=Value syntax.";
                return false;
            }
            string name = line[..separator].Trim();
            string input = line[(separator + 1)..].Trim();
            if (!byName.TryGetValue(name, out DesignAnimationPropertyDescriptor? definition))
            {
                error = $"Property '{name}' is not supported for {entry}.";
                return false;
            }
            if (!seen.Add(name))
            {
                error = $"Property '{name}' is duplicated.";
                return false;
            }
            if (!TryParse(definition, input, out DesignPropertyValue value, out error))
                return false;
            if (!Equivalent(value, definition.DefaultValue))
                parsed[name] = value;
        }

        // Missing lines mean reset-to-default, which keeps documents and generated code compact.
        entry.ReplaceProperties(parsed);
        error = null;
        return true;
    }

    private static bool TryNormalizeProperties(
        DesignAnimationDefinitionDescriptor descriptor,
        IReadOnlyDictionary<string, DesignPropertyValue> source,
        out IReadOnlyDictionary<string, DesignPropertyValue> normalized,
        out string? error)
    {
        try
        {
            var supported = descriptor.Properties.ToDictionary(item => item.Name, StringComparer.Ordinal);
            var result = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
            foreach ((string name, DesignPropertyValue stored) in source)
            {
                if (!supported.TryGetValue(name, out var definition))
                {
                    normalized = new Dictionary<string, DesignPropertyValue>();
                    error = $"Property '{name}' is not supported for {descriptor.DisplayName}.";
                    return false;
                }
                if (!TryParse(definition, Format(definition, stored), out var value, out error))
                {
                    normalized = new Dictionary<string, DesignPropertyValue>();
                    return false;
                }
                if (!Equivalent(value, definition.DefaultValue))
                    result[name] = value;
            }
            normalized = result;
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is InvalidCastException
            or InvalidOperationException
            or FormatException
            or OverflowException)
        {
            normalized = new Dictionary<string, DesignPropertyValue>();
            error = $"The stored definition for {descriptor.DisplayName} is invalid: {exception.Message}";
            return false;
        }
    }

    private static DesignPropertyValue GetEffective(
        DesignerInteractionEffectEntry entry,
        DesignAnimationPropertyDescriptor definition)
        => entry.Properties.TryGetValue(definition.Name, out var value) ? value : definition.DefaultValue;

    private static DesignAnimationDefinitionDescriptor? Find(
        string typeName,
        IEnumerable<DesignAnimationDefinitionDescriptor>? definitions)
    {
        string normalized = NormalizeTypeName(typeName);
        return BuiltInAnimationDefinitionCatalog.Definitions
            .Concat(definitions ?? [])
            .FirstOrDefault(item => item.Kind == DesignAnimationDefinitionKind.InteractionEffect
                && string.Equals(NormalizeTypeName(item.TypeName), normalized, StringComparison.Ordinal));
    }

    private static string NormalizeTypeName(string? typeName)
        => typeName switch
        {
            "RippleEffect" => RippleTypeName,
            "PressScaleEffect" => PressScaleTypeName,
            _ => (typeName ?? string.Empty).Replace("global::", string.Empty, StringComparison.Ordinal).Trim()
        };

    private static string Format(DesignAnimationPropertyDescriptor definition, DesignPropertyValue value)
        => definition.Kind switch
        {
            DesignAnimationPropertyKind.Boolean => value.Value is bool boolValue && boolValue ? "true" : "false",
            DesignAnimationPropertyKind.Int32 => Convert.ToInt32(value.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            DesignAnimationPropertyKind.Number or DesignAnimationPropertyKind.TimeSpan => Convert.ToDouble(value.Value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture),
            DesignAnimationPropertyKind.Easing or DesignAnimationPropertyKind.String or DesignAnimationPropertyKind.Enum => value.GetString(),
            DesignAnimationPropertyKind.ColorArgb => $"#{unchecked((uint)Convert.ToInt32(value.Value, CultureInfo.InvariantCulture)):X8}",
            _ => throw new ArgumentOutOfRangeException()
        };

    private static bool TryParse(
        DesignAnimationPropertyDescriptor definition,
        string text,
        out DesignPropertyValue value,
        out string? error)
    {
        switch (definition.Kind)
        {
            case DesignAnimationPropertyKind.Boolean when bool.TryParse(text, out bool boolValue):
                value = DesignPropertyValue.FromBoolean(boolValue);
                error = null;
                return true;
            case DesignAnimationPropertyKind.Int32 when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue)
                && intValue >= definition.Minimum && intValue <= definition.Maximum:
                value = DesignPropertyValue.FromInt32(intValue);
                error = null;
                return true;
            case DesignAnimationPropertyKind.Number or DesignAnimationPropertyKind.TimeSpan
                when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                && double.IsFinite(number) && number >= definition.Minimum && number <= definition.Maximum:
                value = DesignPropertyValue.FromDouble(number);
                error = null;
                return true;
            case DesignAnimationPropertyKind.Easing when KnownEasingDesignValue.IsKnown(text):
                value = DesignPropertyValue.FromString(text);
                error = null;
                return true;
            case DesignAnimationPropertyKind.String:
                value = DesignPropertyValue.FromString(text);
                error = null;
                return true;
            case DesignAnimationPropertyKind.Enum when definition.EnumMembers.Contains(text, StringComparer.Ordinal):
                value = DesignPropertyValue.FromEnum(definition.EnumTypeName!, text);
                error = null;
                return true;
            case DesignAnimationPropertyKind.ColorArgb when text.Length == 9 && text[0] == '#'
                && uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb):
                value = DesignPropertyValue.FromInt32(unchecked((int)argb));
                error = null;
                return true;
        }

        value = DesignPropertyValue.FromNull();
        error = $"Value '{text}' is not valid for {definition.Name}.";
        return false;
    }

    private static bool Equivalent(DesignPropertyValue left, DesignPropertyValue right)
        => left.Kind == right.Kind
        && Equals(left.Value, right.Value)
        && string.Equals(left.EnumTypeName, right.EnumTypeName, StringComparison.Ordinal);
}
