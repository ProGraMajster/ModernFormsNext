using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

// Read trusted CLR metadata and neutral scalar values only. Reuse the runtime style's range
// admission without constructing a control, executing a getter or activating project metadata.
internal static class DesignerTypographyPropertyValidator
{
    internal static bool TryValidate(IReadOnlyDictionary<string, DesignPropertyValue> properties,
        Type type, out string? error)
    {
        error = null;
        if (type.Assembly != typeof(Control).Assembly || !typeof(Control).IsAssignableFrom(type)) return true;
        try
        {
            foreach ((string path, DesignPropertyValue value) in properties)
            {
                string[] parts = path.Split('.');
                string member = parts[^1];
                if (member is not (nameof(Control.LineHeight) or nameof(Control.LetterSpacing))) continue;
                bool controlMetric = parts.Length == 1 && type.GetProperty(member)?.DeclaringType == typeof(Control);
                bool styleMetric = parts.Length == 2 && type.GetProperty(parts[0])?.PropertyType == typeof(ControlStyle);
                if (!controlMetric && !styleMetric) continue;
                if (!TryValidateMetricValue(member, value, out _, out error)) return false;
            }
            return true;
        }
        catch (Exception failure) when (failure is ArgumentException or FormatException or InvalidCastException or OverflowException)
        {
            error = failure.Message;
            return false;
        }
    }

    // Both nested style editors and preview admission use the same closed scalar contract
    // and runtime bounds. Inspect the stored CLR scalar directly rather than converting an
    // arbitrary object supplied by a malformed in-memory document.
    internal static bool TryValidateMetricValue(string member, DesignPropertyValue value,
        out double? number, out string? error)
    {
        number = null;
        error = null;
        try
        {
            ArgumentNullException.ThrowIfNull(value);
            number = value.Kind switch
            {
                DesignPropertyValueKind.Null when value.Value is null => null,
                DesignPropertyValueKind.Double when value.Value is double scalar => scalar,
                DesignPropertyValueKind.Int32 when value.Value is int scalar => scalar,
                _ => throw new FormatException("A typography metric must be numeric or null.")
            };
            var style = new ControlStyle(null);
            if (member == nameof(Control.LineHeight)) style.LineHeight = number;
            else if (member == nameof(Control.LetterSpacing)) style.LetterSpacing = number;
            else throw new FormatException("Unknown typography metric.");
            return true;
        }
        catch (Exception failure) when (failure is ArgumentException or FormatException or InvalidCastException or OverflowException)
        {
            error = failure.Message;
            return false;
        }
    }
}
