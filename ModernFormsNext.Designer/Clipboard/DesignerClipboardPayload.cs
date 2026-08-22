using System.Text.Json.Serialization;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Clipboard;

/// <summary>
/// Defines the private, versioned data contract stored by the Designer clipboard.
/// </summary>
/// <remarks>
/// These DTOs deliberately mirror only persisted design data. They do not contain runtime
/// controls, CLR type instances, handles, delegates, dispatchers, histories, or selections.
/// </remarks>
internal sealed class DesignerClipboardPayload
{
    public const string CurrentFormat = "ModernFormsNext.Designer";
    public const int CurrentVersion = 1;

    public string? Format { get; set; }

    public int? Version { get; set; }

    public DesignerClipboardNode? Root { get; set; }
}

internal sealed class DesignerClipboardNode
{
    public string? TypeName { get; set; }

    public string? Name { get; set; }

    public int? X { get; set; }

    public int? Y { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public DesignerMemberVisibility? MemberVisibility { get; set; }

    public SortedDictionary<string, DesignerClipboardValue>? Properties { get; set; }

    public SortedDictionary<string, string?>? Events { get; set; }

    public List<DesignerClipboardNode>? Children { get; set; }
}

internal sealed class DesignerClipboardValue
{
    public DesignPropertyValueKind? Kind { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StringValue { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BooleanValue { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Int32Value { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? DoubleValue { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SortedDictionary<string, DesignerClipboardValue>? Properties { get; set; }
}
