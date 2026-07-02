namespace ModernFormsNext.Designing;

/// <summary>
/// Describes whether a property should be shown by designer property editing UI.
/// </summary>
public enum DesignPropertyVisibility
{
    /// <summary>
    /// The property is hidden from designer property UI and should not be manually edited.
    /// </summary>
    Hidden,

    /// <summary>
    /// The property is available for advanced designer UI modes.
    /// </summary>
    Advanced,

    /// <summary>
    /// The property is normally visible in designer property UI.
    /// </summary>
    Visible
}
