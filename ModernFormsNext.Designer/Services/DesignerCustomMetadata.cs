using ModernFormsNext.Designing;
using ModernFormsNext.Designer.Properties;

namespace ModernFormsNext.Designer.Services;

// Detached data adapts non-executing Roslyn symbols to the existing property editors and
// document values. No Type, MemberInfo, attribute instance or user delegate crosses this boundary.
internal sealed record DesignerCustomControlCatalog(
    IReadOnlyList<DesignerProjectUserControlInfo> Controls,
    IReadOnlyList<string> Diagnostics);

internal sealed record DesignerCustomEvent(
    string Name, string DisplayName, string Category, string Description, string? Parameters);

internal sealed record DesignerCustomProperty(
    string Name, string DisplayName, string Category, string Description,
    Type? EditorType, string? EnumTypeName, IReadOnlyList<string>? EnumMembers,
    bool Nullable, bool ReadOnly, bool Advanced, DesignPropertyValue? DefaultValue)
{
    public bool TryConvert(string text, out DesignPropertyValue? value, out string? error)
    {
        value = null;
        error = null;
        if (ReadOnly)
        {
            error = "Metadata is read-only or requires executable conversion; edit the source instead.";
            return false;
        }

        if (Nullable && string.Equals(text.Trim(), "null", StringComparison.OrdinalIgnoreCase))
        {
            value = DesignPropertyValue.FromNull();
            return true;
        }

        if (EnumTypeName is not null)
        {
            // Only declared single members are emitted. Arbitrary expressions, numeric casts and
            // flags combinations are deliberately not accepted by this primitive editor.
            if (EnumMembers?.Contains(text.Trim(), StringComparer.Ordinal) == true)
            {
                value = DesignPropertyValue.FromEnum(EnumTypeName, text.Trim());
                return true;
            }
            error = "Choose a declared enum member.";
            return false;
        }

        if (EditorType is null || !DesignerPropertyValueEditor.TryConvert(text, EditorType, out var primitive, out error))
            return false;
        if (primitive is float number && !float.IsFinite(number)
            || primitive is double doubleNumber && !double.IsFinite(doubleNumber))
        {
            error = "Expected a finite number.";
            return false;
        }
        value = DesignPropertyValue.FromObject(primitive);
        return true;
    }

    public DesignerPropertyDescriptor CreateDescriptor(IDictionary<string, DesignPropertyValue> values) => new()
    {
        Name = Name,
        DisplayName = DisplayName,
        Category = Category,
        Description = Description,
        ValueType = EditorType ?? typeof(string),
        IsReadOnly = ReadOnly,
        IsAdvanced = Advanced,
        ShouldSerialize = !ReadOnly,
        StandardValues = EnumMembers ?? (EditorType is null ? null : DesignerPropertyValueEditor.GetStandardValues(EditorType)),
        GetValue = () => values.TryGetValue(Name, out var value) ? value.Value : DefaultValue?.Value,
        CommitText = text =>
        {
            if (!TryConvert(text, out var value, out var error))
                return (false, error);
            values[Name] = value!;
            return (true, null);
        }
    };
}
