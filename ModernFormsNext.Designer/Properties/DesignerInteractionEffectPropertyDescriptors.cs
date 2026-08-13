using System.Globalization;
using ModernFormsNext.Designing;
using SkiaSharp;

namespace ModernFormsNext.Designer.Properties;

/// <summary>
/// Builds ordinary Designer Property Grid descriptors from detached animation metadata.
/// The resulting accessors edit only the dialog's temporary entry and never instantiate
/// the described runtime effect type.
/// </summary>
internal static class DesignerInteractionEffectPropertyDescriptors
{
    public static IReadOnlyList<DesignerPropertyDescriptor> Create(
        DesignerInteractionEffectEntry? entry)
    {
        if (entry?.Descriptor is null)
            return [];

        return entry.Descriptor.Properties
            .Select(definition => Create(entry, definition))
            .ToArray();
    }

    private static DesignerPropertyDescriptor Create(
        DesignerInteractionEffectEntry entry,
        DesignAnimationPropertyDescriptor definition)
    {
        Type valueType = definition.Kind switch
        {
            DesignAnimationPropertyKind.Boolean => typeof(bool),
            DesignAnimationPropertyKind.Int32 => typeof(int),
            DesignAnimationPropertyKind.Number or DesignAnimationPropertyKind.TimeSpan => typeof(double),
            DesignAnimationPropertyKind.ColorArgb => typeof(SKColor),
            _ => typeof(string)
        };
        IReadOnlyList<string>? standardValues = definition.Kind switch
        {
            DesignAnimationPropertyKind.Easing => KnownEasingDesignValue.Identifiers,
            DesignAnimationPropertyKind.Enum => definition.EnumMembers,
            _ => null
        };
        bool numeric = definition.Kind is DesignAnimationPropertyKind.Int32
            or DesignAnimationPropertyKind.Number
            or DesignAnimationPropertyKind.TimeSpan;
        string displayName = definition.Kind == DesignAnimationPropertyKind.TimeSpan
            ? $"{definition.RuntimePropertyName} (ms)"
            : definition.RuntimePropertyName;

        Func<DesignerPropertyDialogContext, Task<bool>>? dialogEditor =
            definition.Kind == DesignAnimationPropertyKind.ColorArgb
                ? DesignerPropertyDialogEditors.Color(
                    () => GetValue(entry, definition) is SKColor color ? color : null,
                    color => color is { } selected
                        ? Set(entry, definition, FromColor(selected))
                        : (false, "A color value is required."))
                : null;

        return new DesignerPropertyDescriptor
        {
            Name = definition.Name,
            DisplayName = displayName,
            Category = "Effect",
            Description = GetDescription(definition),
            ValueType = valueType,
            StandardValues = standardValues,
            NumericMinimum = numeric ? ToDecimalBound(definition.Minimum, isMinimum: true) : null,
            NumericMaximum = numeric ? ToDecimalBound(definition.Maximum, isMinimum: false) : null,
            NumericIncrement = definition.Kind switch
            {
                DesignAnimationPropertyKind.Int32 => 1m,
                DesignAnimationPropertyKind.TimeSpan => 1m,
                DesignAnimationPropertyKind.Number => 0.01m,
                _ => null
            },
            NumericDecimalPlaces = definition.Kind == DesignAnimationPropertyKind.Number ? 3
                : definition.Kind == DesignAnimationPropertyKind.TimeSpan ? 2
                : 0,
            UseBooleanCheckBox = definition.Kind == DesignAnimationPropertyKind.Boolean,
            HasDialogEditor = dialogEditor is not null,
            DialogEditor = dialogEditor,
            GetValue = () => GetValue(entry, definition),
            CommitText = text => CommitText(entry, definition, text)
        };
    }

