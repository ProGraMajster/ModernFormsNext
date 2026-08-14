using System.Globalization;
using System.Numerics;
using ModernFormsNext.Designing;
using SkiaSharp;

namespace ModernFormsNext.Designer.Properties;

internal static class DesignerPropertyValueEditor
{
    public static IReadOnlyList<string>? GetStandardValues(Type valueType)
    {
        var type = Nullable.GetUnderlyingType(valueType) ?? valueType;

        if (type == typeof(bool))
            return ["True", "False"];

        return type.IsEnum ? Enum.GetNames(type) : null;
    }

    public static string ToDisplayString(object? value)
        => value switch
        {
            null => string.Empty,
            bool boolValue => boolValue ? "True" : "False",
            DesignBounds bounds => $"{bounds.X}, {bounds.Y}, {bounds.Width}, {bounds.Height}",
            DesignSize size => $"{size.Width}, {size.Height}",
            DesignPoint point => $"{point.X}, {point.Y}",
            System.Drawing.Size size => $"{size.Width}, {size.Height}",
            System.Drawing.Point point => $"{point.X}, {point.Y}",
            System.Drawing.PointF point => $"{point.X.ToString("R", CultureInfo.InvariantCulture)}, {point.Y.ToString("R", CultureInfo.InvariantCulture)}",
            System.Drawing.Rectangle rectangle => $"{rectangle.X}, {rectangle.Y}, {rectangle.Width}, {rectangle.Height}",
            System.Drawing.RectangleF rectangle => $"{rectangle.X.ToString("R", CultureInfo.InvariantCulture)}, {rectangle.Y.ToString("R", CultureInfo.InvariantCulture)}, {rectangle.Width.ToString("R", CultureInfo.InvariantCulture)}, {rectangle.Height.ToString("R", CultureInfo.InvariantCulture)}",
            SKPoint point => $"{point.X.ToString("R", CultureInfo.InvariantCulture)}, {point.Y.ToString("R", CultureInfo.InvariantCulture)}",
            SKSize size => $"{size.Width.ToString("R", CultureInfo.InvariantCulture)}, {size.Height.ToString("R", CultureInfo.InvariantCulture)}",
            SKRect rect => $"{rect.Left.ToString("R", CultureInfo.InvariantCulture)}, {rect.Top.ToString("R", CultureInfo.InvariantCulture)}, {rect.Right.ToString("R", CultureInfo.InvariantCulture)}, {rect.Bottom.ToString("R", CultureInfo.InvariantCulture)}",
            Padding padding => $"{padding.Left}, {padding.Top}, {padding.Right}, {padding.Bottom}",
            SKColor color => ToHex(color),
            ModernFormsNext.Drawing.SolidColorBrush solidBrush => ToHex(solidBrush.Color),
            ModernFormsNext.Drawing.GlassBrush => "GlassBrush",
            ModernFormsNext.Drawing.LinearGradientBrush linearBrush => $"LinearGradientBrush ({linearBrush.GradientStops.Count} stops)",
            ModernFormsNext.Drawing.RadialGradientBrush radialBrush => $"RadialGradientBrush ({radialBrush.GradientStops.Count} stops)",
            ModernFormsNext.Drawing.SweepGradientBrush sweepBrush => $"SweepGradientBrush ({sweepBrush.GradientStops.Count} stops)",
            ModernFormsNext.Drawing.Brush => "<custom brush>",
            ModernFormsNext.Drawing.PointCollection points => WritePointCollectionText(points),
            ModernFormsNext.Drawing.Geometry geometry => WriteGeometryText(geometry),
            ModernFormsNext.Font font => $"{font.FamilyName}, {font.SizeInPoints.ToString("G", CultureInfo.InvariantCulture)}pt",
            float floatValue => floatValue.ToString("R", CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

    public static bool TryConvert(string text, Type valueType, out object? value, out string? error)
    {
        var targetType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        var trimmed = text.Trim();

        if (Nullable.GetUnderlyingType(valueType) is not null && string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
        {
            value = null;
            error = null;
            return true;
        }

        if (targetType == typeof(string))
        {
            value = text;
            error = null;
            return true;
        }

        if (targetType == typeof(bool))
        {
            if (bool.TryParse(trimmed, out var boolValue))
            {
                value = boolValue;
                error = null;
                return true;
            }

            error = "Expected True or False.";
            value = null;
            return false;
        }

        if (targetType == typeof(int))
        {
            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                value = intValue;
                error = null;
                return true;
            }

            error = "Expected an integer value.";
            value = null;
            return false;
        }

        if (targetType == typeof(float))
        {
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
            {
                value = floatValue;
                error = null;
                return true;
            }

            error = "Expected a floating-point value.";
            value = null;
            return false;
        }

        if (targetType == typeof(double))
        {
            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                value = doubleValue;
                error = null;
                return true;
            }

            error = "Expected a floating-point value.";
            value = null;
            return false;
        }

        if (targetType.IsEnum)
        {
            if (Enum.TryParse(targetType, trimmed, ignoreCase: false, out var enumValue))
            {
                value = enumValue;
                error = null;
                return true;
            }

            error = $"Expected one of: {string.Join(", ", Enum.GetNames(targetType))}.";
            value = null;
            return false;
        }

        if (targetType == typeof(System.Drawing.Size))
            return TryParseSize(trimmed, out value, out error);

        if (targetType == typeof(System.Drawing.Point))
            return TryParsePoint(trimmed, out value, out error);

        if (targetType == typeof(System.Drawing.PointF))
            return TryParsePointF(trimmed, out value, out error);

        if (targetType == typeof(System.Drawing.Rectangle))
            return TryParseRectangle(trimmed, out value, out error);

        if (targetType == typeof(System.Drawing.RectangleF))
            return TryParseRectangleF(trimmed, out value, out error);

        if (targetType == typeof(DesignSize))
            return TryParseDesignSize(trimmed, out value, out error);

        if (targetType == typeof(DesignPoint))
            return TryParseDesignPoint(trimmed, out value, out error);

        if (targetType == typeof(DesignBounds))
            return TryParseDesignBounds(trimmed, out value, out error);

        if (targetType == typeof(SKPoint))
            return TryParseSkPoint(trimmed, out value, out error);

        if (targetType == typeof(SKSize))
            return TryParseSkSize(trimmed, out value, out error);

        if (targetType == typeof(SKRect))
            return TryParseSkRect(trimmed, out value, out error);

        if (targetType == typeof(Padding))
            return TryParsePadding(trimmed, out value, out error);

        if (targetType == typeof(SKColor))
            return TryParseColor(trimmed, out value, out error);

        if (targetType == typeof(ModernFormsNext.Drawing.SolidColorBrush)
            || targetType == typeof(ModernFormsNext.Drawing.Brush))
        {
            if (TryParseColor(trimmed, out var colorValue, out error) && colorValue is SKColor color)
            {
                value = new ModernFormsNext.Drawing.SolidColorBrush(color);
                return true;
            }

            value = null;
            return false;
        }

        if (targetType == typeof(ModernFormsNext.Font))
            return TryParseFont(trimmed, out value, out error);

        if (targetType == typeof(ModernFormsNext.Drawing.PointCollection))
            return TryParsePointCollection(trimmed, out value, out error);

        if (typeof(ModernFormsNext.Drawing.Geometry).IsAssignableFrom(targetType))
            return TryParseGeometry(trimmed, out value, out error);

        error = $"Values of type '{targetType.Name}' are not editable yet.";
        value = null;
        return false;
    }

    public static DesignPropertyValue ToDesignPropertyValue(object? value, Type valueType)
    {
        var targetType = Nullable.GetUnderlyingType(valueType) ?? valueType;

        if (value is null)
            return DesignPropertyValue.FromNull();

        if (targetType.IsEnum)
            return DesignPropertyValue.FromEnum(targetType.FullName ?? targetType.Name, value.ToString() ?? string.Empty);

        if (value is System.Drawing.Size size)
            return DesignPropertyValue.FromStructuredObject(typeof(System.Drawing.Size).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Width"] = DesignPropertyValue.FromInt32(size.Width),
                ["Height"] = DesignPropertyValue.FromInt32(size.Height)
            });

        if (value is DesignSize designSize)
            return DesignPropertyValue.FromStructuredObject(typeof(DesignSize).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Width"] = DesignPropertyValue.FromInt32(designSize.Width),
                ["Height"] = DesignPropertyValue.FromInt32(designSize.Height)
            });

