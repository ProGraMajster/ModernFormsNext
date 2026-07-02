using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModernFormsNext.Designing;

internal sealed class DesignPropertyValueJsonConverter : JsonConverter<DesignPropertyValue>
{
    public override DesignPropertyValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => DesignPropertyValue.FromNull(),
            JsonTokenType.String => DesignPropertyValue.FromString(reader.GetString() ?? string.Empty),
            JsonTokenType.True => DesignPropertyValue.FromBoolean(true),
            JsonTokenType.False => DesignPropertyValue.FromBoolean(false),
            JsonTokenType.Number => ReadNumber(ref reader),
            JsonTokenType.StartObject => ReadObject(ref reader),
            _ => throw new JsonException($"Unsupported designer property value token '{reader.TokenType}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, DesignPropertyValue value, JsonSerializerOptions options)
    {
        switch (value.Kind)
        {
            case DesignPropertyValueKind.Null:
                writer.WriteNullValue();
                break;
            case DesignPropertyValueKind.String:
                writer.WriteStringValue((string?)value.Value ?? string.Empty);
                break;
            case DesignPropertyValueKind.Boolean:
                writer.WriteBooleanValue(value.Value is bool boolValue && boolValue);
                break;
            case DesignPropertyValueKind.Int32:
                writer.WriteNumberValue(value.Value is int intValue ? intValue : 0);
                break;
            case DesignPropertyValueKind.Double:
                writer.WriteNumberValue(value.Value is double doubleValue ? doubleValue : Convert.ToDouble(value.Value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case DesignPropertyValueKind.Enum:
                writer.WriteStartObject();
                writer.WriteString("kind", "enum");
                writer.WriteString("typeName", value.EnumTypeName);
                writer.WriteString("value", (string?)value.Value ?? string.Empty);
                writer.WriteEndObject();
                break;
            case DesignPropertyValueKind.Object:
                writer.WriteStartObject();
                writer.WriteString("kind", "object");
                writer.WriteString("typeName", value.ObjectTypeName);
                writer.WritePropertyName("properties");
                writer.WriteStartObject();

                foreach (var property in value.ObjectProperties ?? new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Key);
                    Write(writer, property.Value, options);
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            default:
                throw new JsonException($"Unsupported designer property value kind '{value.Kind}'.");
        }
    }

    private static DesignPropertyValue ReadNumber(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt32(out var intValue))
            return DesignPropertyValue.FromInt32(intValue);

        return DesignPropertyValue.FromDouble(reader.GetDouble());
    }

    private DesignPropertyValue ReadObject(ref Utf8JsonReader reader)
    {
        string? kind = null;
        string? typeName = null;
        string? value = null;
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a property name inside a designer property value object.");

            var propertyName = reader.GetString();

            if (!reader.Read())
                throw new JsonException("Unexpected end of designer property value object.");

            switch (propertyName)
            {
                case "kind":
                    kind = reader.GetString();
                    break;
                case "typeName":
                    typeName = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "value":
                    value = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "properties":
                    properties = ReadObjectProperties(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (string.Equals(kind, "enum", StringComparison.OrdinalIgnoreCase))
            return DesignPropertyValue.FromEnum(typeName ?? string.Empty, value ?? string.Empty);

        if (string.Equals(kind, "object", StringComparison.OrdinalIgnoreCase))
            return DesignPropertyValue.FromStructuredObject(typeName ?? "object", properties);

        throw new JsonException($"Unsupported designer property value object kind '{kind}'.");
    }

    private SortedDictionary<string, DesignPropertyValue> ReadObjectProperties(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected an object for structured designer property value properties.");

        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return properties;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a structured property name.");

            var propertyName = reader.GetString() ?? string.Empty;

            if (!reader.Read())
                throw new JsonException("Unexpected end of structured designer property value.");

            properties[propertyName] = Read(ref reader, typeof(DesignPropertyValue), new JsonSerializerOptions())
                ?? DesignPropertyValue.FromNull();
        }

        throw new JsonException("Unexpected end of structured designer property value properties.");
    }
}
