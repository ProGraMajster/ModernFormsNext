using System.Drawing;
using System.Numerics;
using System.Text.Json;
using ModernFormsNext.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

public sealed partial class ThemeJsonSerializer
{
    private static readonly HashSet<string> TypographyProperties = new(StringComparer.Ordinal)
        { "fontFamily", "size", "style", "lineHeight", "letterSpacing" };
    private static readonly HashSet<string> AnimationProperties = new(StringComparer.Ordinal)
        { "durationMs", "easing", "enabled" };
    private static readonly HashSet<string> ResourceProperties = new(StringComparer.Ordinal)
        { "type", "value" };
    private static readonly HashSet<string> PaddingProperties = new(StringComparer.Ordinal)
        { "left", "top", "right", "bottom" };

    private void ReadStringDictionary(JsonElement root, string name, IDictionary<string, string> destination)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
            return;
        RequireKind(element, JsonValueKind.Object, "$." + name);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            EnsureUnique(keys, property.Name, "$." + name);
            ValidateJsonKey(property.Name, "$." + name + "." + property.Name);
            destination.Add(property.Name, ReadString(property.Value, "$." + name + "." + property.Name));
        }
    }

    private void ReadStringArray(JsonElement root, string name, IList<string> destination)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
            return;
        RequireKind(element, JsonValueKind.Array, "$." + name);
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
            destination.Add(ReadString(item, $"$.{name}[{index++}]"));
    }

    private void ReadColorDictionary(JsonElement root, string name, IDictionary<string, Color> destination)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
            return;
        ReadDictionary(element, name, destination, (value, path) => ReadColor(value, path));
    }

    private void ReadBrushDictionary(JsonElement root, string name, IDictionary<string, MfnBrush> destination)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
            return;
        ReadDictionary(element, name, destination, ReadBrush);
    }

    private void ReadTypographyDictionary(JsonElement root, string name, IDictionary<string, ThemeTypography> destination)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
            return;
        ReadDictionary(element, name, destination, ReadTypography);
    }

    private void ReadNumberDictionary(JsonElement root, string name, IDictionary<string, double> destination)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
            return;
        ReadDictionary(element, name, destination, (value, path) => ReadFiniteDouble(value, path));
    }

    private void ReadPaddingDictionary(JsonElement root, string name, IDictionary<string, Padding> destination)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
            return;
        ReadDictionary(element, name, destination, ReadPadding);
    }

    private void ReadAnimationDictionary(JsonElement root, string name, IDictionary<string, ThemeAnimationSettings> destination)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
            return;
        ReadDictionary(element, name, destination, ReadAnimation);
    }

    private void ReadResourceDictionary(JsonElement root, string name, IDictionary<string, ThemeResourceValue> destination)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
            return;
        ReadDictionary(element, name, destination, ReadResource);
    }

    private void ReadDictionary<T>(
        JsonElement element,
        string name,
        IDictionary<string, T> destination,
        Func<JsonElement, string, T> read)
    {
        string path = "$." + name;
        RequireKind(element, JsonValueKind.Object, path);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            EnsureUnique(keys, property.Name, path);
            string itemPath = path + "." + property.Name;
            ValidateJsonKey(property.Name, itemPath);
            destination.Add(property.Name, read(property.Value, itemPath));
        }
    }

    private ThemeTypography ReadTypography(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Object, path);
        EnsureAllowedProperties(element, TypographyProperties, path);
        string family = ReadRequiredString(element, "fontFamily", path + ".fontFamily");
        float size = ReadFiniteSingle(Required(element, "size", path), path + ".size");
        FontStyle style = ReadEnum(element, "style", FontStyle.Regular, path + ".style");
        float? lineHeight = OptionalSingle(element, "lineHeight", path + ".lineHeight");
        float? letterSpacing = OptionalSingle(element, "letterSpacing", path + ".letterSpacing");
        return new ThemeTypography(family, size, style, lineHeight, letterSpacing);
    }

    private ThemeAnimationSettings ReadAnimation(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Object, path);
        EnsureAllowedProperties(element, AnimationProperties, path);
        double milliseconds = ReadFiniteDouble(Required(element, "durationMs", path), path + ".durationMs");
        if (milliseconds < 0d || milliseconds > ThemeSecurityLimits.MaximumAnimationDuration.TotalMilliseconds)
            throw Error("Animation duration is outside the supported range", path + ".durationMs");
        ThemeEasing easing = ReadEnum(element, "easing", ThemeEasing.EaseInOut, path + ".easing");
        bool enabled = ReadOptionalBoolean(element, "enabled", true, path + ".enabled");
        return new ThemeAnimationSettings(TimeSpan.FromMilliseconds(milliseconds), easing, enabled);
    }

    private ThemeResourceValue ReadResource(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Object, path);
        EnsureAllowedProperties(element, ResourceProperties, path);
        string type = ReadRequiredString(element, "type", path + ".type");
        JsonElement value = Required(element, "value", path);
        return type switch
        {
            "string" => ThemeResourceValue.FromString(ReadString(value, path + ".value")),
            "boolean" => ThemeResourceValue.FromBoolean(ReadBoolean(value, path + ".value")),
            "integer" => ThemeResourceValue.FromInteger(ReadInt32(value, path + ".value")),
            "number" => ThemeResourceValue.FromNumber(ReadFiniteDouble(value, path + ".value")),
            "color" => ThemeResourceValue.FromColor(ReadColor(value, path + ".value")),
            "brush" => ThemeResourceValue.FromBrush(ReadBrush(value, path + ".value")),
            "padding" => ThemeResourceValue.FromPadding(ReadPadding(value, path + ".value")),
            "typography" => ThemeResourceValue.FromTypography(ReadTypography(value, path + ".value")),
            "animation" => ThemeResourceValue.FromAnimation(ReadAnimation(value, path + ".value")),
            _ => throw Error($"Resource discriminator '{type}' is not allowed", path + ".type")
        };
    }

    private Padding ReadPadding(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Object, path);
        EnsureAllowedProperties(element, PaddingProperties, path);
        return new Padding(
            ReadRequiredInt32(element, "left", path + ".left"),
            ReadRequiredInt32(element, "top", path + ".top"),
            ReadRequiredInt32(element, "right", path + ".right"),
            ReadRequiredInt32(element, "bottom", path + ".bottom"));
    }

    private MfnBrush ReadBrush(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Object, path);
        string type = ReadRequiredString(element, "type", path + ".type");
        HashSet<string> allowed = type switch
        {
            "solid" => Set("type", "color", "opacity", "transform"),
            "linearGradient" => Set("type", "gradientStops", "spreadMode", "start", "end", "opacity", "transform"),
            "radialGradient" => Set("type", "gradientStops", "spreadMode", "center", "origin", "radius", "opacity", "transform"),
            "sweepGradient" => Set("type", "gradientStops", "spreadMode", "center", "startAngle", "endAngle", "opacity", "transform"),
            "glass" => Set("type", "tint", "secondaryTint", "highlight", "border", "showHighlight", "showInnerBorder", "opacity", "transform"),
            "none" => Set("type", "opacity", "transform"),
            _ => throw Error($"Brush discriminator '{type}' is not allowed", path + ".type")
        };
        EnsureAllowedProperties(element, allowed, path);

        MfnBrush brush = type switch
        {
            "solid" => new SolidColorBrush(ReadColor(Required(element, "color", path), path + ".color")),
            "linearGradient" => ReadLinearGradient(element, path),
            "radialGradient" => ReadRadialGradient(element, path),
            "sweepGradient" => ReadSweepGradient(element, path),
            "glass" => ReadGlass(element, path),
            "none" => new NoBrush(),
            _ => throw Error("Brush discriminator is not allowed", path + ".type")
        };
        brush.Opacity = OptionalSingle(element, "opacity", path + ".opacity") ?? 1f;
        if (element.TryGetProperty("transform", out JsonElement transform))
            brush.Transform = ReadTransform(transform, path + ".transform");
        return brush;
    }

    private LinearGradientBrush ReadLinearGradient(JsonElement element, string path)
    {
        var result = new LinearGradientBrush
        {
            Start = ReadPoint(Required(element, "start", path), path + ".start"),
            End = ReadPoint(Required(element, "end", path), path + ".end"),
            SpreadMode = ReadEnum(element, "spreadMode", GradientSpreadMode.Pad, path + ".spreadMode")
        };
        ReadStops(element, result, path);
        return result;
    }

    private RadialGradientBrush ReadRadialGradient(JsonElement element, string path)
    {
        var result = new RadialGradientBrush
        {
            CenterPoint = ReadPoint(Required(element, "center", path), path + ".center"),
            GradientOrigin = ReadPoint(Required(element, "origin", path), path + ".origin"),
            Radius = ReadFiniteSingle(Required(element, "radius", path), path + ".radius"),
            SpreadMode = ReadEnum(element, "spreadMode", GradientSpreadMode.Pad, path + ".spreadMode")
        };
        ReadStops(element, result, path);
        return result;
    }

    private SweepGradientBrush ReadSweepGradient(JsonElement element, string path)
    {
        var result = new SweepGradientBrush
        {
            CenterPoint = ReadPoint(Required(element, "center", path), path + ".center"),
            StartAngle = ReadFiniteSingle(Required(element, "startAngle", path), path + ".startAngle"),
            EndAngle = ReadFiniteSingle(Required(element, "endAngle", path), path + ".endAngle"),
            SpreadMode = ReadEnum(element, "spreadMode", GradientSpreadMode.Pad, path + ".spreadMode")
        };
        ReadStops(element, result, path);
        return result;
    }

    private GlassBrush ReadGlass(JsonElement element, string path)
        => new()
        {
            Tint = ReadColor(Required(element, "tint", path), path + ".tint"),
            SecondaryTint = ReadColor(Required(element, "secondaryTint", path), path + ".secondaryTint"),
            Highlight = ReadColor(Required(element, "highlight", path), path + ".highlight"),
            Border = ReadColor(Required(element, "border", path), path + ".border"),
            ShowHighlight = ReadOptionalBoolean(element, "showHighlight", true, path + ".showHighlight"),
            ShowInnerBorder = ReadOptionalBoolean(element, "showInnerBorder", true, path + ".showInnerBorder")
        };

    private void ReadStops(JsonElement element, GradientBrush brush, string path)
    {
        JsonElement stops = Required(element, "gradientStops", path);
        RequireKind(stops, JsonValueKind.Array, path + ".gradientStops");
        int index = 0;
        foreach (JsonElement stopElement in stops.EnumerateArray())
        {
            string stopPath = $"{path}.gradientStops[{index}]";
            RequireKind(stopElement, JsonValueKind.Object, stopPath);
            EnsureAllowedProperties(stopElement, Set("color", "offset"), stopPath);
            if (++index > limits.MaximumGradientStops)
                throw Error($"Gradient stops exceed the configured limit of {limits.MaximumGradientStops}", path + ".gradientStops");
            brush.GradientStops.Add(new GradientStop(
                ReadColor(Required(stopElement, "color", stopPath), stopPath + ".color"),
                ReadFiniteSingle(Required(stopElement, "offset", stopPath), stopPath + ".offset")));
        }
    }

    private static PointF ReadPoint(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Array, path);
        JsonElement[] values = element.EnumerateArray().ToArray();
        if (values.Length != 2)
            throw Error("A point must contain exactly two finite numbers", path);
        return new PointF(ReadFiniteSingle(values[0], path + "[0]"), ReadFiniteSingle(values[1], path + "[1]"));
    }

    private static Matrix3x2 ReadTransform(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Array, path);
        JsonElement[] values = element.EnumerateArray().ToArray();
        if (values.Length != 6)
            throw Error("A brush transform must contain exactly six finite numbers", path);
        return new Matrix3x2(
            ReadFiniteSingle(values[0], path + "[0]"), ReadFiniteSingle(values[1], path + "[1]"),
            ReadFiniteSingle(values[2], path + "[2]"), ReadFiniteSingle(values[3], path + "[3]"),
            ReadFiniteSingle(values[4], path + "[4]"), ReadFiniteSingle(values[5], path + "[5]"));
    }

    private void WriteTheme(Utf8JsonWriter writer, ThemeDefinition theme)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", theme.SchemaVersion);
        writer.WriteString("id", theme.Id);
        writer.WriteString("name", theme.Name);
        WriteOptionalString(writer, "description", theme.Description);
        WriteOptionalString(writer, "author", theme.Author);
        WriteOptionalString(writer, "baseTheme", theme.BaseTheme);
        writer.WriteString("variant", theme.Variant.ToString());
        WriteStringDictionary(writer, "metadata", theme.Metadata);
        writer.WritePropertyName("tags");
        writer.WriteStartArray();
        foreach (string tag in theme.Tags)
            writer.WriteStringValue(tag);
        writer.WriteEndArray();
        WriteDictionary(writer, "colors", theme.Colors, static (json, value) => WriteColor(json, value));
        WriteDictionary(writer, "brushes", theme.Brushes, WriteBrush);
        WriteDictionary(writer, "typography", theme.Typography, WriteTypography);
        WriteDictionary(writer, "spacing", theme.Spacing, static (json, value) => json.WriteNumberValue(value));
        WriteDictionary(writer, "padding", theme.Padding, WritePadding);
        WriteDictionary(writer, "sizing", theme.Sizing, static (json, value) => json.WriteNumberValue(value));
        WriteDictionary(writer, "corners", theme.Corners, static (json, value) => json.WriteNumberValue(value));
        WriteDictionary(writer, "borderThickness", theme.BorderThickness, static (json, value) => json.WriteNumberValue(value));
        WriteDictionary(writer, "animations", theme.Animations, WriteAnimation);
        WriteDictionary(writer, "resources", theme.Resources, WriteResource);
        writer.WriteEndObject();
    }

    private static void WriteStringDictionary(Utf8JsonWriter writer, string name, IDictionary<string, string> values)
        => WriteDictionary(writer, name, values, static (json, value) => json.WriteStringValue(value));

    private static void WriteDictionary<T>(
        Utf8JsonWriter writer,
        string name,
        IDictionary<string, T> values,
        Action<Utf8JsonWriter, T> write)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        foreach ((string key, T value) in values.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key);
            write(writer, value);
        }
        writer.WriteEndObject();
    }

    private static void WriteTypography(Utf8JsonWriter writer, ThemeTypography value)
    {
        writer.WriteStartObject();
        writer.WriteString("fontFamily", value.FontFamily);
        writer.WriteNumber("size", value.Size);
        writer.WriteString("style", value.Style.ToString());
        if (value.LineHeight is { } lineHeight)
            writer.WriteNumber("lineHeight", lineHeight);
        if (value.LetterSpacing is { } letterSpacing)
            writer.WriteNumber("letterSpacing", letterSpacing);
        writer.WriteEndObject();
    }

    private static void WriteAnimation(Utf8JsonWriter writer, ThemeAnimationSettings value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("durationMs", value.Duration.TotalMilliseconds);
        writer.WriteString("easing", value.Easing.ToString());
        writer.WriteBoolean("enabled", value.Enabled);
        writer.WriteEndObject();
    }

    private static void WriteResource(Utf8JsonWriter writer, ThemeResourceValue resource)
    {
        writer.WriteStartObject();
        writer.WriteString("type", ResourceName(resource.Kind));
        writer.WritePropertyName("value");
        object value = resource.GetRawValue();
        switch (resource.Kind)
        {
            case ThemeResourceKind.String: writer.WriteStringValue((string)value); break;
            case ThemeResourceKind.Boolean: writer.WriteBooleanValue((bool)value); break;
            case ThemeResourceKind.Integer: writer.WriteNumberValue((int)value); break;
            case ThemeResourceKind.Number: writer.WriteNumberValue((double)value); break;
            case ThemeResourceKind.Color: WriteColor(writer, (Color)value); break;
            case ThemeResourceKind.Brush: WriteBrush(writer, (MfnBrush)value); break;
            case ThemeResourceKind.Padding: WritePadding(writer, (Padding)value); break;
            case ThemeResourceKind.Typography: WriteTypography(writer, (ThemeTypography)value); break;
            case ThemeResourceKind.Animation: WriteAnimation(writer, (ThemeAnimationSettings)value); break;
            default: throw new InvalidOperationException("The theme resource kind is not supported.");
        }
        writer.WriteEndObject();
    }

    private static void WritePadding(Utf8JsonWriter writer, Padding value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("left", value.Left);
        writer.WriteNumber("top", value.Top);
        writer.WriteNumber("right", value.Right);
        writer.WriteNumber("bottom", value.Bottom);
        writer.WriteEndObject();
    }

    private static void WriteBrush(Utf8JsonWriter writer, MfnBrush brush)
    {
        writer.WriteStartObject();
        switch (brush)
        {
            case SolidColorBrush solid when solid.GetType() == typeof(SolidColorBrush):
                writer.WriteString("type", "solid");
                writer.WritePropertyName("color");
                WriteColor(writer, solid.PaintColor);
                break;
            case LinearGradientBrush linear when linear.GetType() == typeof(LinearGradientBrush):
                writer.WriteString("type", "linearGradient");
                WriteStops(writer, linear);
                writer.WriteString("spreadMode", linear.SpreadMode.ToString());
                WritePoint(writer, "start", linear.Start);
                WritePoint(writer, "end", linear.End);
                break;
            case RadialGradientBrush radial when radial.GetType() == typeof(RadialGradientBrush):
                writer.WriteString("type", "radialGradient");
                WriteStops(writer, radial);
                writer.WriteString("spreadMode", radial.SpreadMode.ToString());
                WritePoint(writer, "center", radial.CenterPoint);
                WritePoint(writer, "origin", radial.GradientOrigin);
                writer.WriteNumber("radius", radial.Radius);
                break;
            case SweepGradientBrush sweep when sweep.GetType() == typeof(SweepGradientBrush):
                writer.WriteString("type", "sweepGradient");
                WriteStops(writer, sweep);
                writer.WriteString("spreadMode", sweep.SpreadMode.ToString());
                WritePoint(writer, "center", sweep.CenterPoint);
                writer.WriteNumber("startAngle", sweep.StartAngle);
                writer.WriteNumber("endAngle", sweep.EndAngle);
                break;
            case GlassBrush glass when glass.GetType() == typeof(GlassBrush):
                writer.WriteString("type", "glass");
                WriteNamedColor(writer, "tint", glass.Tint);
                WriteNamedColor(writer, "secondaryTint", glass.SecondaryTint);
                WriteNamedColor(writer, "highlight", glass.Highlight);
                WriteNamedColor(writer, "border", glass.Border);
                writer.WriteBoolean("showHighlight", glass.ShowHighlight);
                writer.WriteBoolean("showInnerBorder", glass.ShowInnerBorder);
                break;
            case NoBrush when brush.GetType() == typeof(NoBrush):
                writer.WriteString("type", "none");
                break;
            default:
                throw new ThemeSerializationException(
                    $"Brush type '{brush.GetType().Name}' is not in the theme JSON allow-list",
                    "$.brushes");
        }
        writer.WriteNumber("opacity", brush.Opacity);
        writer.WritePropertyName("transform");
        writer.WriteStartArray();
        writer.WriteNumberValue(brush.Transform.M11);
        writer.WriteNumberValue(brush.Transform.M12);
        writer.WriteNumberValue(brush.Transform.M21);
        writer.WriteNumberValue(brush.Transform.M22);
        writer.WriteNumberValue(brush.Transform.M31);
        writer.WriteNumberValue(brush.Transform.M32);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteStops(Utf8JsonWriter writer, GradientBrush brush)
    {
        writer.WritePropertyName("gradientStops");
        writer.WriteStartArray();
        foreach (GradientStop stop in brush.GradientStops)
        {
            writer.WriteStartObject();
            WriteNamedColor(writer, "color", stop.PaintColor);
            writer.WriteNumber("offset", stop.Offset);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WritePoint(Utf8JsonWriter writer, string name, PointF value)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }

    private static void WriteNamedColor(Utf8JsonWriter writer, string name, Color value)
    {
        writer.WritePropertyName(name);
        WriteColor(writer, value);
    }

    private static void WriteColor(Utf8JsonWriter writer, Color value)
        => writer.WriteStringValue($"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");

    private static Color ReadColor(JsonElement element, string path)
    {
        string value = ReadStringStatic(element, path);
        if (value.Length is not (7 or 9) || value[0] != '#')
            throw Error("A color must use #RRGGBB or #AARRGGBB", path);
        try
        {
            uint parsed = Convert.ToUInt32(value.AsSpan(1).ToString(), 16);
            if (value.Length == 7)
                parsed |= 0xFF000000u;
            return Color.FromArgb(unchecked((int)parsed));
        }
        catch (FormatException exception)
        {
            throw new ThemeSerializationException("A color contains invalid hexadecimal digits", path, exception);
        }
    }

    private static string ResourceName(ThemeResourceKind kind) => kind switch
    {
        ThemeResourceKind.String => "string",
        ThemeResourceKind.Boolean => "boolean",
        ThemeResourceKind.Integer => "integer",
        ThemeResourceKind.Number => "number",
        ThemeResourceKind.Color => "color",
        ThemeResourceKind.Brush => "brush",
        ThemeResourceKind.Padding => "padding",
        ThemeResourceKind.Typography => "typography",
        ThemeResourceKind.Animation => "animation",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The resource kind is not defined.")
    };

    private string ReadRequiredString(JsonElement element, string name, string path)
        => ReadString(Required(element, name, Parent(path)), path);

    private string? ReadOptionalString(JsonElement element, string name, string path)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return null;
        return ReadString(value, path);
    }

    private string ReadString(JsonElement element, string path)
    {
        string value = ReadStringStatic(element, path);
        if (value.Length > limits.MaximumStringLength)
            throw Error($"String length exceeds the configured limit of {limits.MaximumStringLength}", path);
        return value;
    }

    private static string ReadStringStatic(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.String, path);
        return element.GetString()!;
    }

    private static int ReadRequiredInt32(JsonElement element, string name, string path)
        => ReadInt32(Required(element, name, Parent(path)), path);

    private static int ReadInt32(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
            throw Error("An integer value is required", path);
        return value;
    }

    private static double ReadFiniteDouble(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out double value) || !double.IsFinite(value))
            throw Error("A finite number is required", path);
        return value;
    }

    private static float ReadFiniteSingle(JsonElement element, string path)
    {
        double value = ReadFiniteDouble(element, path);
        if (value < -float.MaxValue || value > float.MaxValue)
            throw Error("The number is outside the single-precision range", path);
        return (float)value;
    }

    private static float? OptionalSingle(JsonElement element, string name, string path)
        => element.TryGetProperty(name, out JsonElement value) ? ReadFiniteSingle(value, path) : null;

    private static bool ReadOptionalBoolean(JsonElement element, string name, bool fallback, string path)
        => element.TryGetProperty(name, out JsonElement value) ? ReadBoolean(value, path) : fallback;

    private static bool ReadBoolean(JsonElement element, string path)
    {
        if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw Error("A Boolean value is required", path);
        return element.GetBoolean();
    }

    private static TEnum ReadEnum<TEnum>(JsonElement element, string name, TEnum fallback, string path)
        where TEnum : struct, Enum
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return fallback;
        string text = ReadStringStatic(value, path);
        bool isFlags = typeof(TEnum).IsDefined(typeof(FlagsAttribute), false);
        TEnum result = default;
        bool valid = !string.IsNullOrWhiteSpace(text) &&
            !char.IsAsciiDigit(text[0]) && text[0] is not '-' and not '+' &&
            Enum.TryParse(text, ignoreCase: false, out result);
        if (valid && isFlags)
        {
            valid = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .All(static part => Enum.TryParse(part, ignoreCase: false, out TEnum item) && Enum.IsDefined(item));
        }
        else if (valid)
        {
            valid = Enum.IsDefined(result);
        }

        if (!valid)
            throw Error($"Enum value '{text}' is not supported", path);
        return result;
    }

    private static JsonElement Required(JsonElement element, string name, string path)
        => element.TryGetProperty(name, out JsonElement value)
            ? value
            : throw Error($"Required property '{name}' is missing", path + "." + name);

    private static void EnsureAllowedProperties(JsonElement element, HashSet<string> allowed, string path)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            EnsureUnique(seen, property.Name, path);
            if (!allowed.Contains(property.Name))
                throw Error($"Unknown property '{property.Name}' is not allowed", path + "." + property.Name);
        }
    }

    private static void EnsureUnique(HashSet<string> seen, string name, string path)
    {
        if (!seen.Add(name))
            throw Error($"Duplicate property '{name}' is not allowed", path + "." + name);
    }

    private static void RequireKind(JsonElement element, JsonValueKind kind, string path)
    {
        if (element.ValueKind != kind)
            throw Error($"Expected JSON {kind}, found {element.ValueKind}", path);
    }

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);

    private static string Parent(string path)
    {
        int index = path.LastIndexOf('.');
        return index > 0 ? path[..index] : "$";
    }

    private void ValidateJsonKey(string value, string path)
    {
        if (value.Length > limits.MaximumStringLength || !ThemeKeyValidator.IsValid(value))
            throw Error("The property name is not a valid theme key", path);
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
            writer.WriteString(name, value);
    }
}
