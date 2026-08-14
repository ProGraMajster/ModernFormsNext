using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>
/// Represents an ellipse bounded by an axis-aligned rectangle in logical coordinates.
/// </summary>
public sealed class EllipseGeometry : Geometry
{
    private RectangleF rect;

    /// <summary>Initializes an empty ellipse.</summary>
    public EllipseGeometry()
    {
    }

    /// <summary>Initializes an ellipse inside the supplied finite rectangle.</summary>
    /// <param name="rect">The bounding rectangle in logical pixels.</param>
    public EllipseGeometry(RectangleF rect)
    {
        Geometry.ValidateRectangle(rect, nameof(rect));
        this.rect = rect;
    }

    /// <summary>Gets or sets the ellipse bounding rectangle in logical pixels.</summary>
    public RectangleF Rect
    {
        get => rect;
        set
        {
            Geometry.ValidateRectangle(value, nameof(value));
            if (rect == value)
                return;

            rect = value;
            OnChanged();
        }
    }
}
