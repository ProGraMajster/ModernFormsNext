namespace ModernFormsNext.Designing;

/// <summary>
/// Marks a control type as intentionally available to ModernFormsNext designer tooling.
/// </summary>
/// <remarks>
/// This attribute is optional. When it is not present, <see cref="DesignMetadataReader"/>
/// can still infer basic metadata from standard <see cref="System.ComponentModel"/> attributes
/// and public type information.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class DesignableControlAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignableControlAttribute"/> class.
    /// </summary>
    /// <param name="displayName">The display name used in designer toolbox UI.</param>
    public DesignableControlAttribute(string displayName)
    {
        DisplayName = displayName;
    }

    /// <summary>
    /// Gets the display name used in designer toolbox UI.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets or initializes the toolbox category for the control.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets or initializes a short description shown by designer tooling.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the control should be visible in toolbox UI.
    /// </summary>
    public bool VisibleInToolbox { get; init; } = true;
}
