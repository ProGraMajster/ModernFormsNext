using System.Text.Json;
using System.Text.Json.Serialization;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Clipboard;

/// <summary>
/// Converts a single control subtree to and from the private Designer clipboard format.
/// </summary>
internal static class DesignerClipboardSerializer
{
    internal const int MaximumContentLength = 4 * 1024 * 1024;
    internal const int MaximumNodeCount = 10_000;
    internal const int MaximumTreeDepth = 96;

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static bool TrySerialize(DesignControlNode node, out string? content, out string? error)
    {
        ArgumentNullException.ThrowIfNull(node);

        try
        {
            var remainingNodes = MaximumNodeCount;
            var payload = new DesignerClipboardPayload
            {
                Format = DesignerClipboardPayload.CurrentFormat,
                Version = DesignerClipboardPayload.CurrentVersion,
                Root = ToClipboardNode(node, depth: 0, ref remainingNodes)
            };
            content = JsonSerializer.Serialize(payload, Options);
            if (content.Length > MaximumContentLength)
                throw new InvalidOperationException("The selected Designer subtree is too large for the internal clipboard.");

            error = null;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or JsonException)
        {
            content = null;
            error = $"Cannot copy the selected control: {exception.Message}";
            return false;
        }
    }

    public static bool TryDeserialize(string? content, out DesignControlNode? node, out string? error)
    {
        node = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "The Designer clipboard is empty.";
            return false;
        }

