using System.Drawing;

namespace ModernFormsNext.Drawing;

/// <summary>
/// Represents one path contour with a start point, ordered segments, and optional closure.
/// </summary>
public sealed class PathFigure
{
    private PointF startPoint;
    private bool isClosed;

    /// <summary>Initializes a figure at the origin.</summary>
    public PathFigure()
    {
        Segments.Changed += HandleSegmentsChanged;
    }

    /// <summary>Initializes a figure at the supplied finite start point.</summary>
    /// <param name="startPoint">The start point in logical pixels.</param>
    /// <param name="isClosed">Whether rendering closes the final segment to the start point.</param>
    public PathFigure(PointF startPoint, bool isClosed = false)
        : this()
    {
        Geometry.ValidatePoint(startPoint, nameof(startPoint));
        this.startPoint = startPoint;
        this.isClosed = isClosed;
    }

    /// <summary>Occurs after the contour geometry changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets or sets the finite start point in logical pixels.</summary>
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

    /// <summary>Gets the observable ordered segment collection.</summary>
    public PathSegmentCollection Segments { get; } = new();

    /// <summary>
    /// Gets or sets whether the contour is explicitly closed to <see cref="StartPoint"/>.
    /// </summary>
    /// <remarks>
    /// Closure affects stroking as well as filling. Open contours can still contribute a filled
    /// area because standard fill rules treat their endpoints as implicitly connected.
    /// </remarks>
    public bool IsClosed
    {
        get => isClosed;
        set
        {
            if (isClosed == value)
                return;

            isClosed = value;
            OnChanged();
        }
    }

    private void HandleSegmentsChanged(object? sender, EventArgs e) => OnChanged();

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
