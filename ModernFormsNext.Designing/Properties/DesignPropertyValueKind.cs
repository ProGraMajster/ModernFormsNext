namespace ModernFormsNext.Designing;

/// <summary>
/// Identifies the primitive value kind stored by a designer property.
/// </summary>
public enum DesignPropertyValueKind
{
    /// <summary>
    /// Represents a JSON null value.
    /// </summary>
    Null,

    /// <summary>
    /// Represents a string value.
    /// </summary>
    String,

    /// <summary>
    /// Represents a Boolean value.
    /// </summary>
    Boolean,

    /// <summary>
    /// Represents a 32-bit integer value.
    /// </summary>
    Int32,

    /// <summary>
    /// Represents a double-precision floating point value.
    /// </summary>
    Double,

    /// <summary>
    /// Represents an enum member stored by name.
    /// </summary>
    Enum,

    /// <summary>
    /// Represents a small structured value with named primitive child values.
    /// </summary>
    Object
}
