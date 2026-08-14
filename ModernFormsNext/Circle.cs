using System.ComponentModel;
using System.Drawing;

namespace ModernFormsNext;

/// <summary>Displays a circle centered inside the control's client bounds.</summary>
/// <remarks>
/// The outer edge of the centered stroke fits the smaller client dimension. The contour diameter
/// therefore equals that dimension minus <see cref="Shape.StrokeThickness"/>. This subclass reuses
/// the complete <see cref="Ellipse"/> rendering, Brush, transform, and hit-testing implementation.
/// </remarks>
[DisplayName("Circle")]
[Category("Shapes")]
[Description("Draws a centered circle constrained by the smaller control dimension.")]
public sealed class Circle : Ellipse
{
    /// <summary>Initializes a centered circle.</summary>
    public Circle()
    {
    }

    /// <inheritdoc/>
    protected override RectangleF GetEllipseBounds(Size size)
    {
        float diameter = Math.Max(0f, Math.Min(size.Width, size.Height) - StrokeThickness);
        return new RectangleF(
            (size.Width - diameter) / 2f,
            (size.Height - diameter) / 2f,
            diameter,
            diameter);
    }
}
