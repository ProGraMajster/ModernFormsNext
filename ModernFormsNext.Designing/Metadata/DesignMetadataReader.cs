using System.ComponentModel;
using System.Reflection;

namespace ModernFormsNext.Designing;

/// <summary>
/// Reads ModernFormsNext designer metadata from reflected types and properties.
/// </summary>
/// <remarks>
/// The reader is intentionally small and Visual Studio independent. ModernFormsNext
/// designer attributes take precedence when present. When they are absent, the reader
/// falls back to standard <see cref="System.ComponentModel"/> attributes such as
/// <see cref="BrowsableAttribute"/>, <see cref="CategoryAttribute"/>,
/// <see cref="DescriptionAttribute"/>, <see cref="DisplayNameAttribute"/>,
/// <see cref="DefaultValueAttribute"/>, <see cref="ReadOnlyAttribute"/>, and
/// <see cref="DesignerSerializationVisibilityAttribute"/>.
/// </remarks>
public sealed class DesignMetadataReader
{
    /// <summary>
    /// Reads designer metadata for a control type.
    /// </summary>
    /// <param name="controlType">The control type to inspect.</param>
    /// <returns>Designer metadata for the control type.</returns>
    public DesignControlMetadata ReadControl(Type controlType)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        var designable = controlType.GetCustomAttribute<DesignableControlAttribute>(inherit: true);
        var displayName = designable?.DisplayName
            ?? ReadDisplayName(controlType)
            ?? controlType.Name;
        var category = designable?.Category ?? ReadCategory(controlType);
        var description = designable?.Description ?? ReadDescription(controlType);
        var visibleInToolbox = designable?.VisibleInToolbox
            ?? controlType.GetCustomAttribute<BrowsableAttribute>(inherit: true)?.Browsable
            ?? true;
        var properties = controlType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsInspectableProperty)
            .Select(ReadProperty)
            .Where(property => property.Visibility != DesignPropertyVisibility.Hidden)
            .OrderBy(property => property.Category ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(property => property.DisplayName, StringComparer.Ordinal)
            .ThenBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        var events = controlType
            .GetEvents(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsInspectableEvent)
            .Select(ReadEvent)
            .Where(eventMetadata => eventMetadata.Visible)
            .OrderBy(eventMetadata => eventMetadata.Category ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(eventMetadata => eventMetadata.DisplayName, StringComparer.Ordinal)
            .ThenBy(eventMetadata => eventMetadata.Name, StringComparer.Ordinal)
            .ToArray();

        return new DesignControlMetadata(
            controlType,
            displayName,
            category,
            description,
            visibleInToolbox,
            properties,
            events);
    }

    /// <summary>
    /// Reads designer metadata for a reflected property.
    /// </summary>
    /// <param name="property">The property to inspect.</param>
    /// <returns>Designer metadata for the property.</returns>
    public DesignPropertyMetadata ReadProperty(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var designable = property.GetCustomAttribute<DesignablePropertyAttribute>(inherit: true);
        var hidden = property.GetCustomAttribute<DesignerHiddenAttribute>(inherit: true) is not null;
        var defaultValue = property.GetCustomAttribute<DefaultValueAttribute>(inherit: true);

        if (hidden)
        {
            return CreatePropertyMetadata(
                property,
                designable,
                DesignPropertyVisibility.Hidden,
                readOnly: true,
                serialize: false,
                defaultValue);
        }

        if (designable is not null)
        {
            return CreatePropertyMetadata(
                property,
                designable,
                designable.Visibility,
                designable.ReadOnly || !CanWrite(property),
                designable.Serialize,
                defaultValue);
        }

        var browsable = property.GetCustomAttribute<BrowsableAttribute>(inherit: true);
        var serialization = property.GetCustomAttribute<DesignerSerializationVisibilityAttribute>(inherit: true);
        var hasStandardMetadata = HasStandardDesignerMetadata(property);
        var safeByDefault = !hasStandardMetadata && CanWrite(property) && IsSimpleSerializableType(property.PropertyType);
        var visibility = browsable?.Browsable == false
            ? DesignPropertyVisibility.Hidden
            : hasStandardMetadata || safeByDefault
                ? DesignPropertyVisibility.Visible
                : DesignPropertyVisibility.Hidden;
        var readOnly = property.GetCustomAttribute<ReadOnlyAttribute>(inherit: true)?.IsReadOnly == true
            || !CanWrite(property);
        var serialize = visibility != DesignPropertyVisibility.Hidden
            && serialization?.Visibility != DesignerSerializationVisibility.Hidden
            && CanWrite(property)
            && IsSimpleSerializableType(property.PropertyType);

        return CreatePropertyMetadata(
            property,
            designable: null,
            visibility,
            readOnly,
            serialize,
            defaultValue);
    }

    /// <summary>
    /// Reads designer metadata for a reflected event.
    /// </summary>
    /// <param name="eventInfo">The event to inspect.</param>
    /// <returns>Designer metadata for the event.</returns>
    public DesignEventMetadata ReadEvent(EventInfo eventInfo)
    {
        ArgumentNullException.ThrowIfNull(eventInfo);

        var designable = eventInfo.GetCustomAttribute<DesignableEventAttribute>(inherit: true);
        var displayName = designable?.DisplayName
            ?? ReadDisplayName(eventInfo)
            ?? eventInfo.Name;
        var category = designable?.Category ?? ReadCategory(eventInfo);
        var description = designable?.Description ?? ReadDescription(eventInfo);

        var visible = designable?.Visible
            ?? eventInfo.GetCustomAttribute<BrowsableAttribute>(inherit: true)?.Browsable
            ?? HasStandardDesignerMetadata(eventInfo);

        return new DesignEventMetadata(
            eventInfo,
            displayName,
            category,
            description,
            visible);
    }

    /// <summary>
    /// Reads generated member visibility from a field or property.
    /// </summary>
    /// <param name="member">The member to inspect.</param>
    /// <returns>The configured member visibility, or <see cref="DesignerMemberVisibility.Private"/> when no attribute exists.</returns>
    public DesignerMemberVisibility ReadMemberVisibility(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return member.GetCustomAttribute<DesignerMemberVisibilityAttribute>(inherit: true)?.Visibility
            ?? DesignerMemberVisibility.Private;
    }

    /// <summary>
    /// Determines whether a type is safe for the MVP serializer and generated property assignment.
    /// </summary>
    /// <param name="type">The property type to test.</param>
    /// <returns><see langword="true"/> when the type is supported by primitive designer values; otherwise, <see langword="false"/>.</returns>
    public static bool IsSimpleSerializableType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var valueType = Nullable.GetUnderlyingType(type) ?? type;

        return valueType == typeof(string)
            || valueType == typeof(bool)
            || valueType == typeof(int)
            || valueType == typeof(float)
            || valueType == typeof(double)
            || valueType.IsEnum;
    }

    private static DesignPropertyMetadata CreatePropertyMetadata(
        PropertyInfo property,
        DesignablePropertyAttribute? designable,
        DesignPropertyVisibility visibility,
        bool readOnly,
        bool serialize,
        DefaultValueAttribute? defaultValue)
    {
        var displayName = designable?.DisplayName
            ?? ReadDisplayName(property)
            ?? property.Name;
        var category = designable?.Category ?? ReadCategory(property);
        var description = designable?.Description ?? ReadDescription(property);

        return new DesignPropertyMetadata(
            property,
            displayName,
            category,
            description,
            visibility,
            readOnly,
            serialize,
            defaultValue is not null,
            defaultValue?.Value);
    }

    private static bool IsInspectableProperty(PropertyInfo property)
        => property.GetMethod is not null
        && !property.GetMethod.IsStatic
        && property.GetIndexParameters().Length == 0;

    private static bool IsInspectableEvent(EventInfo eventInfo)
        => eventInfo.AddMethod is not null
        && !eventInfo.AddMethod.IsStatic;

    private static bool CanWrite(PropertyInfo property)
        => property.SetMethod is not null && property.SetMethod.IsPublic && !property.SetMethod.IsStatic;

    private static bool HasStandardDesignerMetadata(PropertyInfo property)
        => property.GetCustomAttribute<BrowsableAttribute>(inherit: true) is not null
        || property.GetCustomAttribute<CategoryAttribute>(inherit: true) is not null
        || property.GetCustomAttribute<DescriptionAttribute>(inherit: true) is not null
        || property.GetCustomAttribute<DisplayNameAttribute>(inherit: true) is not null
        || property.GetCustomAttribute<DefaultValueAttribute>(inherit: true) is not null
        || property.GetCustomAttribute<ReadOnlyAttribute>(inherit: true) is not null
        || property.GetCustomAttribute<DesignerSerializationVisibilityAttribute>(inherit: true) is not null;

    private static bool HasStandardDesignerMetadata(EventInfo eventInfo)
        => eventInfo.GetCustomAttribute<BrowsableAttribute>(inherit: true) is not null
        || eventInfo.GetCustomAttribute<CategoryAttribute>(inherit: true) is not null
        || eventInfo.GetCustomAttribute<DescriptionAttribute>(inherit: true) is not null
        || eventInfo.GetCustomAttribute<DisplayNameAttribute>(inherit: true) is not null;

    private static string? ReadDisplayName(MemberInfo member)
    {
        var displayName = member.GetCustomAttribute<DisplayNameAttribute>(inherit: true);

        return displayName is not null && !string.IsNullOrWhiteSpace(displayName.DisplayName)
            ? displayName.DisplayName
            : null;
    }

    private static string? ReadCategory(MemberInfo member)
    {
        var category = member.GetCustomAttribute<CategoryAttribute>(inherit: true);

        return category is not null && !string.IsNullOrWhiteSpace(category.Category)
            ? category.Category
            : null;
    }

    private static string? ReadDescription(MemberInfo member)
    {
        var description = member.GetCustomAttribute<DescriptionAttribute>(inherit: true);

        return description is not null && !string.IsNullOrWhiteSpace(description.Description)
            ? description.Description
            : null;
    }
}
