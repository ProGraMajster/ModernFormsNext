using System.Globalization;

namespace ModernFormsNext.Designing;

public sealed partial class DesignDocumentValidator
{
    // These are neutral persistence schemas, not runtime type resolution. Unknown metadata
    // remains data; consumers must separately reject unsupported target types.
    private static string? ValidateStructuredShape(DesignPropertyValue value)
    {
        var properties = value.ObjectProperties!;
        string type = value.ObjectTypeName!;
        if (type.StartsWith("global::", StringComparison.Ordinal)) type = type[8..];
        string drawing = type.StartsWith("ModernFormsNext.Drawing.", StringComparison.Ordinal)
            ? type["ModernFormsNext.Drawing.".Length..] : type.Contains('.') ? string.Empty : type;
        string? error = drawing switch
        {
            "PathGeometry" => ValidateIndexedValues(properties, "FigureCount", "Figure", "ModernFormsNext.Drawing.PathFigure"),
            "PathFigure" => ValidateIndexedValues(properties, "SegmentCount", "Segment",
                "ModernFormsNext.Drawing.LineSegment", "ModernFormsNext.Drawing.QuadraticBezierSegment", "ModernFormsNext.Drawing.BezierSegment"),
            "PointCollection" => ValidateIndexedValues(properties, "Count", "Point", "System.Drawing.PointF"),
            "LinearGradientBrush" or "RadialGradientBrush" or "SweepGradientBrush"
                => ValidateIndexedValues(properties, "GradientStopCount", "GradientStop", "ModernFormsNext.Drawing.GradientStop"),
            _ => null
        };
        if (error is not null) return error;

        string[]? allowed = drawing switch
        {
            "LineGeometry" => ["StartPoint", "EndPoint", "Transform"],
            "RectangleGeometry" or "EllipseGeometry" => ["Rect", "Transform"],
            "PathGeometry" => ["FigureCount", "FillRule", "Transform"],
            "PathFigure" => ["SegmentCount", "StartPoint", "IsClosed"],
            "LineSegment" => ["Point"],
            "QuadraticBezierSegment" => ["ControlPoint", "Point"],
            "BezierSegment" => ["ControlPoint1", "ControlPoint2", "Point"],
            "PointCollection" => ["Count"],
            _ => null
        };
        string? indexedPrefix = drawing switch { "PathGeometry" => "Figure", "PathFigure" => "Segment", "PointCollection" => "Point", _ => null };
        if (allowed is not null)
            foreach (var name in properties.Keys)
                if (!allowed.Contains(name, StringComparer.Ordinal)
                    && !(indexedPrefix is not null && name.StartsWith(indexedPrefix, StringComparison.Ordinal)))
                    return "A known geometry value contains an unsupported member.";

        string[] pointFields = drawing switch
        {
            "LineGeometry" => ["StartPoint", "EndPoint"],
            "PathFigure" => ["StartPoint"],
            "LineSegment" => ["Point"],
            "QuadraticBezierSegment" => ["ControlPoint", "Point"],
            "BezierSegment" => ["ControlPoint1", "ControlPoint2", "Point"],
            _ => []
        };
        foreach (string field in pointFields)
            if (properties.TryGetValue(field, out var point) && !IsStructuredValue(point, "System.Drawing.PointF"))
                return "A geometry point must use its known PointF data shape.";
        if (drawing is "RectangleGeometry" or "EllipseGeometry"
            && properties.TryGetValue("Rect", out var rectangle) && !IsStructuredValue(rectangle, "System.Drawing.RectangleF"))
            return "A geometry rectangle must use its known RectangleF data shape.";
        if (allowed is not null && properties.TryGetValue("Transform", out var transform)
            && !IsStructuredValue(transform, "System.Numerics.Matrix3x2"))
            return "A geometry transform must use its known Matrix3x2 data shape.";
        if (drawing == "PathFigure" && properties.TryGetValue("IsClosed", out var closed)
            && (closed?.Kind != DesignPropertyValueKind.Boolean || closed.Value is not bool))
            return "A path figure IsClosed member must be Boolean.";
        if (drawing == "PathGeometry" && properties.TryGetValue("FillRule", out var fill)
            && !IsKnownEnum(fill, "ModernFormsNext.Drawing.GeometryFillRule", "Winding", "EvenOdd"))
            return "A path fill rule must be a supported GeometryFillRule value.";

        string[]? floatingFields = IsStructuredValue(value, "System.Drawing.PointF") || IsStructuredValue(value, "SkiaSharp.SKPoint")
            ? ["X", "Y"] : IsStructuredValue(value, "System.Drawing.SizeF") || IsStructuredValue(value, "SkiaSharp.SKSize")
            ? ["Width", "Height"] : IsStructuredValue(value, "System.Drawing.RectangleF")
            ? ["X", "Y", "Width", "Height"] : IsStructuredValue(value, "SkiaSharp.SKRect")
            ? ["Left", "Top", "Right", "Bottom"] : IsStructuredValue(value, "System.Numerics.Matrix3x2")
            ? ["M11", "M12", "M21", "M22", "M31", "M32"] : null;
        if (floatingFields is not null)
            foreach (var field in properties)
                if (!floatingFields.Contains(field.Key, StringComparer.Ordinal) || !IsFiniteFloat(field.Value))
                    return "A floating geometry member must be a supported finite Single coordinate.";
        string[] numericFields = drawing switch
        {
            "SolidColorBrush" or "GlassBrush" or "LinearGradientBrush" => ["Opacity"],
            "RadialGradientBrush" => ["Opacity", "Radius"],
            "SweepGradientBrush" => ["Opacity", "StartAngle", "EndAngle"],
            "GradientStop" => ["Offset"],
            _ => []
        };
        foreach (string field in numericFields)
            if (properties.TryGetValue(field, out var number) && !IsFiniteFloat(number))
                return "A gradient numeric member must remain finite after Single conversion.";
        if (IsStructuredValue(value, "ModernFormsNext.Font") && properties.TryGetValue("Size", out var fontSize)
            && !IsFiniteFloat(fontSize))
            return "A font size must remain finite after Single conversion.";
        string[] brushPoints = drawing switch
        {
            "LinearGradientBrush" => ["StartPoint", "EndPoint"],
            "RadialGradientBrush" or "SweepGradientBrush" => ["Center"],
            _ => []
        };
        foreach (string field in brushPoints)
            if (properties.TryGetValue(field, out var point) && !IsStructuredValue(point, "SkiaSharp.SKPoint"))
                return "A gradient point must use its known SKPoint data shape.";
        if (drawing == "RadialGradientBrush" && properties.TryGetValue("GradientOrigin", out var origin)
            && !IsStructuredValue(origin, "System.Drawing.PointF"))
            return "A gradient origin must use its known PointF data shape.";
        return null;
    }

