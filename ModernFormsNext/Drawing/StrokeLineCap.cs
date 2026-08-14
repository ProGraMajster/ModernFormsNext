namespace ModernFormsNext.Drawing;

/// <summary>Specifies how the open ends of a stroked vector contour are drawn.</summary>
public enum StrokeLineCap
{
    /// <summary>Ends the stroke exactly at the endpoint.</summary>
    Flat,

    /// <summary>Extends the endpoint with a semicircle whose radius is half the stroke thickness.</summary>
    Round,

    /// <summary>Extends the endpoint with a square whose length is half the stroke thickness.</summary>
    Square
}
