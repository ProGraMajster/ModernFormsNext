using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Drawing;

namespace ModernFormsNext;

/// <summary>Displays an ellipse fitted inside the control's current client bounds.</summary>
/// <remarks>
/// The contour is inset by half of <see cref="Shape.StrokeThickness"/> so a centered stroke and
/// its anti-aliased edge remain inside the control back buffer.
/// </remarks>
/// <example>
/// <code>
/// var ellipse = new Ellipse
/// {
///     Size = new Size(160, 90),
///     Fill = new SolidColorBrush(Color.CornflowerBlue),
///     Stroke = new SolidColorBrush(Color.Navy),
///     StrokeThickness = 2
/// };
/// </code>
/// </example>
[DisplayName("Ellipse")]
[Category("Shapes")]
[Description("Draws a scalable ellipse using shared Brush fill and stroke values.")]
public class Ellipse : Shape
{
    private readonly EllipseGeometry geometry = new();

    /// <summary>Initializes an ellipse whose geometry follows its client size.</summary>
    public Ellipse()
    {
        SetDefiningGeometry(geometry);
        UpdateGeometry();
    }

    /// <summary>Returns the stroke-safe logical ellipse bounds for the current control size.</summary>
    /// <param name="size">The current logical client size.</param>
    /// <returns>The finite bounds used to define the ellipse.</returns>
    protected virtual RectangleF GetEllipseBounds(Size size)
    {
        float inset = Math.Min(StrokeThickness / 2f, Math.Min(size.Width, size.Height) / 2f);
        return new RectangleF(
            inset,
            inset,
            Math.Max(0f, size.Width - (inset * 2f)),
            Math.Max(0f, size.Height - (inset * 2f)));
    }

    /// <inheritdoc/>
    protected override void OnStrokeThicknessChanged(EventArgs e)
    {
        UpdateGeometry();
        base.OnStrokeThicknessChanged(e);
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(EventArgs e)
    {
        UpdateGeometry();
        base.OnSizeChanged(e);
    }

    private void UpdateGeometry()
        => geometry.Rect = GetEllipseBounds(ClientSize);
}
