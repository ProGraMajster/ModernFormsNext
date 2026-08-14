using System.Globalization;
using System.Text;
using ModernFormsNext.Designing;

namespace ModernFormsNext.CodeGeneration.Utilities;

/// <summary>
/// Writes primitive designer values as deterministic C# literals.
/// </summary>
public static class CSharpLiteralWriter
{
    /// <summary>
    /// Writes a designer property value as a C# expression.
    /// </summary>
    /// <param name="value">The designer property value, or <see langword="null"/> for a literal null assignment.</param>
    /// <returns>A C# expression representing the value.</returns>
    public static string WriteValue(DesignPropertyValue? value)
    {
        if (value is null)
            return "null";

        return value.Kind switch
        {
            DesignPropertyValueKind.Null => "null",
            DesignPropertyValueKind.String => WriteStringLiteral((string?)value.Value),
            DesignPropertyValueKind.Boolean => value.Value is bool boolValue && boolValue ? "true" : "false",
            DesignPropertyValueKind.Int32 => Convert.ToInt32(value.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            DesignPropertyValueKind.Double => Convert.ToDouble(value.Value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture),
            DesignPropertyValueKind.Enum => WriteEnumValue(value),
            DesignPropertyValueKind.Object => WriteObjectValue(value),
            _ => throw new NotSupportedException($"Unsupported designer property value kind '{value.Kind}'.")
        };
    }

    /// <summary>
    /// Writes a string as a C# string literal.
    /// </summary>
    /// <param name="value">The string to write.</param>
    /// <returns>A C# string literal.</returns>
    public static string WriteStringLiteral(string? value)
    {
        if (value is null)
            return "null";

        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');

        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\0' => "\\0",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\v' => "\\v",
                _ when char.IsControl(character) => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                _ => character.ToString()
            });
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static string WriteEnumValue(DesignPropertyValue value)
    {
        var memberName = (string?)value.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value.EnumTypeName))
            return WriteStringLiteral(memberName);

        var members = memberName.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (members.Length > 0 && members.All(CSharpIdentifierSanitizer.IsValidIdentifier))
        {
            return string.Join(
                " | ",
                members.Select(member => $"{value.EnumTypeName}.{member}"));
        }

        // Enum.ToString() returns a numeric value when no named member represents the value.
        // Emit a cast instead of producing an invalid member-access expression such as Type.3.
        if (long.TryParse(memberName, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            || ulong.TryParse(memberName, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return $"({value.EnumTypeName}){memberName}";
        }

        return value.EnumTypeName + "." + memberName;
    }

    private static string WriteObjectValue(DesignPropertyValue value)
    {
        var typeName = value.ObjectTypeName ?? string.Empty;
        var properties = value.ObjectProperties ?? new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);

        if (IsType(typeName, "System.Drawing.Size"))
            return $"new System.Drawing.Size({ReadInt(properties, "Width")}, {ReadInt(properties, "Height")})";

        if (IsType(typeName, "System.Drawing.Point"))
            return $"new System.Drawing.Point({ReadInt(properties, "X")}, {ReadInt(properties, "Y")})";

        if (IsType(typeName, "System.Drawing.PointF"))
            return $"new System.Drawing.PointF({ReadFloat(properties, "X")}, {ReadFloat(properties, "Y")})";

        if (IsType(typeName, "System.Numerics.Matrix3x2"))
        {
            return "new System.Numerics.Matrix3x2("
                + $"{ReadFloat(properties, "M11", 1)}, {ReadFloat(properties, "M12")}, "
                + $"{ReadFloat(properties, "M21")}, {ReadFloat(properties, "M22", 1)}, "
                + $"{ReadFloat(properties, "M31")}, {ReadFloat(properties, "M32")})";
        }

        if (IsType(typeName, "System.Drawing.Rectangle"))
        {
            return $"new System.Drawing.Rectangle({ReadInt(properties, "X")}, {ReadInt(properties, "Y")}, {ReadInt(properties, "Width")}, {ReadInt(properties, "Height")})";
        }

        if (IsType(typeName, "System.Drawing.RectangleF"))
        {
            return $"new System.Drawing.RectangleF({ReadFloat(properties, "X")}, {ReadFloat(properties, "Y")}, {ReadFloat(properties, "Width")}, {ReadFloat(properties, "Height")})";
        }

        if (IsType(typeName, "ModernFormsNext.Padding"))
        {
            return $"new Padding({ReadInt(properties, "Left")}, {ReadInt(properties, "Top")}, {ReadInt(properties, "Right")}, {ReadInt(properties, "Bottom")})";
        }

        if (IsType(typeName, "SkiaSharp.SKPoint"))
        {
            return $"new SkiaSharp.SKPoint({ReadFloat(properties, "X")}, {ReadFloat(properties, "Y")})";
        }

        if (IsType(typeName, "SkiaSharp.SKSize"))
        {
            return $"new SkiaSharp.SKSize({ReadFloat(properties, "Width")}, {ReadFloat(properties, "Height")})";
        }

        if (IsType(typeName, "SkiaSharp.SKRect"))
        {
            return $"new SkiaSharp.SKRect({ReadFloat(properties, "Left")}, {ReadFloat(properties, "Top")}, {ReadFloat(properties, "Right")}, {ReadFloat(properties, "Bottom")})";
        }

        if (IsType(typeName, "SkiaSharp.SKColor"))
        {
            return $"new SkiaSharp.SKColor({ReadByte(properties, "R")}, {ReadByte(properties, "G")}, {ReadByte(properties, "B")}, {ReadByte(properties, "A", 255)})";
        }

        if (IsType(typeName, "ModernFormsNext.Font"))
        {
            var name = WriteStringLiteral(ReadString(properties, "Name", "Segoe UI"));
            var size = ReadDouble(properties, "Size", 9).ToString("R", CultureInfo.InvariantCulture) + "f";
            var style = WriteFontStyle(properties);
            return $"new Font({name}, {size}, {style})";
        }

        if (IsType(typeName, "ModernFormsNext.Drawing.PointCollection"))
            return WritePointCollection(properties);

        if (IsType(typeName, "ModernFormsNext.Drawing.LineGeometry"))
        {
            return "new ModernFormsNext.Drawing.LineGeometry("
                + ReadValue(properties, "StartPoint", "System.Drawing.PointF.Empty") + ", "
                + ReadValue(properties, "EndPoint", "System.Drawing.PointF.Empty") + ")"
                + WriteGeometryInitializer(properties);
        }

        if (IsType(typeName, "ModernFormsNext.Drawing.RectangleGeometry"))
        {
            return "new ModernFormsNext.Drawing.RectangleGeometry("
                + ReadValue(properties, "Rect", "System.Drawing.RectangleF.Empty") + ")"
                + WriteGeometryInitializer(properties);
        }

        if (IsType(typeName, "ModernFormsNext.Drawing.EllipseGeometry"))
        {
            return "new ModernFormsNext.Drawing.EllipseGeometry("
                + ReadValue(properties, "Rect", "System.Drawing.RectangleF.Empty") + ")"
                + WriteGeometryInitializer(properties);
        }

        if (IsType(typeName, "ModernFormsNext.Drawing.PathGeometry"))
            return WritePathGeometry(properties);

        if (IsType(typeName, "ModernFormsNext.Drawing.SolidColorBrush"))
        {
            var color = properties.TryGetValue("Color", out var colorValue)
                ? WriteValue(colorValue)
                : "new SkiaSharp.SKColor(255, 255, 255, 255)";
            return $"new ModernFormsNext.Drawing.SolidColorBrush({color}) {{ {WriteBrushProperties(properties)} }}";
        }

        if (IsType(typeName, "ModernFormsNext.Drawing.GlassBrush"))
        {
            return "new ModernFormsNext.Drawing.GlassBrush"
                + " { "
                + $"TintColor = {ReadValue(properties, "TintColor", "new SkiaSharp.SKColor(255, 255, 255, 28)")}, "
                + $"SecondaryTintColor = {ReadValue(properties, "SecondaryTintColor", "new SkiaSharp.SKColor(255, 255, 255, 12)")}, "
                + $"HighlightColor = {ReadValue(properties, "HighlightColor", "new SkiaSharp.SKColor(255, 255, 255, 38)")}, "
                + $"BorderColor = {ReadValue(properties, "BorderColor", "new SkiaSharp.SKColor(255, 255, 255, 65)")}, "
                + $"ShowHighlight = {ReadBoolLiteral(properties, "ShowHighlight", fallback: true)}, "
                + $"ShowInnerBorder = {ReadBoolLiteral(properties, "ShowInnerBorder", fallback: true)}, "
                + WriteBrushProperties(properties)
                + " }";
        }

        if (IsType(typeName, "ModernFormsNext.Drawing.LinearGradientBrush"))
        {
            return "new ModernFormsNext.Drawing.LinearGradientBrush"
                + " { "
                + $"StartPoint = {ReadValue(properties, "StartPoint", "new SkiaSharp.SKPoint(0f, 0f)")}, "
                + $"EndPoint = {ReadValue(properties, "EndPoint", "new SkiaSharp.SKPoint(1f, 1f)")}"
                + WriteGradientBrushProperties(properties)
                + WriteGradientStopsInitializer(properties)
                + " }";
        }

        if (IsType(typeName, "ModernFormsNext.Drawing.RadialGradientBrush"))
        {
            return "new ModernFormsNext.Drawing.RadialGradientBrush"
                + " { "
                + $"Center = {ReadValue(properties, "Center", "new SkiaSharp.SKPoint(0.5f, 0.5f)")}, "
                + $"Radius = {ReadFloat(properties, "Radius", 0.5)}"
                + WriteOptionalProperty(properties, "GradientOrigin")
                + WriteGradientBrushProperties(properties)
                + WriteGradientStopsInitializer(properties)
                + " }";
        }

        if (IsType(typeName, "ModernFormsNext.Drawing.SweepGradientBrush"))
        {
            return "new ModernFormsNext.Drawing.SweepGradientBrush"
                + " { "
                + $"Center = {ReadValue(properties, "Center", "new SkiaSharp.SKPoint(0.5f, 0.5f)")}, "
                + $"StartAngle = {ReadFloat(properties, "StartAngle")}, "
                + $"EndAngle = {ReadFloat(properties, "EndAngle", 360)}"
                + WriteGradientBrushProperties(properties)
                + WriteGradientStopsInitializer(properties)
                + " }";
        }

        throw new NotSupportedException($"Unsupported structured designer property value type '{typeName}'.");
    }

    private static bool IsType(string actual, string expected)
        => string.Equals(actual, expected, StringComparison.Ordinal)
        || string.Equals(actual, expected.Split('.').Last(), StringComparison.Ordinal);

    private static int ReadInt(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name, int fallback = 0)
        => properties.TryGetValue(name, out var value) && value is not null
            ? Convert.ToInt32(value.Value, CultureInfo.InvariantCulture)
            : fallback;

    private static byte ReadByte(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name, byte fallback = 0)
        => (byte)Math.Clamp(ReadInt(properties, name, fallback), byte.MinValue, byte.MaxValue);

    private static double ReadDouble(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name, double fallback = 0)
        => properties.TryGetValue(name, out var value) && value is not null
            ? Convert.ToDouble(value.Value, CultureInfo.InvariantCulture)
            : fallback;

    private static string ReadFloat(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name, double fallback = 0)
        => ((float)ReadDouble(properties, name, fallback)).ToString("R", CultureInfo.InvariantCulture) + "f";

    private static string ReadString(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name, string fallback = "")
        => properties.TryGetValue(name, out var value) && value is not null
            ? value.Value?.ToString() ?? fallback
            : fallback;

    private static string ReadValue(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        string name,
        string fallback)
        => properties.TryGetValue(name, out var value) && value is not null
            ? WriteValue(value)
            : fallback;

    private static string ReadBoolLiteral(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        string name,
        bool fallback = false)
        => properties.TryGetValue(name, out var value) && value is not null
            ? WriteValue(value)
            : fallback ? "true" : "false";

    private static string WriteBrushProperties(IReadOnlyDictionary<string, DesignPropertyValue> properties)
        => $"Opacity = {ReadFloat(properties, "Opacity", 1)}, "
            + $"Transform = {ReadValue(properties, "Transform", "System.Numerics.Matrix3x2.Identity")}";

    private static string WriteGradientBrushProperties(IReadOnlyDictionary<string, DesignPropertyValue> properties)
        => $", {WriteBrushProperties(properties)}, SpreadMode = "
            + ReadValue(properties, "SpreadMode", "ModernFormsNext.Drawing.GradientSpreadMode.Pad");

    private static string WriteOptionalProperty(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        string name)
        => properties.TryGetValue(name, out DesignPropertyValue? value) && value is not null
            ? $", {name} = {WriteValue(value)}"
            : string.Empty;

    private static string WriteGradientStopsInitializer(IReadOnlyDictionary<string, DesignPropertyValue> properties)
    {
        var stops = new List<string>();
        var count = ReadInt(properties, "GradientStopCount");

        for (var index = 0; index < count; index++)
        {
            if (!properties.TryGetValue($"GradientStop{index}", out var stop)
                || stop.ObjectProperties is null)
            {
                continue;
            }

            var color = ReadValue(stop.ObjectProperties, "Color", "SkiaSharp.SKColors.Transparent");
            var offset = ReadFloat(stop.ObjectProperties, "Offset");
            stops.Add($"new ModernFormsNext.Drawing.GradientStop({color}, {offset})");
        }

        return stops.Count == 0
            ? string.Empty
            : $", GradientStops = {{ {string.Join(", ", stops)} }}";
    }

    private static string WritePointCollection(IReadOnlyDictionary<string, DesignPropertyValue> properties)
    {
        var points = new List<string>();
        int count = ReadInt(properties, "Count");
        for (int index = 0; index < count; index++)
        {
            if (properties.TryGetValue($"Point{index}", out DesignPropertyValue? point))
                points.Add(WriteValue(point));
        }
        return points.Count == 0
            ? "new ModernFormsNext.Drawing.PointCollection()"
            : $"new ModernFormsNext.Drawing.PointCollection {{ {string.Join(", ", points)} }}";
    }

    private static string WriteGeometryInitializer(IReadOnlyDictionary<string, DesignPropertyValue> properties)
        => properties.TryGetValue("Transform", out DesignPropertyValue? transform)
            ? $" {{ Transform = {WriteValue(transform)} }}"
            : string.Empty;

    private static string WritePathGeometry(IReadOnlyDictionary<string, DesignPropertyValue> properties)
    {
        var figures = new List<string>();
        int count = ReadInt(properties, "FigureCount");
        for (int index = 0; index < count; index++)
        {
            if (properties.TryGetValue($"Figure{index}", out DesignPropertyValue? figure))
                figures.Add(WritePathFigure(figure));
        }

        var assignments = new List<string>
        {
            "FillRule = " + ReadValue(properties, "FillRule", "ModernFormsNext.Drawing.GeometryFillRule.Winding"),
            "Transform = " + ReadValue(properties, "Transform", "System.Numerics.Matrix3x2.Identity")
        };
        if (figures.Count > 0)
            assignments.Add($"Figures = {{ {string.Join(", ", figures)} }}");
        return $"new ModernFormsNext.Drawing.PathGeometry {{ {string.Join(", ", assignments)} }}";
    }

    private static string WritePathFigure(DesignPropertyValue value)
    {
        IReadOnlyDictionary<string, DesignPropertyValue> properties = value.ObjectProperties
            ?? new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        var segments = new List<string>();
        int count = ReadInt(properties, "SegmentCount");
        for (int index = 0; index < count; index++)
        {
            if (properties.TryGetValue($"Segment{index}", out DesignPropertyValue? segment))
                segments.Add(WritePathSegment(segment));
        }

        string result = "new ModernFormsNext.Drawing.PathFigure("
            + ReadValue(properties, "StartPoint", "System.Drawing.PointF.Empty") + ", "
            + ReadBoolLiteral(properties, "IsClosed") + ")";
        return segments.Count == 0
            ? result
            : result + $" {{ Segments = {{ {string.Join(", ", segments)} }} }}";
    }

    private static string WritePathSegment(DesignPropertyValue value)
    {
        string typeName = value.ObjectTypeName ?? string.Empty;
        IReadOnlyDictionary<string, DesignPropertyValue> properties = value.ObjectProperties
            ?? new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        if (IsType(typeName, "ModernFormsNext.Drawing.LineSegment"))
            return $"new ModernFormsNext.Drawing.LineSegment({ReadValue(properties, "Point", "System.Drawing.PointF.Empty")})";
        if (IsType(typeName, "ModernFormsNext.Drawing.QuadraticBezierSegment"))
        {
            return "new ModernFormsNext.Drawing.QuadraticBezierSegment("
                + ReadValue(properties, "ControlPoint", "System.Drawing.PointF.Empty") + ", "
                + ReadValue(properties, "Point", "System.Drawing.PointF.Empty") + ")";
        }
        if (IsType(typeName, "ModernFormsNext.Drawing.BezierSegment"))
        {
            return "new ModernFormsNext.Drawing.BezierSegment("
                + ReadValue(properties, "ControlPoint1", "System.Drawing.PointF.Empty") + ", "
                + ReadValue(properties, "ControlPoint2", "System.Drawing.PointF.Empty") + ", "
                + ReadValue(properties, "Point", "System.Drawing.PointF.Empty") + ")";
        }
        throw new NotSupportedException($"Unsupported path segment type '{typeName}'.");
    }

    private static string WriteFontStyle(IReadOnlyDictionary<string, DesignPropertyValue> properties)
    {
        var members = new List<string>();

        if (ReadBool(properties, "Bold"))
            members.Add("FontStyle.Bold");
        if (ReadBool(properties, "Italic"))
            members.Add("FontStyle.Italic");
        if (ReadBool(properties, "Underline"))
            members.Add("FontStyle.Underline");
        if (ReadBool(properties, "Strikeout"))
            members.Add("FontStyle.Strikeout");

        return members.Count == 0
            ? "FontStyle.Regular"
            : string.Join(" | ", members);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name)
        => properties.TryGetValue(name, out var value)
        && value is not null
        && value.Value is bool boolValue
        && boolValue;
}
