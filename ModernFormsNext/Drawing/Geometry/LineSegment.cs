using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>Represents a straight segment ending at a point.</summary>
public sealed class LineSegment : PathSegment
{
    private PointF point;

    /// <summary>Initializes a line segment ending at the origin.</summary>
    public LineSegment()
    {
    }

    /// <summary>Initializes a line segment ending at the supplied point.</summary>
    /// <param name="point">The finite endpoint in logical pixels.</param>
    public LineSegment(PointF point)
    {
        ValidatePoint(point, nameof(point));
        this.point = point;
    }

    /// <summary>Gets or sets the finite endpoint in logical pixels.</summary>
    public PointF Point
    {
        get => point;
        set
        {
            ValidatePoint(value, nameof(value));
            if (point == value)
                return;

            point = value;
            OnChanged();
        }
    }
}
