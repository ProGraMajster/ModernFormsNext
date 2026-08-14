using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

/// <summary>Displays a stroked line between two points in local logical coordinates.</summary>
[DisplayName("Line")]
[Category("Shapes")]
[Description("Draws a straight vector line between two local points.")]
public sealed class Line : Shape
{
    private readonly LineGeometry geometry = new();

    /// <summary>Initializes a centered horizontal line from (8,20) to (112,20).</summary>
    public Line()
    {
        geometry.StartPoint = new PointF(8f, 20f);
        geometry.EndPoint = new PointF(112f, 20f);
        SetDefiningGeometry(geometry);
    }

    /// <summary>Gets or sets the starting point in local logical pixels.</summary>
    [Category("Geometry")]
    [Description("The starting point in local logical pixels.")]
    public PointF StartPoint
    {
        get => geometry.StartPoint;
        set => geometry.StartPoint = value;
    }

    /// <summary>Gets or sets the ending point in local logical pixels.</summary>
    [Category("Geometry")]
    [Description("The ending point in local logical pixels.")]
    public PointF EndPoint
    {
        get => geometry.EndPoint;
        set => geometry.EndPoint = value;
    }

    /// <summary>Gets no fill because an open line has no fill semantics.</summary>
    /// <remarks>Assignments are ignored; use <see cref="Shape.Stroke"/> to render a line.</remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override MfnBrush? Fill
    {
        get => null;
        set { }
    }

    /// <summary>Gets the default line control size.</summary>
    protected override Size DefaultSize => new(120, 40);
}