    private static string? ValidateIndexedValues(IReadOnlyDictionary<string, DesignPropertyValue> properties,
        string countName, string prefix, params string[] elementTypes)
    {
        int count = 0;
        if (properties.TryGetValue(countName, out var stored))
        {
            if (stored?.Kind != DesignPropertyValueKind.Int32 || stored.Value is not int number
                || number < 0 || number > MaximumPropertyValueCount || number > properties.Count)
                return "A structured collection count must be a bounded non-negative integer matching its actual members.";
            count = number;
        }
        // Bound by actual dictionary size before examining indexed members. A declared count
        // never controls an unbounded loop or causes missing entries to be silently omitted.
        for (int index = 0; index < count; index++)
            if (!properties.TryGetValue(prefix + index.ToString(CultureInfo.InvariantCulture), out var child)
                || !elementTypes.Any(type => IsStructuredValue(child, type)))
                return "A structured collection contains a missing or incorrectly typed element.";
        foreach (string key in properties.Keys)
        {
            if (key == countName || !key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string suffix = key[prefix.Length..];
            if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                || index >= count || suffix != index.ToString(CultureInfo.InvariantCulture))
                return "A structured collection contains an unclaimed or non-canonical indexed member.";
        }
        return null;
    }

    private static bool IsStructuredValue(DesignPropertyValue? value, string expected)
    {
        if (value?.Kind != DesignPropertyValueKind.Object || value.ObjectProperties is null) return false;
        string? actual = value.ObjectTypeName;
        if (actual?.StartsWith("global::", StringComparison.Ordinal) == true) actual = actual[8..];
        return actual == expected || actual == expected[(expected.LastIndexOf('.') + 1)..];
    }

    private static bool IsFiniteFloat(DesignPropertyValue? value)
        => value?.Kind == DesignPropertyValueKind.Int32 && value.Value is int
        || value?.Kind == DesignPropertyValueKind.Double && value.Value is double number && float.IsFinite((float)number);

    private static bool IsKnownEnum(DesignPropertyValue? value, string expected, params string[] names)
    {
        if (value?.Kind != DesignPropertyValueKind.Enum || value.Value is not string member) return false;
        string? type = value.EnumTypeName;
        if (type?.StartsWith("global::", StringComparison.Ordinal) == true) type = type[8..];
        if (type != expected && type != expected[(expected.LastIndexOf('.') + 1)..]) return false;
        return names.Contains(member, StringComparer.Ordinal)
            || int.TryParse(member, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number < names.Length;
    }
}