        if (value is System.Drawing.Point point)
            return DesignPropertyValue.FromStructuredObject(typeof(System.Drawing.Point).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["X"] = DesignPropertyValue.FromInt32(point.X),
                ["Y"] = DesignPropertyValue.FromInt32(point.Y)
            });

        if (value is System.Drawing.PointF pointF)
            return DesignPropertyValue.FromStructuredObject(typeof(System.Drawing.PointF).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["X"] = DesignPropertyValue.FromDouble(pointF.X),
                ["Y"] = DesignPropertyValue.FromDouble(pointF.Y)
            });

        if (value is Matrix3x2 matrix)
            return DesignPropertyValue.FromStructuredObject(typeof(Matrix3x2).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["M11"] = DesignPropertyValue.FromDouble(matrix.M11),
                ["M12"] = DesignPropertyValue.FromDouble(matrix.M12),
                ["M21"] = DesignPropertyValue.FromDouble(matrix.M21),
                ["M22"] = DesignPropertyValue.FromDouble(matrix.M22),
                ["M31"] = DesignPropertyValue.FromDouble(matrix.M31),
                ["M32"] = DesignPropertyValue.FromDouble(matrix.M32)
            });

        if (value is DesignPoint designPoint)
            return DesignPropertyValue.FromStructuredObject(typeof(DesignPoint).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["X"] = DesignPropertyValue.FromInt32(designPoint.X),
                ["Y"] = DesignPropertyValue.FromInt32(designPoint.Y)
            });

        if (value is System.Drawing.Rectangle rectangle)
            return DesignPropertyValue.FromStructuredObject(typeof(System.Drawing.Rectangle).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["X"] = DesignPropertyValue.FromInt32(rectangle.X),
                ["Y"] = DesignPropertyValue.FromInt32(rectangle.Y),
                ["Width"] = DesignPropertyValue.FromInt32(rectangle.Width),
                ["Height"] = DesignPropertyValue.FromInt32(rectangle.Height)
            });

        if (value is System.Drawing.RectangleF rectangleF)
            return DesignPropertyValue.FromStructuredObject(typeof(System.Drawing.RectangleF).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["X"] = DesignPropertyValue.FromDouble(rectangleF.X),
                ["Y"] = DesignPropertyValue.FromDouble(rectangleF.Y),
                ["Width"] = DesignPropertyValue.FromDouble(rectangleF.Width),
                ["Height"] = DesignPropertyValue.FromDouble(rectangleF.Height)
            });

        if (value is DesignBounds designBounds)
            return DesignPropertyValue.FromStructuredObject(typeof(DesignBounds).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["X"] = DesignPropertyValue.FromInt32(designBounds.X),
                ["Y"] = DesignPropertyValue.FromInt32(designBounds.Y),
                ["Width"] = DesignPropertyValue.FromInt32(designBounds.Width),
                ["Height"] = DesignPropertyValue.FromInt32(designBounds.Height)
            });

        if (value is SKPoint skPoint)
            return DesignPropertyValue.FromStructuredObject(typeof(SKPoint).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["X"] = DesignPropertyValue.FromDouble(skPoint.X),
                ["Y"] = DesignPropertyValue.FromDouble(skPoint.Y)
            });

        if (value is SKSize skSize)
            return DesignPropertyValue.FromStructuredObject(typeof(SKSize).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Width"] = DesignPropertyValue.FromDouble(skSize.Width),
                ["Height"] = DesignPropertyValue.FromDouble(skSize.Height)
            });

        if (value is SKRect skRect)
            return DesignPropertyValue.FromStructuredObject(typeof(SKRect).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Left"] = DesignPropertyValue.FromDouble(skRect.Left),
                ["Top"] = DesignPropertyValue.FromDouble(skRect.Top),
                ["Right"] = DesignPropertyValue.FromDouble(skRect.Right),
                ["Bottom"] = DesignPropertyValue.FromDouble(skRect.Bottom)
            });

        if (value is Padding padding)
            return DesignPropertyValue.FromStructuredObject(typeof(Padding).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Left"] = DesignPropertyValue.FromInt32(padding.Left),
                ["Top"] = DesignPropertyValue.FromInt32(padding.Top),
                ["Right"] = DesignPropertyValue.FromInt32(padding.Right),
                ["Bottom"] = DesignPropertyValue.FromInt32(padding.Bottom)
            });

        if (value is SKColor color)
            return ToColorPropertyValue(color);

        if (value is ModernFormsNext.Drawing.SolidColorBrush solidBrush)
        {
            var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Color"] = ToColorPropertyValue(solidBrush.Color)
            };
            AddBrushProperties(properties, solidBrush);
            return DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.SolidColorBrush).FullName!, properties);
        }

        if (value is ModernFormsNext.Drawing.GlassBrush glassBrush)
            return ToGlassBrushPropertyValue(glassBrush);

        if (value is ModernFormsNext.Drawing.LinearGradientBrush linearGradientBrush)
            return ToLinearGradientBrushPropertyValue(linearGradientBrush);

        if (value is ModernFormsNext.Drawing.RadialGradientBrush radialGradientBrush)
            return ToRadialGradientBrushPropertyValue(radialGradientBrush);

        if (value is ModernFormsNext.Drawing.SweepGradientBrush sweepGradientBrush)
            return ToSweepGradientBrushPropertyValue(sweepGradientBrush);

        if (value is ModernFormsNext.Drawing.PointCollection points)
            return ToPointCollectionPropertyValue(points);

        if (value is ModernFormsNext.Drawing.Geometry geometry)
            return ToGeometryPropertyValue(geometry);

        if (value is ModernFormsNext.Font font)
            return ToFontPropertyValue(font);

        return DesignPropertyValue.FromObject(value);
    }

    public static object? FromDesignPropertyValue(DesignPropertyValue value, Type valueType)
    {
        ArgumentNullException.ThrowIfNull(value);

        var targetType = Nullable.GetUnderlyingType(valueType) ?? valueType;

        return value.Kind switch
        {
            DesignPropertyValueKind.Null => null,
            DesignPropertyValueKind.String => value.Value?.ToString() ?? string.Empty,
            DesignPropertyValueKind.Boolean => Convert.ToBoolean(value.Value, CultureInfo.InvariantCulture),
            DesignPropertyValueKind.Int32 => Convert.ToInt32(value.Value, CultureInfo.InvariantCulture),
            DesignPropertyValueKind.Double when targetType == typeof(float) => Convert.ToSingle(value.Value, CultureInfo.InvariantCulture),
            DesignPropertyValueKind.Double => Convert.ToDouble(value.Value, CultureInfo.InvariantCulture),
            DesignPropertyValueKind.Enum when targetType.IsEnum && value.Value is string memberName => Enum.Parse(targetType, memberName),
            DesignPropertyValueKind.Enum => value.Value?.ToString(),
            DesignPropertyValueKind.Object => FromStructuredValue(value, targetType),
            _ => value.Value
        };
    }

    public static DesignPropertyValue ToColorPropertyValue(SKColor color)
        => DesignPropertyValue.FromStructuredObject(typeof(SKColor).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["Hex"] = DesignPropertyValue.FromString(ToHex(color)),
            ["A"] = DesignPropertyValue.FromInt32(color.Alpha),
            ["R"] = DesignPropertyValue.FromInt32(color.Red),
            ["G"] = DesignPropertyValue.FromInt32(color.Green),
            ["B"] = DesignPropertyValue.FromInt32(color.Blue)
        });

    public static DesignPropertyValue ToFontPropertyValue(ModernFormsNext.Font font)
        => DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Font).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["Name"] = DesignPropertyValue.FromString(font.FamilyName),
            ["Size"] = DesignPropertyValue.FromDouble(font.SizeInPoints),
            ["Unit"] = DesignPropertyValue.FromString("Point"),
            ["Bold"] = DesignPropertyValue.FromBoolean(font.Bold),
            ["Italic"] = DesignPropertyValue.FromBoolean(font.Italic),
            ["Underline"] = DesignPropertyValue.FromBoolean(font.Underline),
            ["Strikeout"] = DesignPropertyValue.FromBoolean(font.Strikeout)
        });

    private static DesignPropertyValue ToGlassBrushPropertyValue(ModernFormsNext.Drawing.GlassBrush brush)
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["TintColor"] = ToColorPropertyValue(brush.TintColor),
            ["SecondaryTintColor"] = ToColorPropertyValue(brush.SecondaryTintColor),
            ["HighlightColor"] = ToColorPropertyValue(brush.HighlightColor),
            ["BorderColor"] = ToColorPropertyValue(brush.BorderColor),
            ["ShowHighlight"] = DesignPropertyValue.FromBoolean(brush.ShowHighlight),
            ["ShowInnerBorder"] = DesignPropertyValue.FromBoolean(brush.ShowInnerBorder)
        };
        AddBrushProperties(properties, brush);
        return DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.GlassBrush).FullName!, properties);
    }

    private static DesignPropertyValue ToLinearGradientBrushPropertyValue(ModernFormsNext.Drawing.LinearGradientBrush brush)
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["StartPoint"] = ToDesignPropertyValue(brush.StartPoint, typeof(SKPoint)),
            ["EndPoint"] = ToDesignPropertyValue(brush.EndPoint, typeof(SKPoint))
        };

        AddGradientProperties(properties, brush);
        return DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.LinearGradientBrush).FullName!, properties);
    }

    private static DesignPropertyValue ToRadialGradientBrushPropertyValue(ModernFormsNext.Drawing.RadialGradientBrush brush)
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["Center"] = ToDesignPropertyValue(brush.Center, typeof(SKPoint)),
            ["GradientOrigin"] = ToDesignPropertyValue(brush.GradientOrigin, typeof(System.Drawing.PointF)),
            ["Radius"] = DesignPropertyValue.FromDouble(brush.Radius)
        };

        AddGradientProperties(properties, brush);
        return DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.RadialGradientBrush).FullName!, properties);
    }

    private static DesignPropertyValue ToSweepGradientBrushPropertyValue(ModernFormsNext.Drawing.SweepGradientBrush brush)
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["Center"] = ToDesignPropertyValue(brush.Center, typeof(SKPoint)),
            ["StartAngle"] = DesignPropertyValue.FromDouble(brush.StartAngle),
            ["EndAngle"] = DesignPropertyValue.FromDouble(brush.EndAngle)
        };

        AddGradientProperties(properties, brush);
        return DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.SweepGradientBrush).FullName!, properties);
    }

    private static void AddBrushProperties(
        SortedDictionary<string, DesignPropertyValue> properties,
        ModernFormsNext.Drawing.Brush brush)
    {
        properties["Opacity"] = DesignPropertyValue.FromDouble(brush.Opacity);
        properties["Transform"] = ToDesignPropertyValue(brush.Transform, typeof(Matrix3x2));
    }

    private static void AddGradientProperties(
        SortedDictionary<string, DesignPropertyValue> properties,
        ModernFormsNext.Drawing.GradientBrush brush)
    {
        AddBrushProperties(properties, brush);
        properties["SpreadMode"] = DesignPropertyValue.FromEnum(
            typeof(ModernFormsNext.Drawing.GradientSpreadMode).FullName!,
            brush.SpreadMode.ToString());
        AddGradientStops(properties, brush);
    }

    private static void AddGradientStops(
        SortedDictionary<string, DesignPropertyValue> properties,
        ModernFormsNext.Drawing.GradientBrush brush)
    {
        properties["GradientStopCount"] = DesignPropertyValue.FromInt32(brush.GradientStops.Count);

        for (var index = 0; index < brush.GradientStops.Count; index++)
        {
            var stop = brush.GradientStops[index];
            properties[$"GradientStop{index}"] = DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.GradientStop).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Color"] = ToColorPropertyValue(stop.Color),
                ["Offset"] = DesignPropertyValue.FromDouble(stop.Offset)
            });
        }
    }

    private static DesignPropertyValue ToPointCollectionPropertyValue(ModernFormsNext.Drawing.PointCollection points)
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["Count"] = DesignPropertyValue.FromInt32(points.Count)
        };
        for (int index = 0; index < points.Count; index++)
            properties[$"Point{index}"] = ToDesignPropertyValue(points[index], typeof(System.Drawing.PointF));
        return DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.PointCollection).FullName!, properties);
    }

    private static DesignPropertyValue ToGeometryPropertyValue(ModernFormsNext.Drawing.Geometry geometry)
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["Transform"] = ToDesignPropertyValue(geometry.Transform, typeof(Matrix3x2))
        };

        switch (geometry)
        {
            case ModernFormsNext.Drawing.LineGeometry line:
                properties["StartPoint"] = ToDesignPropertyValue(line.StartPoint, typeof(System.Drawing.PointF));
                properties["EndPoint"] = ToDesignPropertyValue(line.EndPoint, typeof(System.Drawing.PointF));
                break;
            case ModernFormsNext.Drawing.RectangleGeometry rectangle:
                properties["Rect"] = ToDesignPropertyValue(rectangle.Rect, typeof(System.Drawing.RectangleF));
                break;
            case ModernFormsNext.Drawing.EllipseGeometry ellipse:
                properties["Rect"] = ToDesignPropertyValue(ellipse.Rect, typeof(System.Drawing.RectangleF));
                break;
            case ModernFormsNext.Drawing.PathGeometry path:
                properties["FillRule"] = DesignPropertyValue.FromEnum(
                    typeof(ModernFormsNext.Drawing.GeometryFillRule).FullName!,
                    path.FillRule.ToString());
                properties["FigureCount"] = DesignPropertyValue.FromInt32(path.Figures.Count);
                for (int index = 0; index < path.Figures.Count; index++)
                    properties[$"Figure{index}"] = ToPathFigurePropertyValue(path.Figures[index]);
                break;
        }

        return DesignPropertyValue.FromStructuredObject(geometry.GetType().FullName!, properties);
    }

    private static DesignPropertyValue ToPathFigurePropertyValue(ModernFormsNext.Drawing.PathFigure figure)
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["StartPoint"] = ToDesignPropertyValue(figure.StartPoint, typeof(System.Drawing.PointF)),
            ["IsClosed"] = DesignPropertyValue.FromBoolean(figure.IsClosed),
            ["SegmentCount"] = DesignPropertyValue.FromInt32(figure.Segments.Count)
        };
        for (int index = 0; index < figure.Segments.Count; index++)
            properties[$"Segment{index}"] = ToPathSegmentPropertyValue(figure.Segments[index]);
        return DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.PathFigure).FullName!, properties);
    }

    private static DesignPropertyValue ToPathSegmentPropertyValue(ModernFormsNext.Drawing.PathSegment segment)
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        switch (segment)
        {
            case ModernFormsNext.Drawing.LineSegment line:
                properties["Point"] = ToDesignPropertyValue(line.Point, typeof(System.Drawing.PointF));
                break;
            case ModernFormsNext.Drawing.QuadraticBezierSegment quadratic:
                properties["ControlPoint"] = ToDesignPropertyValue(quadratic.ControlPoint, typeof(System.Drawing.PointF));
                properties["Point"] = ToDesignPropertyValue(quadratic.Point, typeof(System.Drawing.PointF));
                break;
            case ModernFormsNext.Drawing.BezierSegment cubic:
                properties["ControlPoint1"] = ToDesignPropertyValue(cubic.ControlPoint1, typeof(System.Drawing.PointF));
                properties["ControlPoint2"] = ToDesignPropertyValue(cubic.ControlPoint2, typeof(System.Drawing.PointF));
                properties["Point"] = ToDesignPropertyValue(cubic.Point, typeof(System.Drawing.PointF));
                break;
        }
        return DesignPropertyValue.FromStructuredObject(segment.GetType().FullName!, properties);
    }

    public static string ToHex(SKColor color)
        => color.Alpha == byte.MaxValue
            ? $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
            : $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

    private static object? FromStructuredValue(DesignPropertyValue value, Type targetType)
    {
        var properties = value.ObjectProperties ?? new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);

        if (targetType == typeof(System.Drawing.Size))
            return new System.Drawing.Size(ReadInt(properties, "Width"), ReadInt(properties, "Height"));

        if (targetType == typeof(DesignSize))
            return new DesignSize(ReadInt(properties, "Width"), ReadInt(properties, "Height"));

        if (targetType == typeof(System.Drawing.Point))
            return new System.Drawing.Point(ReadInt(properties, "X"), ReadInt(properties, "Y"));

        if (targetType == typeof(System.Drawing.PointF))
            return new System.Drawing.PointF((float)ReadDouble(properties, "X"), (float)ReadDouble(properties, "Y"));

        if (targetType == typeof(System.Drawing.RectangleF))
            return new System.Drawing.RectangleF(
                (float)ReadDouble(properties, "X"),
                (float)ReadDouble(properties, "Y"),
                (float)ReadDouble(properties, "Width"),
                (float)ReadDouble(properties, "Height"));

        if (targetType == typeof(Matrix3x2))
            return new Matrix3x2(
                (float)ReadDouble(properties, "M11", 1),
                (float)ReadDouble(properties, "M12"),
                (float)ReadDouble(properties, "M21"),
                (float)ReadDouble(properties, "M22", 1),
                (float)ReadDouble(properties, "M31"),
                (float)ReadDouble(properties, "M32"));

        if (targetType == typeof(DesignPoint))
            return new DesignPoint(ReadInt(properties, "X"), ReadInt(properties, "Y"));

        if (targetType == typeof(System.Drawing.Rectangle))
            return new System.Drawing.Rectangle(ReadInt(properties, "X"), ReadInt(properties, "Y"), ReadInt(properties, "Width"), ReadInt(properties, "Height"));

        if (targetType == typeof(DesignBounds))
            return new DesignBounds(ReadInt(properties, "X"), ReadInt(properties, "Y"), ReadInt(properties, "Width"), ReadInt(properties, "Height"));

        if (targetType == typeof(SKPoint))
            return new SKPoint((float)ReadDouble(properties, "X"), (float)ReadDouble(properties, "Y"));

        if (targetType == typeof(SKSize))
            return new SKSize((float)ReadDouble(properties, "Width"), (float)ReadDouble(properties, "Height"));

        if (targetType == typeof(SKRect))
            return new SKRect((float)ReadDouble(properties, "Left"), (float)ReadDouble(properties, "Top"), (float)ReadDouble(properties, "Right"), (float)ReadDouble(properties, "Bottom"));

        if (targetType == typeof(Padding))
            return new Padding(ReadInt(properties, "Left"), ReadInt(properties, "Top"), ReadInt(properties, "Right"), ReadInt(properties, "Bottom"));

        if (targetType == typeof(SKColor))
            return new SKColor((byte)ReadInt(properties, "R"), (byte)ReadInt(properties, "G"), (byte)ReadInt(properties, "B"), (byte)ReadInt(properties, "A", 255));

        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.SolidColorBrush))
            || targetType == typeof(ModernFormsNext.Drawing.SolidColorBrush))
        {
            var color = properties.TryGetValue("Color", out var colorValue)
                ? FromDesignPropertyValue(colorValue, typeof(SKColor))
                : new SKColor(255, 255, 255);

            var brush = new ModernFormsNext.Drawing.SolidColorBrush(color is SKColor skColor ? skColor : new SKColor(255, 255, 255));
            ApplyBrushProperties(properties, brush);
            return brush;
        }

        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.GlassBrush)))
        {
            var brush = new ModernFormsNext.Drawing.GlassBrush
            {
                TintColor = ReadColor(properties, "TintColor", new SKColor(255, 255, 255, 28)),
                SecondaryTintColor = ReadColor(properties, "SecondaryTintColor", new SKColor(255, 255, 255, 12)),
                HighlightColor = ReadColor(properties, "HighlightColor", new SKColor(255, 255, 255, 38)),
                BorderColor = ReadColor(properties, "BorderColor", new SKColor(255, 255, 255, 65)),
                ShowHighlight = ReadBool(properties, "ShowHighlight", fallback: true),
                ShowInnerBorder = ReadBool(properties, "ShowInnerBorder", fallback: true)
            };
            ApplyBrushProperties(properties, brush);
            return brush;
        }

        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.LinearGradientBrush)))
        {
            var brush = new ModernFormsNext.Drawing.LinearGradientBrush
            {
                StartPoint = ReadSkPoint(properties, "StartPoint", new SKPoint(0f, 0f)),
                EndPoint = ReadSkPoint(properties, "EndPoint", new SKPoint(1f, 1f))
            };

            ApplyGradientProperties(properties, brush);
            ReadGradientStops(properties, brush);
            return brush;
        }

        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.RadialGradientBrush)))
        {
            var brush = new ModernFormsNext.Drawing.RadialGradientBrush();
            brush.Center = ReadSkPoint(properties, "Center", new SKPoint(0.5f, 0.5f));
            if (properties.TryGetValue("GradientOrigin", out DesignPropertyValue? originValue) &&
                FromDesignPropertyValue(originValue, typeof(System.Drawing.PointF)) is System.Drawing.PointF origin)
            {
                brush.GradientOrigin = origin;
            }
            brush.Radius = (float)ReadDouble(properties, "Radius", 0.5);

            ApplyGradientProperties(properties, brush);
            ReadGradientStops(properties, brush);
            return brush;
        }

        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.SweepGradientBrush)))
        {
            var brush = new ModernFormsNext.Drawing.SweepGradientBrush
            {
                Center = ReadSkPoint(properties, "Center", new SKPoint(0.5f, 0.5f)),
                StartAngle = (float)ReadDouble(properties, "StartAngle"),
                EndAngle = (float)ReadDouble(properties, "EndAngle", 360)
            };

            ApplyGradientProperties(properties, brush);
            ReadGradientStops(properties, brush);
            return brush;
        }

        if (targetType == typeof(ModernFormsNext.Font))
        {
            var style = FontStyle.Regular;

            if (ReadBool(properties, "Bold"))
                style |= FontStyle.Bold;
            if (ReadBool(properties, "Italic"))
                style |= FontStyle.Italic;
            if (ReadBool(properties, "Underline"))
                style |= FontStyle.Underline;
            if (ReadBool(properties, "Strikeout"))
                style |= FontStyle.Strikeout;

            return new ModernFormsNext.Font(ReadString(properties, "Name", "Segoe UI"), (float)ReadDouble(properties, "Size", 9), style);
        }

        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.PointCollection))
            || targetType == typeof(ModernFormsNext.Drawing.PointCollection))
        {
            var result = new ModernFormsNext.Drawing.PointCollection();
            int count = ReadInt(properties, "Count");
            for (int index = 0; index < count; index++)
            {
                if (properties.TryGetValue($"Point{index}", out DesignPropertyValue? pointValue)
                    && FromDesignPropertyValue(pointValue, typeof(System.Drawing.PointF)) is System.Drawing.PointF point)
                {
                    result.Add(point);
                }
            }
            return result;
        }

        if (typeof(ModernFormsNext.Drawing.Geometry).IsAssignableFrom(targetType)
            || IsGeometryTypeName(value.ObjectTypeName))
        {
            return FromGeometryPropertyValue(value, properties);
        }

        return value;
    }

    private static ModernFormsNext.Drawing.Geometry FromGeometryPropertyValue(
        DesignPropertyValue value,
        IReadOnlyDictionary<string, DesignPropertyValue> properties)
    {
        ModernFormsNext.Drawing.Geometry geometry;
        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.LineGeometry)))
        {
            geometry = new ModernFormsNext.Drawing.LineGeometry(
                ReadPointF(properties, "StartPoint"),
                ReadPointF(properties, "EndPoint"));
        }
        else if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.RectangleGeometry)))
        {
            geometry = new ModernFormsNext.Drawing.RectangleGeometry(ReadRectangleF(properties, "Rect"));
        }
        else if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.EllipseGeometry)))
        {
            geometry = new ModernFormsNext.Drawing.EllipseGeometry(ReadRectangleF(properties, "Rect"));
        }
        else
        {
            var path = new ModernFormsNext.Drawing.PathGeometry();
            if (properties.TryGetValue("FillRule", out DesignPropertyValue? fillRuleValue)
                && FromDesignPropertyValue(fillRuleValue, typeof(ModernFormsNext.Drawing.GeometryFillRule)) is ModernFormsNext.Drawing.GeometryFillRule fillRule)
            {
                path.FillRule = fillRule;
            }

            int count = ReadInt(properties, "FigureCount");
            for (int index = 0; index < count; index++)
            {
                if (properties.TryGetValue($"Figure{index}", out DesignPropertyValue? figureValue))
                    path.Figures.Add(FromPathFigurePropertyValue(figureValue));
            }
            geometry = path;
        }

        if (properties.TryGetValue("Transform", out DesignPropertyValue? transformValue)
            && FromDesignPropertyValue(transformValue, typeof(Matrix3x2)) is Matrix3x2 transform)
        {
            geometry.Transform = transform;
        }
        return geometry;
    }

    private static ModernFormsNext.Drawing.PathFigure FromPathFigurePropertyValue(DesignPropertyValue value)
    {
        IReadOnlyDictionary<string, DesignPropertyValue> properties = value.ObjectProperties
            ?? new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        var figure = new ModernFormsNext.Drawing.PathFigure(
            ReadPointF(properties, "StartPoint"),
            ReadBool(properties, "IsClosed"));
        int count = ReadInt(properties, "SegmentCount");
        for (int index = 0; index < count; index++)
        {
            if (properties.TryGetValue($"Segment{index}", out DesignPropertyValue? segmentValue)
                && FromPathSegmentPropertyValue(segmentValue) is { } segment)
            {
                figure.Segments.Add(segment);
            }
        }
        return figure;
    }

    private static ModernFormsNext.Drawing.PathSegment? FromPathSegmentPropertyValue(DesignPropertyValue value)
    {
        IReadOnlyDictionary<string, DesignPropertyValue> properties = value.ObjectProperties
            ?? new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.LineSegment)))
            return new ModernFormsNext.Drawing.LineSegment(ReadPointF(properties, "Point"));
        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.QuadraticBezierSegment)))
        {
            return new ModernFormsNext.Drawing.QuadraticBezierSegment(
                ReadPointF(properties, "ControlPoint"),
                ReadPointF(properties, "Point"));
        }
        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.BezierSegment)))
        {
            return new ModernFormsNext.Drawing.BezierSegment(
                ReadPointF(properties, "ControlPoint1"),
                ReadPointF(properties, "ControlPoint2"),
                ReadPointF(properties, "Point"));
        }
        return null;
    }

    private static string WritePointCollectionText(ModernFormsNext.Drawing.PointCollection points)
        => string.Join("; ", points.Select(FormatPoint));

    private static string WriteGeometryText(ModernFormsNext.Drawing.Geometry geometry)
    {
        string transform = geometry.Transform == Matrix3x2.Identity
            ? string.Empty
            : $"transform({FormatMatrix(geometry.Transform)}) ";

        return geometry switch
        {
            ModernFormsNext.Drawing.LineGeometry line =>
                $"{transform}line {FormatPoint(line.StartPoint)} {FormatPoint(line.EndPoint)}",
            ModernFormsNext.Drawing.RectangleGeometry rectangle =>
                $"{transform}rectangle {FormatRectangle(rectangle.Rect)}",
            ModernFormsNext.Drawing.EllipseGeometry ellipse =>
                $"{transform}ellipse {FormatRectangle(ellipse.Rect)}",
            ModernFormsNext.Drawing.PathGeometry path =>
                $"{transform}path {path.FillRule.ToString().ToLowerInvariant()} {WritePathFigures(path)}".TrimEnd(),
            _ => geometry.GetType().Name
        };
    }

    private static string WritePathFigures(ModernFormsNext.Drawing.PathGeometry path)
    {
        var parts = new List<string>();
        foreach (ModernFormsNext.Drawing.PathFigure figure in path.Figures)
        {
            parts.Add("M");
            parts.Add(FormatPoint(figure.StartPoint));
            foreach (ModernFormsNext.Drawing.PathSegment segment in figure.Segments)
            {
                switch (segment)
                {
                    case ModernFormsNext.Drawing.LineSegment line:
                        parts.Add("L");
                        parts.Add(FormatPoint(line.Point));
                        break;
                    case ModernFormsNext.Drawing.QuadraticBezierSegment quadratic:
                        parts.Add("Q");
                        parts.Add(FormatPoint(quadratic.ControlPoint));
                        parts.Add(FormatPoint(quadratic.Point));
                        break;
                    case ModernFormsNext.Drawing.BezierSegment cubic:
                        parts.Add("C");
                        parts.Add(FormatPoint(cubic.ControlPoint1));
                        parts.Add(FormatPoint(cubic.ControlPoint2));
                        parts.Add(FormatPoint(cubic.Point));
                        break;
                }
            }

            if (figure.IsClosed)
                parts.Add("Z");
        }
        return string.Join(' ', parts);
    }

    private static string FormatPoint(System.Drawing.PointF point)
        => $"{point.X.ToString("R", CultureInfo.InvariantCulture)},{point.Y.ToString("R", CultureInfo.InvariantCulture)}";

    private static string FormatRectangle(System.Drawing.RectangleF rectangle)
        => $"{rectangle.X.ToString("R", CultureInfo.InvariantCulture)},{rectangle.Y.ToString("R", CultureInfo.InvariantCulture)},{rectangle.Width.ToString("R", CultureInfo.InvariantCulture)},{rectangle.Height.ToString("R", CultureInfo.InvariantCulture)}";

    private static string FormatMatrix(Matrix3x2 matrix)
        => string.Join(',', new[] { matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32 }
            .Select(value => value.ToString("R", CultureInfo.InvariantCulture)));

    private static bool TryParsePointF(string text, out object? value, out string? error)
    {
        if (TryReadFloatList(text, 2, out float[] parts, out error))
        {
            value = new System.Drawing.PointF(parts[0], parts[1]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseRectangleF(string text, out object? value, out string? error)
    {
        if (TryReadFloatList(text, 4, out float[] parts, out error))
        {
            if (parts[2] < 0 || parts[3] < 0)
            {
                value = null;
                error = "Rectangle width and height cannot be negative.";
                return false;
            }

            value = new System.Drawing.RectangleF(parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParsePointCollection(string text, out object? value, out string? error)
    {
        var points = new ModernFormsNext.Drawing.PointCollection();
        if (string.IsNullOrWhiteSpace(text))
        {
            value = points;
            error = null;
            return true;
        }

        foreach (string token in text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParsePointF(token, out object? pointValue, out error)
                || pointValue is not System.Drawing.PointF point)
            {
                value = null;
                error = "Expected points as 'x,y; x,y; ...'.";
                return false;
            }
            points.Add(point);
        }

        value = points;
        error = null;
        return true;
    }

    private static bool TryParseGeometry(string text, out object? value, out string? error)
    {
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
        {
            value = null;
            error = null;
            return true;
        }

        string[] tokens = text.Split((char[]?)null, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        int index = 0;
        Matrix3x2 transform = Matrix3x2.Identity;
        if (tokens[index].StartsWith("transform(", StringComparison.OrdinalIgnoreCase))
        {
            string token = tokens[index++];
            if (!token.EndsWith(')'))
                return FailGeometry("Expected transform(m11,m12,m21,m22,m31,m32).", out value, out error);
            if (!TryReadFloatList(token[10..^1], 6, out float[] matrix, out _))
                return FailGeometry("Expected six finite transform components.", out value, out error);
            transform = new Matrix3x2(matrix[0], matrix[1], matrix[2], matrix[3], matrix[4], matrix[5]);
        }

        if (index >= tokens.Length)
            return FailGeometry("Expected line, rectangle, ellipse, or path geometry.", out value, out error);

        string kind = tokens[index++].ToLowerInvariant();
        ModernFormsNext.Drawing.Geometry? geometry;
        switch (kind)
        {
            case "line":
                if (!TryReadPointToken(tokens, ref index, out System.Drawing.PointF start)
                    || !TryReadPointToken(tokens, ref index, out System.Drawing.PointF end)
                    || index != tokens.Length)
                {
                    return FailGeometry("Expected 'line x1,y1 x2,y2'.", out value, out error);
                }
                geometry = new ModernFormsNext.Drawing.LineGeometry(start, end);
                break;
            case "rectangle":
            case "ellipse":
                if (index >= tokens.Length
                    || !TryParseRectangleF(tokens[index++], out object? rectangleValue, out _)
                    || rectangleValue is not System.Drawing.RectangleF rectangle
                    || index != tokens.Length)
                {
                    return FailGeometry($"Expected '{kind} x,y,width,height'.", out value, out error);
                }
                geometry = kind == "rectangle"
                    ? new ModernFormsNext.Drawing.RectangleGeometry(rectangle)
                    : new ModernFormsNext.Drawing.EllipseGeometry(rectangle);
                break;
            case "path":
                geometry = ParsePathGeometry(tokens, ref index, out error);
                if (geometry is null)
                {
                    value = null;
                    return false;
                }
                break;
            default:
                return FailGeometry("Expected line, rectangle, ellipse, or path geometry.", out value, out error);
        }

        geometry.Transform = transform;
        value = geometry;
        error = null;
        return true;
    }

    private static ModernFormsNext.Drawing.PathGeometry? ParsePathGeometry(
        string[] tokens,
        ref int index,
        out string? error)
    {
        var path = new ModernFormsNext.Drawing.PathGeometry();
        if (index < tokens.Length
            && Enum.TryParse(tokens[index], ignoreCase: true, out ModernFormsNext.Drawing.GeometryFillRule fillRule))
        {
            path.FillRule = fillRule;
            index++;
        }

        ModernFormsNext.Drawing.PathFigure? figure = null;
        while (index < tokens.Length)
        {
            string command = tokens[index++].ToUpperInvariant();
            switch (command)
            {
                case "M":
                    if (!TryReadPointToken(tokens, ref index, out System.Drawing.PointF start))
                        return FailPath("Command M requires one point.", out error);
                    figure = new ModernFormsNext.Drawing.PathFigure(start);
                    path.Figures.Add(figure);
                    break;
                case "L" when figure is not null:
                    if (!TryReadPointToken(tokens, ref index, out System.Drawing.PointF linePoint))
                        return FailPath("Command L requires one point.", out error);
                    figure.Segments.Add(new ModernFormsNext.Drawing.LineSegment(linePoint));
                    break;
                case "Q" when figure is not null:
                    if (!TryReadPointToken(tokens, ref index, out System.Drawing.PointF control)
                        || !TryReadPointToken(tokens, ref index, out System.Drawing.PointF quadraticPoint))
                        return FailPath("Command Q requires a control point and an end point.", out error);
                    figure.Segments.Add(new ModernFormsNext.Drawing.QuadraticBezierSegment(control, quadraticPoint));
                    break;
                case "C" when figure is not null:
                    if (!TryReadPointToken(tokens, ref index, out System.Drawing.PointF control1)
                        || !TryReadPointToken(tokens, ref index, out System.Drawing.PointF control2)
                        || !TryReadPointToken(tokens, ref index, out System.Drawing.PointF cubicPoint))
                        return FailPath("Command C requires two control points and an end point.", out error);
                    figure.Segments.Add(new ModernFormsNext.Drawing.BezierSegment(control1, control2, cubicPoint));
                    break;
                case "Z" when figure is not null:
                    figure.IsClosed = true;
                    break;
                default:
                    return FailPath("Path commands must start with M and use M, L, Q, C, or Z.", out error);
            }
        }

        error = null;
        return path;
    }

    private static bool TryReadPointToken(string[] tokens, ref int index, out System.Drawing.PointF point)
    {
        point = default;
        if (index >= tokens.Length
            || !TryParsePointF(tokens[index++], out object? pointValue, out _)
            || pointValue is not System.Drawing.PointF parsed)
        {
            return false;
        }
        point = parsed;
        return true;
    }

    private static bool FailGeometry(string message, out object? value, out string? error)
    {
        value = null;
        error = message;
        return false;
    }

    private static ModernFormsNext.Drawing.PathGeometry? FailPath(string message, out string? error)
    {
        error = message;
        return null;
    }

    private static bool TryParseSize(string text, out object? value, out string? error)
    {
        if (TryReadIntList(text, 2, out var parts, out error))
        {
            if (parts[0] < 0 || parts[1] < 0)
            {
                value = null;
                error = "Size values cannot be negative.";
                return false;
            }

            value = new System.Drawing.Size(parts[0], parts[1]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParsePoint(string text, out object? value, out string? error)
    {
        if (TryReadIntList(text, 2, out var parts, out error))
        {
            value = new System.Drawing.Point(parts[0], parts[1]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseRectangle(string text, out object? value, out string? error)
    {
        if (TryReadIntList(text, 4, out var parts, out error))
        {
            if (parts[2] < 0 || parts[3] < 0)
            {
                value = null;
                error = "Rectangle width and height cannot be negative.";
                return false;
            }

            value = new System.Drawing.Rectangle(parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseDesignSize(string text, out object? value, out string? error)
    {
        if (TryReadIntList(text, 2, out var parts, out error))
        {
            if (parts[0] < 0 || parts[1] < 0)
            {
                value = null;
                error = "Size values cannot be negative.";
                return false;
            }

            value = new DesignSize(parts[0], parts[1]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseDesignPoint(string text, out object? value, out string? error)
    {
        if (TryReadIntList(text, 2, out var parts, out error))
        {
            value = new DesignPoint(parts[0], parts[1]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseDesignBounds(string text, out object? value, out string? error)
    {
        if (TryReadIntList(text, 4, out var parts, out error))
        {
            if (parts[2] < 0 || parts[3] < 0)
            {
                value = null;
                error = "Bounds width and height cannot be negative.";
                return false;
            }

            value = new DesignBounds(parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParsePadding(string text, out object? value, out string? error)
    {
        if (TryReadIntList(text, 4, out var parts, out error))
        {
            value = new Padding(parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseSkPoint(string text, out object? value, out string? error)
    {
        if (TryReadFloatList(text, 2, out var parts, out error))
        {
            value = new SKPoint(parts[0], parts[1]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseSkSize(string text, out object? value, out string? error)
    {
        if (TryReadFloatList(text, 2, out var parts, out error))
        {
            if (parts[0] < 0 || parts[1] < 0)
            {
                value = null;
                error = "Size values cannot be negative.";
                return false;
            }

            value = new SKSize(parts[0], parts[1]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseSkRect(string text, out object? value, out string? error)
    {
        if (TryReadFloatList(text, 4, out var parts, out error))
        {
            value = new SKRect(parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseColor(string text, out object? value, out string? error)
    {
        if (TryParseHexColor(text, out var color) || TryParseRgbFunction(text, out color))
        {
            value = color;
            error = null;
            return true;
        }

        value = null;
        error = "Expected #RRGGBB, #AARRGGBB, rgb(r,g,b), or rgba(r,g,b,a).";
        return false;
    }

    private static bool TryParseFont(string text, out object? value, out string? error)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            value = null;
            error = "Expected font as 'Name, Size'.";
            return false;
        }

        var sizeText = parts[1].EndsWith("pt", StringComparison.OrdinalIgnoreCase)
            ? parts[1][..^2]
            : parts[1];

        if (!float.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) || size <= 0)
        {
            value = null;
            error = "Font size must be a positive number.";
            return false;
        }

        value = new ModernFormsNext.Font(parts[0], size);
        error = null;
        return true;
    }

    private static bool TryReadIntList(string text, int expectedCount, out int[] parts, out string? error)
    {
        var tokens = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != expectedCount)
        {
            parts = [];
            error = $"Expected {expectedCount} comma-separated integer values.";
            return false;
        }

        parts = new int[expectedCount];

        for (var i = 0; i < tokens.Length; i++)
        {
            if (!int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out parts[i]))
            {
                error = "Expected integer values.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryReadFloatList(string text, int expectedCount, out float[] parts, out string? error)
    {
        var tokens = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != expectedCount)
        {
            parts = [];
            error = $"Expected {expectedCount} comma-separated numeric values.";
            return false;
        }

        parts = new float[expectedCount];

        for (var i = 0; i < tokens.Length; i++)
        {
            if (!float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parts[i])
                || !float.IsFinite(parts[i]))
            {
                error = "Expected finite numeric values.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryParseHexColor(string text, out SKColor color)
    {
        color = default;
        var value = text.Trim();

        if (!value.StartsWith('#') || value.Length is not (7 or 9))
            return false;

        var hex = value[1..];

        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
            return false;

        if (hex.Length == 6)
        {
            color = new SKColor((byte)((number >> 16) & 0xFF), (byte)((number >> 8) & 0xFF), (byte)(number & 0xFF));
            return true;
        }

        color = new SKColor((byte)((number >> 16) & 0xFF), (byte)((number >> 8) & 0xFF), (byte)(number & 0xFF), (byte)((number >> 24) & 0xFF));
        return true;
    }

    private static bool TryParseRgbFunction(string text, out SKColor color)
    {
        color = default;
        var value = text.Trim();
        var isRgba = value.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')');
        var isRgb = value.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')');

        if (!isRgb && !isRgba)
            return false;

        var start = isRgba ? 5 : 4;
        var expected = isRgba ? 4 : 3;
        var body = value[start..^1];

        if (!TryReadIntList(body, expected, out var parts, out _))
            return false;

        if (parts.Any(part => part is < 0 or > 255))
            return false;

        color = new SKColor((byte)parts[0], (byte)parts[1], (byte)parts[2], (byte)(isRgba ? parts[3] : 255));
        return true;
    }

    private static bool IsStructuredType(DesignPropertyValue value, Type type)
        => string.Equals(value.ObjectTypeName, type.FullName, StringComparison.Ordinal)
        || string.Equals(value.ObjectTypeName, type.Name, StringComparison.Ordinal);

    private static bool IsGeometryTypeName(string? typeName)
        => typeName is not null
        && new[]
        {
            typeof(ModernFormsNext.Drawing.LineGeometry),
            typeof(ModernFormsNext.Drawing.RectangleGeometry),
            typeof(ModernFormsNext.Drawing.EllipseGeometry),
            typeof(ModernFormsNext.Drawing.PathGeometry)
        }.Any(type => string.Equals(typeName, type.FullName, StringComparison.Ordinal)
            || string.Equals(typeName, type.Name, StringComparison.Ordinal));

    private static System.Drawing.PointF ReadPointF(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        string name,
        System.Drawing.PointF fallback = default)
        => properties.TryGetValue(name, out DesignPropertyValue? value)
            && FromDesignPropertyValue(value, typeof(System.Drawing.PointF)) is System.Drawing.PointF point
                ? point
                : fallback;

    private static System.Drawing.RectangleF ReadRectangleF(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        string name,
        System.Drawing.RectangleF fallback = default)
        => properties.TryGetValue(name, out DesignPropertyValue? value)
            && FromDesignPropertyValue(value, typeof(System.Drawing.RectangleF)) is System.Drawing.RectangleF rectangle
                ? rectangle
                : fallback;

    private static SKColor ReadColor(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        string name,
        SKColor fallback)
    {
        if (!properties.TryGetValue(name, out var value))
            return fallback;

        return FromDesignPropertyValue(value, typeof(SKColor)) is SKColor color ? color : fallback;
    }

    private static SKPoint ReadSkPoint(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        string name,
        SKPoint fallback)
    {
        if (!properties.TryGetValue(name, out var value))
            return fallback;

        return FromDesignPropertyValue(value, typeof(SKPoint)) is SKPoint point ? point : fallback;
    }

    private static void ReadGradientStops(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        ModernFormsNext.Drawing.GradientBrush brush)
    {
        var count = ReadInt(properties, "GradientStopCount");

        for (var index = 0; index < count; index++)
        {
            if (!properties.TryGetValue($"GradientStop{index}", out var value)
                || value.ObjectProperties is null)
            {
                continue;
            }

            var color = ReadColor(value.ObjectProperties, "Color", SKColors.Transparent);
            var offset = (float)ReadDouble(value.ObjectProperties, "Offset");
            brush.GradientStops.Add(new ModernFormsNext.Drawing.GradientStop(color, offset));
        }
    }

    private static void ApplyBrushProperties(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        ModernFormsNext.Drawing.Brush brush)
    {
        brush.Opacity = (float)ReadDouble(properties, "Opacity", 1);
        if (properties.TryGetValue("Transform", out DesignPropertyValue? transformValue) &&
            FromDesignPropertyValue(transformValue, typeof(Matrix3x2)) is Matrix3x2 transform)
        {
            brush.Transform = transform;
        }
    }

    private static void ApplyGradientProperties(
        IReadOnlyDictionary<string, DesignPropertyValue> properties,
        ModernFormsNext.Drawing.GradientBrush brush)
    {
        ApplyBrushProperties(properties, brush);
        if (properties.TryGetValue("SpreadMode", out DesignPropertyValue? spreadValue) &&
            FromDesignPropertyValue(spreadValue, typeof(ModernFormsNext.Drawing.GradientSpreadMode)) is ModernFormsNext.Drawing.GradientSpreadMode spreadMode)
        {
            brush.SpreadMode = spreadMode;
        }
    }

    private static int ReadInt(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name, int fallback = 0)
        => properties.TryGetValue(name, out var value)
            ? Convert.ToInt32(value.Value, CultureInfo.InvariantCulture)
            : fallback;

    private static double ReadDouble(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name, double fallback = 0)
        => properties.TryGetValue(name, out var value)
            ? Convert.ToDouble(value.Value, CultureInfo.InvariantCulture)
            : fallback;

    private static bool ReadBool(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name, bool fallback = false)
        => properties.TryGetValue(name, out var value)
            ? value.Value is bool boolValue && boolValue
            : fallback;

    private static string ReadString(IReadOnlyDictionary<string, DesignPropertyValue> properties, string name, string fallback)
        => properties.TryGetValue(name, out var value)
            ? value.Value?.ToString() ?? fallback
            : fallback;
}
