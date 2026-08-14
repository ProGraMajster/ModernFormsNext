using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>
/// Represents a reusable straight line between two points in logical coordinates.
/// </summary>
public sealed class LineGeometry : Geometry
{
    private PointF startPoint;
    private PointF endPoint;

    /// <summary>Initializes an empty line whose endpoints are both at the origin.</summary>
    public LineGeometry()
    {
    }

    /// <summary>Initializes a line with the supplied finite endpoints.</summary>
    /// <param name="startPoint">The starting point in logical pixels.</param>
    /// <param name="endPoint">The ending point in logical pixels.</param>
    public LineGeometry(PointF startPoint, PointF endPoint)
    {
        Geometry.ValidatePoint(startPoint, nameof(startPoint));
        Geometry.ValidatePoint(endPoint, nameof(endPoint));
        this.startPoint = startPoint;
        this.endPoint = endPoint;
    }

    /// <summary>Gets or sets the starting point in logical pixels.</summary>
    public PointF StartPoint
    {
        get => startPoint;
        set
        {
            Geometry.ValidatePoint(value, nameof(value));
            if (startPoint == value)
                return;

            startPoint = value;
            OnChanged();
        }
    }

    /// <summary>Gets or sets the ending point in logical pixels.</summary>
    public PointF EndPoint
    {
        get => endPoint;
        set
        {
            Geometry.ValidatePoint(value, nameof(value));
            if (endPoint == value)
                return;

            endPoint = value;
            OnChanged();
        }
    }
}
