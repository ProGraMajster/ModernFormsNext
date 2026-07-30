using System.Globalization;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerInteractionEffectEntry
{
    public DesignerInteractionEffectEntry(
        string typeName,
        IReadOnlyDictionary<string, DesignPropertyValue> properties)
    {
        TypeName = typeName;
        Properties = new SortedDictionary<string, DesignPropertyValue>(
            properties.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    public string TypeName { get; }

    public SortedDictionary<string, DesignPropertyValue> Properties { get; private set; }

    public void ReplaceProperties(IReadOnlyDictionary<string, DesignPropertyValue> properties)
        => Properties = new SortedDictionary<string, DesignPropertyValue>(
            properties.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);

    public DesignPropertyValue ToDesignValue()
        => DesignPropertyValue.FromStructuredObject(TypeName, Properties);

    public override string ToString()
        => TypeName.Split('.').Last();
}

internal static class InteractionEffectDesignerRegistry
{
    internal const string RippleTypeName = "ModernFormsNext.Animations.RippleEffect";
    internal const string PressScaleTypeName = "ModernFormsNext.Animations.PressScaleEffect";

    private static readonly EffectPropertyDefinition[] RippleProperties =
    [
        EffectPropertyDefinition.Boolean("Enabled", true),
        EffectPropertyDefinition.ColorArgb("ColorArgb", unchecked((int)0x5AFFFFFF)),
        EffectPropertyDefinition.Number("DurationMilliseconds", 450d, 0d, double.MaxValue),
        EffectPropertyDefinition.Boolean("StartFromPointer", true),
        EffectPropertyDefinition.Enum(
            "RadiusMode",
            "ModernFormsNext.Animations.RippleRadiusMode",
            "CoverControl",
            "CoverControl",
            "Fixed"),
        EffectPropertyDefinition.Number("FixedRadius", 48d, 0d, float.MaxValue),
        EffectPropertyDefinition.Enum(
            "Layer",
            "ModernFormsNext.Animations.RippleLayer",
            "AboveBackgroundBelowContent",
            "AboveBackgroundBelowContent",
            "AboveContent"),
        EffectPropertyDefinition.Integer("MaxConcurrentRipples", 4, 1, 32),
        EffectPropertyDefinition.Enum(
            "OverflowPolicy",
            "ModernFormsNext.Animations.RippleOverflowPolicy",
            "RemoveOldest",
            "RemoveOldest",
            "RemoveNewest",
            "IgnoreNew",
            "ReplaceAll")
    ];

    private static readonly EffectPropertyDefinition[] PressScaleProperties =
    [
        EffectPropertyDefinition.Boolean("Enabled", true),
        EffectPropertyDefinition.Number("PressedScale", 0.97d, double.Epsilon, float.MaxValue),
        EffectPropertyDefinition.Number("PressDurationMilliseconds", 80d, 0d, double.MaxValue),
        EffectPropertyDefinition.Number("ReleaseDurationMilliseconds", 120d, 0d, double.MaxValue)
    ];

    public static IReadOnlyList<string> SupportedTypeNames { get; } =
        [RippleTypeName, PressScaleTypeName];

    public static DesignerInteractionEffectEntry Create(string typeName)
    {
        EffectPropertyDefinition[] definitions = GetDefinitions(typeName)
            ?? throw new NotSupportedException($"Interaction effect type '{typeName}' is not supported by the Designer.");
        return new DesignerInteractionEffectEntry(
            typeName,
            definitions.ToDictionary(item => item.Name, item => item.DefaultValue, StringComparer.Ordinal));
    }

    public static bool TryReadCollection(
        DesignPropertyValue? value,
        out List<DesignerInteractionEffectEntry> entries,
        out string? error)
    {
        entries = [];
        if (!InteractionEffectDesignValue.TryRead(value, out IReadOnlyList<DesignPropertyValue> effects, out error))
            return false;

        foreach (DesignPropertyValue effect in effects)
        {
            string typeName = NormalizeTypeName(effect.ObjectTypeName);
            EffectPropertyDefinition[]? definitions = GetDefinitions(typeName);
            if (definitions is null)
            {
                error = $"Interaction effect type '{effect.ObjectTypeName}' is not supported by the Designer.";
                entries.Clear();
                return false;
            }
            if (!TryNormalizeProperties(definitions, effect.ObjectProperties!, out var properties, out error))
            {
                entries.Clear();
                return false;
            }
            entries.Add(new DesignerInteractionEffectEntry(typeName, properties));
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
        EffectPropertyDefinition[] definitions = GetDefinitions(entry.TypeName)!;
        return string.Join(
            Environment.NewLine,
            definitions.Select(definition =>
                $"{definition.Name}={definition.Format(entry.Properties[definition.Name])}"));
    }

    public static bool TryApplyEditorText(
        DesignerInteractionEffectEntry entry,
        string text,
        out string? error)
    {
        EffectPropertyDefinition[] definitions = GetDefinitions(entry.TypeName)!;
        var byName = definitions.ToDictionary(item => item.Name, StringComparer.Ordinal);
        var parsed = definitions.ToDictionary(item => item.Name, item => item.DefaultValue, StringComparer.Ordinal);
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
            string value = line[(separator + 1)..].Trim();
            if (!byName.TryGetValue(name, out EffectPropertyDefinition? definition))
            {
                error = $"Property '{name}' is not supported for {entry}.";
                return false;
            }
            if (!seen.Add(name))
            {
                error = $"Property '{name}' is duplicated.";
                return false;
            }
            if (!definition.TryParse(value, out DesignPropertyValue parsedValue, out error))
                return false;
            parsed[name] = parsedValue;
        }

        entry.ReplaceProperties(parsed);
        error = null;
        return true;
    }

    private static bool TryNormalizeProperties(
        EffectPropertyDefinition[] definitions,
        IReadOnlyDictionary<string, DesignPropertyValue> source,
        out IReadOnlyDictionary<string, DesignPropertyValue> normalized,
        out string? error)
    {
        var supported = definitions.ToDictionary(item => item.Name, StringComparer.Ordinal);
        foreach (string name in source.Keys)
        {
            if (!supported.ContainsKey(name))
            {
                normalized = new Dictionary<string, DesignPropertyValue>();
                error = $"Property '{name}' is not supported for this interaction effect.";
                return false;
            }
        }

        var result = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        foreach (EffectPropertyDefinition definition in definitions)
        {
            DesignPropertyValue value = source.TryGetValue(definition.Name, out DesignPropertyValue? stored)
                ? stored
                : definition.DefaultValue;
            if (!definition.TryParse(definition.Format(value), out DesignPropertyValue normalizedValue, out error))
            {
                normalized = new Dictionary<string, DesignPropertyValue>();
                return false;
            }
            result[definition.Name] = normalizedValue;
        }

        normalized = result;
        error = null;
        return true;
    }

    private static string NormalizeTypeName(string? typeName)
        => typeName switch
        {
            "RippleEffect" => RippleTypeName,
            "PressScaleEffect" => PressScaleTypeName,
            _ => typeName ?? string.Empty
        };

    private static EffectPropertyDefinition[]? GetDefinitions(string typeName)
        => NormalizeTypeName(typeName) switch
        {
            RippleTypeName => RippleProperties,
            PressScaleTypeName => PressScaleProperties,
            _ => null
        };

    private enum EffectPropertyKind
    {
        Boolean,
        Integer,
        Number,
        Enum,
        ColorArgb
    }

    private sealed class EffectPropertyDefinition
    {
        private EffectPropertyDefinition(
            string name,
            EffectPropertyKind kind,
            DesignPropertyValue defaultValue,
            double minimum = double.MinValue,
            double maximum = double.MaxValue,
            string? enumTypeName = null,
            string[]? enumMembers = null)
        {
            Name = name;
            Kind = kind;
            DefaultValue = defaultValue;
            Minimum = minimum;
            Maximum = maximum;
            EnumTypeName = enumTypeName;
            EnumMembers = enumMembers ?? [];
        }

        public string Name { get; }
        public EffectPropertyKind Kind { get; }
        public DesignPropertyValue DefaultValue { get; }
        public double Minimum { get; }
        public double Maximum { get; }
        public string? EnumTypeName { get; }
        public IReadOnlyList<string> EnumMembers { get; }

        public static EffectPropertyDefinition Boolean(string name, bool defaultValue)
            => new(name, EffectPropertyKind.Boolean, DesignPropertyValue.FromBoolean(defaultValue));

        public static EffectPropertyDefinition Integer(string name, int defaultValue, int minimum, int maximum)
            => new(name, EffectPropertyKind.Integer, DesignPropertyValue.FromInt32(defaultValue), minimum, maximum);

        public static EffectPropertyDefinition Number(string name, double defaultValue, double minimum, double maximum)
            => new(name, EffectPropertyKind.Number, DesignPropertyValue.FromDouble(defaultValue), minimum, maximum);

        public static EffectPropertyDefinition ColorArgb(string name, int defaultValue)
            => new(name, EffectPropertyKind.ColorArgb, DesignPropertyValue.FromInt32(defaultValue));

        public static EffectPropertyDefinition Enum(
            string name,
            string enumTypeName,
            string defaultMember,
            params string[] members)
            => new(
                name,
                EffectPropertyKind.Enum,
                DesignPropertyValue.FromEnum(enumTypeName, defaultMember),
                enumTypeName: enumTypeName,
                enumMembers: members);

        public string Format(DesignPropertyValue value)
            => Kind switch
            {
                EffectPropertyKind.Boolean => value.Value is bool boolValue && boolValue ? "true" : "false",
                EffectPropertyKind.Integer => Convert.ToInt32(value.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                EffectPropertyKind.Number => Convert.ToDouble(value.Value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture),
                EffectPropertyKind.Enum => value.GetString(),
                EffectPropertyKind.ColorArgb => $"#{unchecked((uint)Convert.ToInt32(value.Value, CultureInfo.InvariantCulture)):X8}",
                _ => throw new ArgumentOutOfRangeException()
            };

        public bool TryParse(string text, out DesignPropertyValue value, out string? error)
        {
            switch (Kind)
            {
                case EffectPropertyKind.Boolean:
                    if (bool.TryParse(text, out bool boolValue))
                    {
                        value = DesignPropertyValue.FromBoolean(boolValue);
                        error = null;
                        return true;
                    }
                    break;
                case EffectPropertyKind.Integer:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue)
                        && intValue >= Minimum && intValue <= Maximum)
                    {
                        value = DesignPropertyValue.FromInt32(intValue);
                        error = null;
                        return true;
                    }
                    break;
                case EffectPropertyKind.Number:
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                        && double.IsFinite(number)
                        && number >= Minimum && number <= Maximum)
                    {
                        value = DesignPropertyValue.FromDouble(number);
                        error = null;
                        return true;
                    }
                    break;
                case EffectPropertyKind.Enum:
                    if (EnumMembers.Contains(text, StringComparer.Ordinal))
                    {
                        value = DesignPropertyValue.FromEnum(EnumTypeName!, text);
                        error = null;
                        return true;
                    }
                    break;
                case EffectPropertyKind.ColorArgb:
                    if (text.Length == 9
                        && text[0] == '#'
                        && uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
                    {
                        value = DesignPropertyValue.FromInt32(unchecked((int)argb));
                        error = null;
                        return true;
                    }
                    break;
            }

            value = DesignPropertyValue.FromNull();
            error = $"Value '{text}' is not valid for {Name}.";
            return false;
        }
    }
}