        if (content.Length > MaximumContentLength)
        {
            error = "The Designer clipboard payload exceeds the supported size limit.";
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<DesignerClipboardPayload>(content, Options)
                ?? throw new JsonException("The Designer clipboard did not contain a payload.");

            if (!string.Equals(payload.Format, DesignerClipboardPayload.CurrentFormat, StringComparison.Ordinal))
                throw new JsonException($"Unsupported Designer clipboard format '{payload.Format}'.");
            if (payload.Version != DesignerClipboardPayload.CurrentVersion)
                throw new JsonException($"Unsupported Designer clipboard version '{payload.Version}'.");
            if (payload.Root is null)
                throw new JsonException("The Designer clipboard payload does not contain a control subtree.");

            var remainingNodes = MaximumNodeCount;
            node = FromClipboardNode(payload.Root, depth: 0, ref remainingNodes);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or JsonException)
        {
            error = $"Cannot paste the Designer clipboard payload: {exception.Message}";
            return false;
        }
    }

    private static DesignerClipboardNode ToClipboardNode(
        DesignControlNode node,
        int depth,
        ref int remainingNodes)
    {
        EnsureNodeBudget(depth, ref remainingNodes);

        var result = new DesignerClipboardNode
        {
            TypeName = node.TypeName,
            Name = node.Name,
            X = node.Bounds.X,
            Y = node.Bounds.Y,
            Width = node.Bounds.Width,
            Height = node.Bounds.Height,
            MemberVisibility = node.MemberVisibility,
            Properties = new SortedDictionary<string, DesignerClipboardValue>(
                node.Properties.ToDictionary(
                    property => property.Key,
                    property => ToClipboardValue(property.Value),
                    StringComparer.Ordinal),
                StringComparer.Ordinal),
            Events = new SortedDictionary<string, string?>(node.Events, StringComparer.Ordinal),
            Children = []
        };

        foreach (var child in node.Children)
            result.Children!.Add(ToClipboardNode(child, depth + 1, ref remainingNodes));

        return result;
    }

    private static DesignControlNode FromClipboardNode(
        DesignerClipboardNode source,
        int depth,
        ref int remainingNodes)
    {
        EnsureNodeBudget(depth, ref remainingNodes);
        var typeName = RequireSafeTypeName(source.TypeName, "control type");
        var name = RequireIdentifier(source.Name, "control name");

        if (source.X is null || source.Y is null || source.Width is null || source.Height is null)
            throw new JsonException($"Control '{name}' has incomplete bounds.");
        if (source.Width < 0 || source.Height < 0)
            throw new JsonException($"Control '{name}' has negative bounds.");
        if (source.MemberVisibility is null || !Enum.IsDefined(source.MemberVisibility.Value))
            throw new JsonException($"Control '{name}' has an unsupported member visibility.");
        if (source.Properties is null || source.Events is null || source.Children is null)
            throw new JsonException($"Control '{name}' has an incomplete clipboard representation.");

        var node = new DesignControlNode
        {
            TypeName = typeName,
            Name = name,
            Bounds = new DesignBounds(source.X.Value, source.Y.Value, source.Width.Value, source.Height.Value),
            MemberVisibility = source.MemberVisibility.Value
        };

        foreach (var property in source.Properties)
        {
            var propertyName = RequireMemberPath(property.Key, "property name");
            node.Properties[propertyName] = FromClipboardValue(property.Value, propertyName);
        }

        foreach (var eventBinding in source.Events)
        {
            var eventName = RequireIdentifier(eventBinding.Key, "event name");
            if (!string.IsNullOrWhiteSpace(eventBinding.Value)
                && !DesignDocumentValidator.IsValidCSharpIdentifier(eventBinding.Value))
            {
                throw new JsonException($"Event '{eventName}' has an invalid handler name '{eventBinding.Value}'.");
            }

            node.Events[eventName] = eventBinding.Value;
        }

        foreach (var child in source.Children)
            node.Children.Add(FromClipboardNode(child, depth + 1, ref remainingNodes));

        return node;
    }

    private static DesignerClipboardValue ToClipboardValue(DesignPropertyValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new DesignerClipboardValue { Kind = value.Kind };

        switch (value.Kind)
        {
            case DesignPropertyValueKind.Null:
                if (value.Value is not null)
                    throw new InvalidOperationException("A null Designer value contains unexpected CLR data.");
                break;
            case DesignPropertyValueKind.String:
                result.StringValue = value.Value as string
                    ?? throw new InvalidOperationException("A string Designer value does not contain a string.");
                break;
            case DesignPropertyValueKind.Boolean:
                result.BooleanValue = value.Value is bool booleanValue
                    ? booleanValue
                    : throw new InvalidOperationException("A Boolean Designer value does not contain a Boolean.");
                break;
            case DesignPropertyValueKind.Int32:
                result.Int32Value = value.Value is int intValue
                    ? intValue
                    : throw new InvalidOperationException("An Int32 Designer value does not contain an Int32.");
                break;
            case DesignPropertyValueKind.Double:
                result.DoubleValue = value.Value is double doubleValue
                    ? doubleValue
                    : throw new InvalidOperationException("A Double Designer value does not contain a Double.");
                break;
            case DesignPropertyValueKind.Enum:
                result.TypeName = RequireSafeTypeName(value.EnumTypeName, "enum type");
                result.StringValue = RequireEnumMemberText(
                    value.Value as string
                        ?? throw new InvalidOperationException("An enum Designer value does not contain a member name."),
                    "enum member");
                break;
            case DesignPropertyValueKind.Object:
                result.TypeName = RequireSafeTypeName(value.ObjectTypeName, "object type");
                result.Properties = new SortedDictionary<string, DesignerClipboardValue>(StringComparer.Ordinal);
                foreach (var property in value.ObjectProperties
                    ?? throw new InvalidOperationException("A structured Designer value has no properties."))
                {
                    result.Properties[RequireMemberPath(property.Key, "structured property name")] = ToClipboardValue(property.Value);
                }
                break;
            default:
                throw new NotSupportedException($"Designer value kind '{value.Kind}' is not supported by clipboard version 1.");
        }

        return result;
    }

    private static DesignPropertyValue FromClipboardValue(DesignerClipboardValue? value, string path)
    {
        if (value?.Kind is null || !Enum.IsDefined(value.Kind.Value))
            throw new JsonException($"Property '{path}' has an unsupported value kind.");

        EnsureCanonicalValueRepresentation(value, path);
        return value.Kind.Value switch
        {
            DesignPropertyValueKind.Null => DesignPropertyValue.FromNull(),
            DesignPropertyValueKind.String when value.StringValue is not null => DesignPropertyValue.FromString(value.StringValue),
            DesignPropertyValueKind.Boolean when value.BooleanValue is not null => DesignPropertyValue.FromBoolean(value.BooleanValue.Value),
            DesignPropertyValueKind.Int32 when value.Int32Value is not null => DesignPropertyValue.FromInt32(value.Int32Value.Value),
            DesignPropertyValueKind.Double when value.DoubleValue is not null => DesignPropertyValue.FromDouble(value.DoubleValue.Value),
            DesignPropertyValueKind.Enum when value.StringValue is not null => DesignPropertyValue.FromEnum(
                RequireSafeTypeName(value.TypeName, $"enum type for '{path}'"),
                RequireEnumMemberText(value.StringValue, $"enum member for '{path}'")),
            DesignPropertyValueKind.Object when value.Properties is not null => DesignPropertyValue.FromStructuredObject(
                RequireSafeTypeName(value.TypeName, $"object type for '{path}'"),
                value.Properties.ToDictionary(
                    property => RequireMemberPath(property.Key, "structured property name"),
                    property => FromClipboardValue(property.Value, $"{path}.{property.Key}"),
                    StringComparer.Ordinal)),
            _ => throw new JsonException($"Property '{path}' has an incomplete value representation for kind '{value.Kind}'.")
        };
    }

    private static void EnsureCanonicalValueRepresentation(DesignerClipboardValue value, string path)
    {
        var valid = value.Kind switch
        {
            DesignPropertyValueKind.Null => value.StringValue is null
                && value.BooleanValue is null
                && value.Int32Value is null
                && value.DoubleValue is null
                && value.TypeName is null
                && value.Properties is null,
            DesignPropertyValueKind.String => value.StringValue is not null
                && value.BooleanValue is null
                && value.Int32Value is null
                && value.DoubleValue is null
                && value.TypeName is null
                && value.Properties is null,
            DesignPropertyValueKind.Boolean => value.StringValue is null
                && value.BooleanValue is not null
                && value.Int32Value is null
                && value.DoubleValue is null
                && value.TypeName is null
                && value.Properties is null,
            DesignPropertyValueKind.Int32 => value.StringValue is null
                && value.BooleanValue is null
                && value.Int32Value is not null
                && value.DoubleValue is null
                && value.TypeName is null
                && value.Properties is null,
            DesignPropertyValueKind.Double => value.StringValue is null
                && value.BooleanValue is null
                && value.Int32Value is null
                && value.DoubleValue is not null
                && value.TypeName is null
                && value.Properties is null,
            DesignPropertyValueKind.Enum => value.StringValue is not null
                && value.BooleanValue is null
                && value.Int32Value is null
                && value.DoubleValue is null
                && value.TypeName is not null
                && value.Properties is null,
            DesignPropertyValueKind.Object => value.StringValue is null
                && value.BooleanValue is null
                && value.Int32Value is null
                && value.DoubleValue is null
                && value.TypeName is not null
                && value.Properties is not null,
            _ => false
        };

        if (!valid)
            throw new JsonException($"Property '{path}' has a non-canonical representation for kind '{value.Kind}'.");
    }

    private static void EnsureNodeBudget(int depth, ref int remainingNodes)
    {
        if (depth > MaximumTreeDepth)
            throw new JsonException($"The Designer clipboard subtree exceeds the supported depth of {MaximumTreeDepth}.");
        if (--remainingNodes < 0)
            throw new JsonException($"The Designer clipboard subtree exceeds {MaximumNodeCount} controls.");
    }

    private static string RequireIdentifier(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value) || !DesignDocumentValidator.IsValidCSharpIdentifier(value))
            throw new JsonException($"The clipboard {description} '{value}' is not a valid C# identifier.");

        return value;
    }

    private static string RequireMemberPath(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException($"The clipboard {description} '{value}' is invalid.");

        var parts = value.Split('.');
        if (parts.Any(part => string.IsNullOrWhiteSpace(part)
            || !DesignDocumentValidator.IsValidCSharpIdentifier(part)))
        {
            throw new JsonException($"The clipboard {description} '{value}' is invalid.");
        }

        return value;
    }

    private static string RequireEnumMemberText(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException($"The clipboard {description} is missing.");

        var normalized = value.Trim();
        if (long.TryParse(normalized, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)
            || ulong.TryParse(normalized, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return value;
        }

        var members = normalized.Split(',', StringSplitOptions.TrimEntries);
        if (members.Length == 0
            || members.Any(member => string.IsNullOrWhiteSpace(member)
                || !DesignDocumentValidator.IsValidCSharpIdentifier(member)))
        {
            throw new JsonException($"The clipboard {description} '{value}' is invalid.");
        }

        return value;
    }

    private static string RequireSafeTypeName(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException($"The clipboard {description} is missing.");

        var normalized = value.StartsWith("global::", StringComparison.Ordinal) ? value[8..] : value;
        if (normalized.Length > 1024
            || normalized.Any(character => !(char.IsLetterOrDigit(character)
                || character is '_' or '.' or '+' or '`' or '[' or ']' or ',' or ' ')))
        {
            throw new JsonException($"The clipboard {description} '{value}' is invalid.");
        }

        return value;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            MaxDepth = MaximumTreeDepth + 16,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
