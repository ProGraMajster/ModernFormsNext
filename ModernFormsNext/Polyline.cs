using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

/// <summary>Displays an open polyline from an observable point collection.</summary>
/// <remarks>
/// The contour remains open and has stroke-only semantics. Assignments to
/// <see cref="Shape.Fill"/> are ignored so an open polyline never acquires a filled hit-test area
/// through the renderer's implicit fill closure.
/// </remarks>
[DisplayName("Polyline")]
[Category("Shapes")]
[Description("Draws an open vector polyline whose point collection can change at runtime.")]
public sealed class Polyline : Shape
{
    private readonly PathGeometry geometry = new();
    private PointCollection points = new();

    /// <summary>Initializes an empty polyline.</summary>
    public Polyline()
    {
        points.Changed += HandlePointsChanged;
        SetDefiningGeometry(geometry);
    }

    /// <summary>Gets no fill because a polyline has open, stroke-only semantics.</summary>
    /// <remarks>Assignments are ignored; use <see cref="Shape.Stroke"/> to render a polyline.</remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override MfnBrush? Fill
    {
        get => null;
        set { }
    }

    /// <summary>Gets or sets the observable ordered polyline points in local logical pixels.</summary>
    [Category("Geometry")]
    [Description("The ordered local points; the contour is not closed for stroking.")]
    public PointCollection Points
    {
        get => points;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(points, value))
                return;

            points.Changed -= HandlePointsChanged;
            points = value;
            points.Changed += HandlePointsChanged;
            RebuildGeometry();
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        points.Changed -= HandlePointsChanged;
        base.Dispose(disposing);
    }

    private void HandlePointsChanged(object? sender, EventArgs e) => RebuildGeometry();

    private void RebuildGeometry()
    {
        geometry.Figures.Clear();
        if (points.Count == 0)
            return;

        var figure = new PathFigure(points[0]);
        for (int index = 1; index < points.Count; index++)
            figure.Segments.Add(new LineSegment(points[index]));
        geometry.Figures.Add(figure);
    }
}
