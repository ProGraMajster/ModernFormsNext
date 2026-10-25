using System.Globalization;
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
            var style = new ControlStyle(null);
            foreach ((string path, DesignPropertyValue value) in properties)
            {
                string[] parts = path.Split('.');
                string member = parts[^1];
                if (member is not (nameof(Control.LineHeight) or nameof(Control.LetterSpacing))) continue;
                bool controlMetric = parts.Length == 1 && type.GetProperty(member)?.DeclaringType == typeof(Control);
                bool styleMetric = parts.Length == 2 && type.GetProperty(parts[0])?.PropertyType == typeof(ControlStyle);
                if (!controlMetric && !styleMetric) continue;
                double? number = value.Kind == DesignPropertyValueKind.Null ? null
                    : value.Kind is DesignPropertyValueKind.Double or DesignPropertyValueKind.Int32
                        ? Convert.ToDouble(value.Value, CultureInfo.InvariantCulture)
                        : throw new FormatException("A typography metric must be numeric or null.");
                if (member == nameof(Control.LineHeight)) style.LineHeight = number;
                else style.LetterSpacing = number;
            }
            return true;
        }
        catch (Exception failure) when (failure is ArgumentException or FormatException or InvalidCastException or OverflowException)
        {
            error = failure.Message;
            return false;
        }
    }
}
