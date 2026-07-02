using System.Reflection;

namespace ModernFormsNext.Designing;

/// <summary>
/// Describes a runtime event as seen by ModernFormsNext designer tooling.
/// </summary>
public sealed class DesignEventMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignEventMetadata"/> class.
    /// </summary>
    /// <param name="eventInfo">The reflected runtime event.</param>
    /// <param name="displayName">The display name shown in designer UI.</param>
    /// <param name="category">The event category.</param>
    /// <param name="description">The event description.</param>
    /// <param name="visible">A value indicating whether the event should be visible in designer UI.</param>
    public DesignEventMetadata(
        EventInfo eventInfo,
        string displayName,
        string? category,
        string? description,
        bool visible)
    {
        EventInfo = eventInfo;
        DisplayName = displayName;
        Category = category;
        Description = description;
        Visible = visible;
    }

    /// <summary>
    /// Gets the reflected runtime event.
    /// </summary>
    public EventInfo EventInfo { get; }

    /// <summary>
    /// Gets the runtime event name.
    /// </summary>
    public string Name => EventInfo.Name;

    /// <summary>
    /// Gets the display name shown in designer UI.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the event category.
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Gets the event description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets a value indicating whether the event should be visible in designer UI.
    /// </summary>
    public bool Visible { get; }
}
