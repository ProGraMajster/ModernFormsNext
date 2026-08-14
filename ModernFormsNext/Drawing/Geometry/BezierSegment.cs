using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>Represents a cubic Bézier segment with two control points.</summary>
public sealed class BezierSegment : PathSegment
{
    private PointF controlPoint1;
    private PointF controlPoint2;
    private PointF point;

    /// <summary>Initializes a degenerate cubic segment at the origin.</summary>
    public BezierSegment()
    {
    }

    /// <summary>Initializes a cubic Bézier segment.</summary>
    /// <param name="controlPoint1">The first finite control point.</param>
    /// <param name="controlPoint2">The second finite control point.</param>
    /// <param name="point">The finite endpoint.</param>
    public BezierSegment(PointF controlPoint1, PointF controlPoint2, PointF point)
    {
        ValidatePoint(controlPoint1, nameof(controlPoint1));
        ValidatePoint(controlPoint2, nameof(controlPoint2));
        ValidatePoint(point, nameof(point));
        this.controlPoint1 = controlPoint1;
        this.controlPoint2 = controlPoint2;
        this.point = point;
    }

    /// <summary>Gets or sets the first control point in logical pixels.</summary>
    public PointF ControlPoint1
    {
        get => controlPoint1;
        set
        {
            ValidatePoint(value, nameof(value));
            if (controlPoint1 == value)
                return;

            controlPoint1 = value;
            OnChanged();
        }
    }

    /// <summary>Gets or sets the second control point in logical pixels.</summary>
    public PointF ControlPoint2
    {
        get => controlPoint2;
        set
        {
            ValidatePoint(value, nameof(value));
            if (controlPoint2 == value)
                return;

            controlPoint2 = value;
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
