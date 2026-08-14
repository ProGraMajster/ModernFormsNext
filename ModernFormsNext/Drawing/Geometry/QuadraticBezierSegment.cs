using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>Represents a quadratic Bézier segment with one control point.</summary>
public sealed class QuadraticBezierSegment : PathSegment
{
    private PointF controlPoint;
    private PointF point;

    /// <summary>Initializes a degenerate quadratic segment at the origin.</summary>
    public QuadraticBezierSegment()
    {
    }

    /// <summary>Initializes a quadratic segment.</summary>
    /// <param name="controlPoint">The finite control point in logical pixels.</param>
    /// <param name="point">The finite endpoint in logical pixels.</param>
    public QuadraticBezierSegment(PointF controlPoint, PointF point)
    {
        ValidatePoint(controlPoint, nameof(controlPoint));
        ValidatePoint(point, nameof(point));
        this.controlPoint = controlPoint;
        this.point = point;
    }

    /// <summary>Gets or sets the control point in logical pixels.</summary>
    public PointF ControlPoint
    {
        get => controlPoint;
        set
        {
            ValidatePoint(value, nameof(value));
            if (controlPoint == value)
                return;

            controlPoint = value;
            OnChanged();
        }
    }

    /// <summary>Gets or sets the endpoint in logical pixels.</summary>
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
