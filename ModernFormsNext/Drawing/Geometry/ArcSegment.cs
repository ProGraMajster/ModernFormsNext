using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>Represents an endpoint-parameterized elliptical arc within a <see cref="PathFigure"/>.</summary>
/// <remarks>
/// Coordinates and radii use logical pixels; rotation uses degrees. The preceding segment supplies
/// the start point. Equal endpoints omit the arc, a zero radius produces a line, and radii too small
/// to connect the endpoints are enlarged uniformly by the shared converter. Mutations synchronously
/// notify every owning figure; mutate on the UI thread while the geometry is rendered.
/// </remarks>
/// <example><code>
/// var arc = new ArcSegment(new PointF(120, 60), new SizeF(60, 30),
///     rotationAngle: 20, isLargeArc: false, sweepDirection: SweepDirection.Clockwise);
/// </code></example>
public sealed class ArcSegment : PathSegment
{
    private PointF point;
    private SizeF size;
    private float rotationAngle;
    private bool isLargeArc;
    private SweepDirection sweepDirection = SweepDirection.Clockwise;

    /// <summary>Initializes a degenerate arc at the origin.</summary>
    public ArcSegment() { }

    /// <summary>Initializes a finite elliptical arc.</summary>
    /// <param name="point">The endpoint in logical pixels.</param>
    /// <param name="size">The finite, non-negative X/Y radii in logical pixels.</param>
    /// <param name="rotationAngle">The finite ellipse-axis rotation in degrees.</param>
    /// <param name="isLargeArc">Whether to select the arc spanning more than 180 degrees.</param>
    /// <param name="sweepDirection">The direction in the downward-Y coordinate system.</param>
    public ArcSegment(PointF point, SizeF size, float rotationAngle = 0,
        bool isLargeArc = false, SweepDirection sweepDirection = SweepDirection.Clockwise)
    {
        ValidatePoint(point, nameof(point));
        Geometry.ValidateSize(size, nameof(size));
        ValidateRotation(rotationAngle);
        ValidateDirection(sweepDirection);
        this.point = point;
        this.size = size;
        this.rotationAngle = rotationAngle;
        this.isLargeArc = isLargeArc;
        this.sweepDirection = sweepDirection;
    }

    /// <summary>Gets or sets the finite endpoint in logical pixels.</summary>
    public PointF Point
    {
        get => point;
        set
        {
            ValidatePoint(value, nameof(value));
            if (point == value) return;
            point = value;
            OnChanged();
        }
    }

    /// <summary>Gets or sets the finite, non-negative X/Y ellipse radii in logical pixels.</summary>
    /// <remarks>A zero radius makes this a straight segment.</remarks>
    public SizeF Size
    {
        get => size;
        set
        {
            Geometry.ValidateSize(value, nameof(value));
            if (size == value) return;
            size = value;
            OnChanged();
        }
    }

    /// <summary>Gets or sets the finite ellipse-axis rotation in degrees.</summary>
    public float RotationAngle
    {
        get => rotationAngle;
        set
        {
            ValidateRotation(value);
            if (rotationAngle == value) return;
            rotationAngle = value;
            OnChanged();
        }
    }

    /// <summary>Gets or sets whether the arc spanning more than 180 degrees is selected.</summary>
    public bool IsLargeArc
    {
        get => isLargeArc;
        set
        {
            if (isLargeArc == value) return;
            isLargeArc = value;
            OnChanged();
        }
    }

    /// <summary>Gets or sets the arc direction. The default is clockwise.</summary>
    public SweepDirection SweepDirection
    {
        get => sweepDirection;
        set
        {
            ValidateDirection(value);
            if (sweepDirection == value) return;
            sweepDirection = value;
            OnChanged();
        }
    }

    private static void ValidateRotation(float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "Arc rotation must be finite.");
    }

    private static void ValidateDirection(SweepDirection value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), "The sweep direction is not defined.");
    }
}