    private static object? GetValue(
        DesignerInteractionEffectEntry entry,
        DesignAnimationPropertyDescriptor definition)
    {
        DesignPropertyValue value = InteractionEffectDesignerRegistry.GetEffectiveValue(entry, definition);
        return definition.Kind switch
        {
            DesignAnimationPropertyKind.Boolean => value.Value is bool enabled && enabled,
            DesignAnimationPropertyKind.Int32 => Convert.ToInt32(value.Value, CultureInfo.InvariantCulture),
            DesignAnimationPropertyKind.Number or DesignAnimationPropertyKind.TimeSpan =>
                Convert.ToDouble(value.Value, CultureInfo.InvariantCulture),
            DesignAnimationPropertyKind.ColorArgb => ToColor(value),
            _ => value.GetString()
        };
    }

    private static (bool Success, string? Error) CommitText(
        DesignerInteractionEffectEntry entry,
        DesignAnimationPropertyDescriptor definition,
        string text)
    {
        DesignPropertyValue value;
        switch (definition.Kind)
        {
            case DesignAnimationPropertyKind.Boolean:
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(bool), out object? boolean, out string? boolError))
                    return (false, boolError);
                value = DesignPropertyValue.FromBoolean((bool)boolean!);
                break;
            case DesignAnimationPropertyKind.Int32:
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(int), out object? integer, out string? intError))
                    return (false, intError);
                value = DesignPropertyValue.FromInt32((int)integer!);
                break;
            case DesignAnimationPropertyKind.Number:
            case DesignAnimationPropertyKind.TimeSpan:
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(double), out object? number, out string? numberError))
                    return (false, numberError);
                value = DesignPropertyValue.FromDouble((double)number!);
                break;
            case DesignAnimationPropertyKind.Easing:
                value = DesignPropertyValue.FromString(text);
                break;
            case DesignAnimationPropertyKind.Enum:
                value = DesignPropertyValue.FromEnum(definition.EnumTypeName!, text);
                break;
            case DesignAnimationPropertyKind.ColorArgb:
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(SKColor), out object? color, out string? colorError))
                    return (false, colorError);
                value = FromColor((SKColor)color!);
                break;
            case DesignAnimationPropertyKind.String:
                value = DesignPropertyValue.FromString(text);
                break;
            default:
                return (false, "The property kind is not supported by the Designer.");
        }

        return Set(entry, definition, value);
    }

    private static (bool Success, string? Error) Set(
        DesignerInteractionEffectEntry entry,
        DesignAnimationPropertyDescriptor definition,
        DesignPropertyValue value)
        => InteractionEffectDesignerRegistry.TrySetProperty(entry, definition, value, out string? error)
            ? (true, null)
            : (false, error);

    private static SKColor ToColor(DesignPropertyValue value)
    {
        uint argb = unchecked((uint)Convert.ToInt32(value.Value, CultureInfo.InvariantCulture));
        return new SKColor(
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb,
            (byte)(argb >> 24));
    }

    private static DesignPropertyValue FromColor(SKColor color)
    {
        uint argb = ((uint)color.Alpha << 24)
            | ((uint)color.Red << 16)
            | ((uint)color.Green << 8)
            | color.Blue;
        return DesignPropertyValue.FromInt32(unchecked((int)argb));
    }

    private static decimal ToDecimalBound(double value, bool isMinimum)
    {
        if (value <= (double)decimal.MinValue)
            return decimal.MinValue;
        if (value >= (double)decimal.MaxValue)
            return decimal.MaxValue;
        decimal converted = (decimal)value;
        if (isMinimum && value > 0d && converted == 0m)
            return 0.001m;
        return converted;
    }

    private static string GetDescription(DesignAnimationPropertyDescriptor definition)
        => definition.Kind switch
        {
            DesignAnimationPropertyKind.TimeSpan => "Duration in milliseconds.",
            DesignAnimationPropertyKind.Easing => "Built-in easing used by this effect.",
            DesignAnimationPropertyKind.ColorArgb => "ARGB color rendered by this effect.",
            DesignAnimationPropertyKind.Enum => "Select one of the effect's supported values.",
            DesignAnimationPropertyKind.Boolean => "Enables or disables this effect option.",
            DesignAnimationPropertyKind.Int32 or DesignAnimationPropertyKind.Number =>
                $"Numeric value from {definition.Minimum.ToString(CultureInfo.InvariantCulture)} through {definition.Maximum.ToString(CultureInfo.InvariantCulture)}.",
            _ => "Value used by this effect."
        };
}
