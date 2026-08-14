namespace ModernFormsNext.Drawing;

/// <summary>
/// Specifies how overlapping contours determine the filled area of a <see cref="PathGeometry"/>
/// or <see cref="ModernFormsNext.Polygon"/>.
/// </summary>
public enum GeometryFillRule
{
    /// <summary>Uses the non-zero winding rule.</summary>
    Winding,

    /// <summary>Fills regions crossed by an odd number of contour edges.</summary>
    EvenOdd
}
