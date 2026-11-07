namespace ModernFormsNext.Drawing;

/// <summary>Specifies a Boolean operation on filled geometry areas, excluding stroke widths.</summary>
public enum GeometryCombineMode
{
    /// <summary>Includes the area filled by either operand.</summary>
    Union,
    /// <summary>Includes only the area filled by both operands.</summary>
    Intersect,
    /// <summary>Includes the area filled by exactly one operand.</summary>
    Xor,
    /// <summary>Includes the first operand's area with the second operand's area removed.</summary>
    Exclude
}
