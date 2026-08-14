using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>
/// Represents a reusable axis-aligned rectangle in logical coordinates.
/// </summary>
public sealed class RectangleGeometry : Geometry
{
    private RectangleF rect;

    /// <summary>Initializes an empty rectangle.</summary>
    public RectangleGeometry()
    {
    }

    /// <summary>Initializes a rectangle geometry with finite, non-negative dimensions.</summary>
    /// <param name="rect">The rectangle in logical pixels.</param>
    public RectangleGeometry(RectangleF rect)
    {
        Geometry.ValidateRectangle(rect, nameof(rect));
        this.rect = rect;
    }

    /// <summary>Gets or sets the rectangle in logical pixels.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a dimension is negative, NaN, or infinity.
    /// </exception>
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
