using System.Reflection;

namespace ModernFormsNext.Designing;

/// <summary>
/// Describes a runtime property as seen by ModernFormsNext designer tooling.
/// </summary>
public sealed class DesignPropertyMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignPropertyMetadata"/> class.
    /// </summary>
    /// <param name="property">The reflected runtime property.</param>
    /// <param name="displayName">The display name shown in designer UI.</param>
    /// <param name="category">The property category.</param>
    /// <param name="description">The property description.</param>
    /// <param name="visibility">The designer visibility.</param>
    /// <param name="readOnly">A value indicating whether the property is read-only in designer UI.</param>
    /// <param name="serialize">A value indicating whether the property should be serialized.</param>
    /// <param name="hasDefaultValue">A value indicating whether a default value was provided by metadata.</param>
    /// <param name="defaultValue">The default value from metadata.</param>
    public DesignPropertyMetadata(
        PropertyInfo property,
        string displayName,
        string? category,
        string? description,
        DesignPropertyVisibility visibility,
        bool readOnly,
        bool serialize,
        bool hasDefaultValue,
        object? defaultValue)
    {
        Property = property;
        DisplayName = displayName;
        Category = category;
        Description = description;
        Visibility = visibility;
        ReadOnly = readOnly;
        Serialize = serialize;
        HasDefaultValue = hasDefaultValue;
        DefaultValue = defaultValue;
    }

    /// <summary>
    /// Gets the reflected runtime property.
    /// </summary>
    public PropertyInfo Property { get; }

    /// <summary>
    /// Gets the runtime property name.
    /// </summary>
    public string Name => Property.Name;

    /// <summary>
    /// Gets the property type.
    /// </summary>
    public Type PropertyType => Property.PropertyType;

    /// <summary>
    /// Gets the display name shown in designer UI.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the property category.
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Gets the property description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the designer property visibility.
    /// </summary>
    public DesignPropertyVisibility Visibility { get; }

    /// <summary>
    /// Gets a value indicating whether the property is hidden from designer UI.
    /// </summary>
    public bool IsHidden => Visibility == DesignPropertyVisibility.Hidden;

    /// <summary>
    /// Gets a value indicating whether the property should be edited as read-only.
    /// </summary>
    public bool ReadOnly { get; }

    /// <summary>
    /// Gets a value indicating whether the property should be serialized by designer code generation.
    /// </summary>
    public bool Serialize { get; }

    /// <summary>
    /// Gets a value indicating whether a default value was supplied by metadata.
    /// </summary>
    public bool HasDefaultValue { get; }

    /// <summary>
    /// Gets the default value supplied by metadata.
    /// </summary>
    public object? DefaultValue { get; }
}
