using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Drawing;

namespace ModernFormsNext;

/// <summary>Displays an automatically closed polygon from an observable point collection.</summary>
[DisplayName("Polygon")]
[Category("Shapes")]
[Description("Draws a closed polygon whose point collection can change at runtime.")]
public sealed class Polygon : Shape
{
    private readonly PathGeometry geometry = new();
    private PointCollection points = new();

    /// <summary>Initializes an empty polygon.</summary>
    public Polygon()
    {
        points.Changed += HandlePointsChanged;
        SetDefiningGeometry(geometry);
    }

    /// <summary>Gets or sets the observable ordered polygon points in local logical pixels.</summary>
    /// <remarks>
    /// Replacing or mutating the collection rebuilds and invalidates the cached path. Three or more
    /// points normally produce a filled area; fewer points remain deterministic and do not throw.
    /// </remarks>
    [Category("Geometry")]
    [Description("The ordered local points; the final point is automatically connected to the first.")]
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

    /// <summary>Gets or sets how overlapping polygon regions contribute to the fill.</summary>
    /// <value><see cref="GeometryFillRule.Winding"/> by default.</value>
    /// <remarks>
    /// The rule affects both fill rendering and geometry-aware fill hit testing. It does not
    /// change the explicitly closed stroke contour.
    /// </remarks>
    [Category("Geometry")]
    [Description("How overlapping polygon regions contribute to the fill and fill hit testing.")]
    [DefaultValue(GeometryFillRule.Winding)]
    public GeometryFillRule FillRule
    {
        get => geometry.FillRule;
        set => geometry.FillRule = value;
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

        var figure = new PathFigure(points[0], isClosed: true);
        for (int index = 1; index < points.Count; index++)
            figure.Segments.Add(new LineSegment(points[index]));
        geometry.Figures.Add(figure);
    }
}
