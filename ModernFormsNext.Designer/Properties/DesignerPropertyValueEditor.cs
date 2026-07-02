using System.Globalization;
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
            System.Drawing.Rectangle rectangle => $"{rectangle.X}, {rectangle.Y}, {rectangle.Width}, {rectangle.Height}",
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

        if (targetType == typeof(System.Drawing.Rectangle))
            return TryParseRectangle(trimmed, out value, out error);

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
            return DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.SolidColorBrush).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Color"] = ToColorPropertyValue(solidBrush.Color)
            });
        }

        if (value is ModernFormsNext.Drawing.GlassBrush glassBrush)
            return ToGlassBrushPropertyValue(glassBrush);

        if (value is ModernFormsNext.Drawing.LinearGradientBrush linearGradientBrush)
            return ToLinearGradientBrushPropertyValue(linearGradientBrush);

        if (value is ModernFormsNext.Drawing.RadialGradientBrush radialGradientBrush)
            return ToRadialGradientBrushPropertyValue(radialGradientBrush);

        if (value is ModernFormsNext.Drawing.SweepGradientBrush sweepGradientBrush)
            return ToSweepGradientBrushPropertyValue(sweepGradientBrush);

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
        => DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.GlassBrush).FullName!, new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["TintColor"] = ToColorPropertyValue(brush.TintColor),
            ["SecondaryTintColor"] = ToColorPropertyValue(brush.SecondaryTintColor),
            ["HighlightColor"] = ToColorPropertyValue(brush.HighlightColor),
            ["BorderColor"] = ToColorPropertyValue(brush.BorderColor),
            ["ShowHighlight"] = DesignPropertyValue.FromBoolean(brush.ShowHighlight),
            ["ShowInnerBorder"] = DesignPropertyValue.FromBoolean(brush.ShowInnerBorder)
        });

    private static DesignPropertyValue ToLinearGradientBrushPropertyValue(ModernFormsNext.Drawing.LinearGradientBrush brush)
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["StartPoint"] = ToDesignPropertyValue(brush.StartPoint, typeof(SKPoint)),
            ["EndPoint"] = ToDesignPropertyValue(brush.EndPoint, typeof(SKPoint))
        };

        AddGradientStops(properties, brush);
        return DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.LinearGradientBrush).FullName!, properties);
    }

    private static DesignPropertyValue ToRadialGradientBrushPropertyValue(ModernFormsNext.Drawing.RadialGradientBrush brush)
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["Center"] = ToDesignPropertyValue(brush.Center, typeof(SKPoint)),
            ["Radius"] = DesignPropertyValue.FromDouble(brush.Radius)
        };

        AddGradientStops(properties, brush);
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

        AddGradientStops(properties, brush);
        return DesignPropertyValue.FromStructuredObject(typeof(ModernFormsNext.Drawing.SweepGradientBrush).FullName!, properties);
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

            return new ModernFormsNext.Drawing.SolidColorBrush(color is SKColor skColor ? skColor : new SKColor(255, 255, 255));
        }

        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.GlassBrush)))
        {
            return new ModernFormsNext.Drawing.GlassBrush
            {
                TintColor = ReadColor(properties, "TintColor", new SKColor(255, 255, 255, 28)),
                SecondaryTintColor = ReadColor(properties, "SecondaryTintColor", new SKColor(255, 255, 255, 12)),
                HighlightColor = ReadColor(properties, "HighlightColor", new SKColor(255, 255, 255, 38)),
                BorderColor = ReadColor(properties, "BorderColor", new SKColor(255, 255, 255, 65)),
                ShowHighlight = ReadBool(properties, "ShowHighlight", fallback: true),
                ShowInnerBorder = ReadBool(properties, "ShowInnerBorder", fallback: true)
            };
        }

        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.LinearGradientBrush)))
        {
            var brush = new ModernFormsNext.Drawing.LinearGradientBrush
            {
                StartPoint = ReadSkPoint(properties, "StartPoint", new SKPoint(0f, 0f)),
                EndPoint = ReadSkPoint(properties, "EndPoint", new SKPoint(1f, 1f))
            };

            ReadGradientStops(properties, brush);
            return brush;
        }

        if (IsStructuredType(value, typeof(ModernFormsNext.Drawing.RadialGradientBrush)))
        {
            var brush = new ModernFormsNext.Drawing.RadialGradientBrush
            {
                Center = ReadSkPoint(properties, "Center", new SKPoint(0.5f, 0.5f)),
                Radius = (float)ReadDouble(properties, "Radius", 0.5)
            };

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

        return value;
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
            if (!float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parts[i]))
            {
                error = "Expected numeric values.";
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
