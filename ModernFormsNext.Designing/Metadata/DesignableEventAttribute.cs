namespace ModernFormsNext.Designing;

/// <summary>
/// Describes a runtime event as visible to ModernFormsNext designer tooling.
/// </summary>
/// <remarks>
/// Apply this attribute to control events when the designer should display a
/// custom name, category, description, or visibility state. The attribute is
/// intentionally Visual Studio independent and can be consumed by standalone
/// tools as well as a future IDE extension.
/// </remarks>
[AttributeUsage(AttributeTargets.Event, Inherited = true)]
public sealed class DesignableEventAttribute : Attribute
{
    /// <summary>
    /// Gets or initializes the display name shown by event grids.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets or initializes the event category shown by designer UI.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets or initializes the event description shown by designer UI.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the event should be visible in designer UI.
    /// </summary>
    public bool Visible { get; init; } = true;
}
