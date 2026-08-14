namespace ModernFormsNext.Drawing;

/// <summary>Specifies how consecutive stroked vector segments are joined.</summary>
public enum StrokeLineJoin
{
    /// <summary>Extends segment edges until they meet, subject to the shape's miter limit.</summary>
    Miter,

    /// <summary>Rounds the outside of the join.</summary>
    Round,

    /// <summary>Bevels the outside of the join.</summary>
    Bevel
}
