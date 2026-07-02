namespace ModernFormsNext.Designing;

/// <summary>
/// Describes a runtime control type as seen by ModernFormsNext designer tooling.
/// </summary>
public sealed class DesignControlMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignControlMetadata"/> class.
    /// </summary>
    /// <param name="controlType">The reflected control type.</param>
    /// <param name="displayName">The display name shown in toolbox UI.</param>
    /// <param name="category">The toolbox category.</param>
    /// <param name="description">The control description.</param>
    /// <param name="visibleInToolbox">A value indicating whether the control should be shown in toolbox UI.</param>
    /// <param name="properties">The designer-visible properties for the control type.</param>
    /// <param name="events">The designer-visible events for the control type.</param>
    public DesignControlMetadata(
        Type controlType,
        string displayName,
        string? category,
        string? description,
        bool visibleInToolbox,
        IReadOnlyList<DesignPropertyMetadata> properties,
        IReadOnlyList<DesignEventMetadata> events)
    {
        ControlType = controlType;
        DisplayName = displayName;
        Category = category;
        Description = description;
        VisibleInToolbox = visibleInToolbox;
        Properties = properties;
        Events = events;
    }

    /// <summary>
    /// Gets the reflected control type.
    /// </summary>
    public Type ControlType { get; }

    /// <summary>
    /// Gets the display name shown in toolbox UI.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the toolbox category.
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Gets the control description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets a value indicating whether the control should be shown in toolbox UI.
    /// </summary>
    public bool VisibleInToolbox { get; }

    /// <summary>
    /// Gets the properties that should be shown by designer property UI.
    /// </summary>
    public IReadOnlyList<DesignPropertyMetadata> Properties { get; }

    /// <summary>
    /// Gets the events that should be shown by designer event UI.
    /// </summary>
    public IReadOnlyList<DesignEventMetadata> Events { get; }
}
