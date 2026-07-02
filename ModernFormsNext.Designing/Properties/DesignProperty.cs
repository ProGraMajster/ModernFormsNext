namespace ModernFormsNext.Designing;

/// <summary>
/// Represents a named property on a designer control node.
/// </summary>
/// <remarks>
/// Control nodes store properties in a dictionary for quick lookup. This type is
/// provided for future property-grid and editor scenarios where a named property row
/// is more convenient than a dictionary entry.
/// </remarks>
public sealed class DesignProperty
{
    /// <summary>
    /// Gets or sets the property name as it should be emitted into generated C# code.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primitive property value.
    /// </summary>
    public DesignPropertyValue Value { get; set; } = DesignPropertyValue.FromNull();
}
