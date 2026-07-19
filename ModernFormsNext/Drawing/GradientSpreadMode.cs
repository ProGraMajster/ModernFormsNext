namespace ModernFormsNext.Drawing;

/// <summary>
/// Specifies how a gradient paints positions outside its first and last stop.
/// </summary>
public enum GradientSpreadMode
{
    /// <summary>
    /// Extends the nearest edge color before the first stop and after the last stop.
    /// </summary>
    Pad,

    /// <summary>
    /// Repeats the gradient in the same direction for every interval.
    /// </summary>
    Repeat,

    /// <summary>
    /// Repeats the gradient while mirroring every alternate interval.
    /// </summary>
    Reflect
}
