using System.Drawing;
using System.Numerics;
using ModernFormsNext;
using ModernFormsNext.Drawing;

namespace ControlGallery.Panels;

/// <summary>
/// Provides manual visual smoke tests for vector controls, reusable geometry, strokes, transforms,
/// and stroke-safe control bounds.
/// </summary>
public sealed class ShapesPanel : BasePanel
{
    /// <summary>Initializes the vector geometry gallery.</summary>
    public ShapesPanel()
    {
        AutoScroll = true;
        Controls.Add(new Label
        {
            Left = 24,
            Top = 18,
            Width = 760,
            Height = 30,
            Text = "Shapes and vector geometry",
            Font = new ModernFormsNext.Font("Segoe UI", 16)
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 52,
            Width = 780,
            Height = 42,
            Multiline = true,
            Text = "All samples use the shared Brush and Skia control pipeline. Transparent corners and thin strokes are intended for geometry-aware pointer-routing checks."
        });

        AddEllipseCard(24, 106);
        AddCircleCard(280, 106);
        AddLineCard(536, 106);
        AddPolygonCard(24, 278);
        AddPolylineCard(280, 278);
        AddPathCard(536, 278);

        Controls.Add(new Label
        {
            Left = 24,
            Top = 458,
            Width = 756,
            Height = 72,
            Multiline = true,
            Text = "Manual checks: resize and dock the gallery, verify smooth solid/linear/radial/sweep fills, round caps and joins, the rotated Path, and complete anti-aliased strokes inside every card. On Android, also verify density-correct stroke widths and touch targeting."
        });
    }

    private void AddEllipseCard(int left, int top)
    {
        Panel card = AddCard(left, top, "Ellipse / linear fill");
        card.Controls.Add(new Ellipse
        {
            Left = 24,
            Top = 14,
            Width = 184,
            Height = 94,
            Fill = Linear(Color.CornflowerBlue, Color.MediumPurple),
            Stroke = new SolidColorBrush(Color.MidnightBlue),
            StrokeThickness = 4
        });
    }

    private void AddCircleCard(int left, int top)
    {
        Panel card = AddCard(left, top, "Circle / radial fill");
        var fill = new RadialGradientBrush
        {
            CenterPoint = new PointF(0.35f, 0.3f),
            GradientOrigin = new PointF(0.2f, 0.2f),
            Radius = 0.72f
        };
        fill.GradientStops.AddRange([
            new GradientStop(Color.White, 0),
            new GradientStop(Color.DeepSkyBlue, 0.45f),
            new GradientStop(Color.MidnightBlue, 1)
        ]);
        card.Controls.Add(new Circle
        {
            Left = 64,
            Top = 8,
            Width = 104,
            Height = 104,
            Fill = fill,
            Stroke = new SolidColorBrush(Color.Navy),
            StrokeThickness = 3
        });
    }

    private void AddLineCard(int left, int top)
    {
        Panel card = AddCard(left, top, "Line / caps + gradient stroke");
        var stroke = Linear(Color.Crimson, Color.Gold);
        card.Controls.Add(new Line
        {
            Left = 14,
            Top = 16,
            Width = 204,
            Height = 40,
            StartPoint = new PointF(10, 20),
            EndPoint = new PointF(194, 20),
            Stroke = stroke,
            StrokeThickness = 12,
            StrokeLineCap = StrokeLineCap.Round
        });
        card.Controls.Add(new Line
        {
            Left = 14,
            Top = 65,
            Width = 204,
            Height = 40,
            StartPoint = new PointF(10, 20),
            EndPoint = new PointF(194, 20),
            Stroke = new SolidColorBrush(Color.RoyalBlue),
            StrokeThickness = 8,
            StrokeLineCap = StrokeLineCap.Square
        });
    }

    private void AddPolygonCard(int left, int top)
    {
        Panel card = AddCard(left, top, "Polygon / bevel join");
        card.Controls.Add(new Polygon
        {
            Left = 38,
            Top = 8,
            Width = 156,
            Height = 104,
            Points = [new(78, 4), new(148, 100), new(4, 42), new(152, 42), new(8, 100)],
            Fill = new SolidColorBrush(Color.FromArgb(180, Color.Gold)),
            Stroke = new SolidColorBrush(Color.DarkOrange),
            StrokeThickness = 5,
            StrokeLineJoin = StrokeLineJoin.Bevel
        });
    }

    private void AddPolylineCard(int left, int top)
    {
        Panel card = AddCard(left, top, "Polyline / round join");
        var stroke = new SweepGradientBrush { CenterPoint = new PointF(0.5f, 0.5f) };
        stroke.GradientStops.AddRange([
            new GradientStop(Color.MediumSeaGreen, 0),
            new GradientStop(Color.RoyalBlue, 0.5f),
            new GradientStop(Color.MediumPurple, 1)
        ]);
        card.Controls.Add(new Polyline
        {
            Left = 12,
            Top = 8,
            Width = 208,
            Height = 104,
            Points = [new(6, 84), new(42, 20), new(82, 76), new(122, 16), new(162, 72), new(202, 24)],
            Stroke = stroke,
            StrokeThickness = 9,
            StrokeLineCap = StrokeLineCap.Round,
            StrokeLineJoin = StrokeLineJoin.Round
        });
    }

    private void AddPathCard(int left, int top)
    {
        Panel card = AddCard(left, top, "Path / vector transform");
        var geometry = new PathGeometry
        {
            // Transform the vector before rasterization so Skia anti-aliases the final contour.
            // A Control.Rotation would instead rotate the already-rendered control backbuffer.
            Transform = Matrix3x2.CreateRotation(-7f * (System.MathF.PI / 180f), new Vector2(106f, 52f))
        };
        var figure = new PathFigure(new PointF(6, 78), isClosed: true);
        figure.Segments.Add(new QuadraticBezierSegment(new PointF(38, 4), new PointF(86, 30)));
        figure.Segments.Add(new BezierSegment(new PointF(126, 4), new PointF(180, 14), new PointF(204, 84)));
        geometry.Figures.Add(figure);
        card.Controls.Add(new ModernFormsNext.Path
        {
            Left = 10,
            Top = 8,
            Width = 212,
            Height = 104,
            Data = geometry,
            Fill = Linear(Color.HotPink, Color.Orange),
            Stroke = new SolidColorBrush(Color.DarkRed),
            StrokeThickness = 4
        });
    }

    private Panel AddCard(int left, int top, string caption)
    {
        var card = Controls.Add(new Panel
        {
            Left = left,
            Top = top,
            Width = 232,
            Height = 152
        });
        card.Style.Border.Width = 1;
        card.Style.Border.Color = Theme.BorderMidColor;
        card.Controls.Add(new Label
        {
            Left = 10,
            Top = 120,
            Width = 210,
            Height = 24,
            Text = caption
        });
        return card;
    }

    private static LinearGradientBrush Linear(Color start, Color end)
    {
        var brush = new LinearGradientBrush { Start = new PointF(0, 0), End = new PointF(1, 1) };
        brush.GradientStops.AddRange([new GradientStop(start, 0), new GradientStop(end, 1)]);
        return brush;
    }
}
