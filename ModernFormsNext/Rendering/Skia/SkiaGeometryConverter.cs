using System.Drawing;
using System.Numerics;
using ModernFormsNext.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Rendering.Skia;

/// <summary>Converts platform-neutral geometry to an owned Skia path.</summary>
internal static class SkiaGeometryConverter
{
    public static SKPath CreatePath(Geometry geometry, SizeF scale)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (!float.IsFinite(scale.Width) || !float.IsFinite(scale.Height) ||
            scale.Width <= 0f || scale.Height <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Geometry scale must be finite and positive.");
        }

        var path = new SKPath();
        switch (geometry)
        {
            case LineGeometry line:
                path.MoveTo(line.StartPoint.X, line.StartPoint.Y);
                path.LineTo(line.EndPoint.X, line.EndPoint.Y);
                break;

            case RectangleGeometry rectangle when rectangle.Rect.Width > 0f && rectangle.Rect.Height > 0f:
                path.AddRect(ToSkiaRect(rectangle.Rect));
                break;

            case EllipseGeometry ellipse when ellipse.Rect.Width > 0f && ellipse.Rect.Height > 0f:
                path.AddOval(ToSkiaRect(ellipse.Rect));
                break;

            case PathGeometry pathGeometry:
                AppendPathGeometry(path, pathGeometry);
                break;
        }

        if (!geometry.Transform.IsIdentity)
            path.Transform(ToSkiaMatrix(geometry.Transform));
        if (scale.Width != 1f || scale.Height != 1f)
            path.Transform(new SKMatrix(scale.Width, 0f, 0f, 0f, scale.Height, 0f, 0f, 0f, 1f));

        return path;
    }

    internal static SKMatrix ToSkiaMatrix(Matrix3x2 transform)
        => new(
            transform.M11,
            transform.M21,
            transform.M31,
            transform.M12,
            transform.M22,
            transform.M32,
            0f,
            0f,
            1f);

    private static SKRect ToSkiaRect(RectangleF rectangle)
        => new(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

    private static void AppendPathGeometry(SKPath path, PathGeometry geometry)
    {
        path.FillType = geometry.FillRule == GeometryFillRule.EvenOdd
            ? SKPathFillType.EvenOdd
            : SKPathFillType.Winding;

        foreach (PathFigure figure in geometry.Figures)
        {
            path.MoveTo(figure.StartPoint.X, figure.StartPoint.Y);
            foreach (PathSegment segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment line:
                        path.LineTo(line.Point.X, line.Point.Y);
                        break;
                    case QuadraticBezierSegment quadratic:
                        path.QuadTo(
                            quadratic.ControlPoint.X,
                            quadratic.ControlPoint.Y,
                            quadratic.Point.X,
                            quadratic.Point.Y);
                        break;
                    case BezierSegment cubic:
                        path.CubicTo(
                            cubic.ControlPoint1.X,
                            cubic.ControlPoint1.Y,
                            cubic.ControlPoint2.X,
                            cubic.ControlPoint2.Y,
                            cubic.Point.X,
                            cubic.Point.Y);
                        break;
                }
            }

            if (figure.IsClosed)
                path.Close();
        }
    }
}
