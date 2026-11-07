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

        // Use an explicit stack: a valid shared geometry graph need not be shallow.
        // Each frame owns its native path until ownership is transferred to its parent.
        var frames = new Stack<ConversionFrame>();
        frames.Push(new(geometry));
        try
        {
            while (frames.Count > 0)
            {
                ConversionFrame frame = frames.Peek();
                Geometry? child = frame.NextChild();
                if (child is not null)
                {
                    frames.Push(new(child));
                    continue;
                }

                frame.Complete();
                if (frames.Count == 1)
                {
                    if (scale.Width != 1f || scale.Height != 1f)
                        frame.Path.Transform(new SKMatrix(scale.Width, 0f, 0f, 0f, scale.Height, 0f, 0f, 0f, 1f));
                    RequireFinite(frame.Path);
                    frames.Pop();
                    return frame.Path;
                }
                frames.Pop();
                using (frame.Path) frames.Peek().Accept(frame.Path);
            }
        }
        catch
        {
            foreach (var frame in frames) frame.Dispose();
            throw;
        }
        throw new InvalidOperationException("A geometry conversion did not produce a path.");
    }

    private static SKPath CreatePrimitive(Geometry geometry)
    {
        var path = new SKPath();
        try
        {
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

            return path;
        }
        catch { path.Dispose(); throw; }
    }

    private sealed class ConversionFrame : IDisposable
    {
        private readonly Geometry geometry;
        private int childIndex;
        private SKPath? first;
        private SKPath? second;

        internal ConversionFrame(Geometry geometry)
        {
            this.geometry = geometry;
            Path = CreatePrimitive(geometry);
            if (geometry is GeometryGroup group)
                Path.FillType = group.FillRule == GeometryFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        }
        internal SKPath Path { get; }
        internal Geometry? NextChild()
        {
            if (geometry is GeometryGroup group)
                return childIndex < group.Children.Count ? group.Children[childIndex++] : null;
            if (geometry is CombinedGeometry combined)
            {
                while (childIndex < 2)
                {
                    Geometry? child = childIndex++ == 0 ? combined.Geometry1 : combined.Geometry2;
                    if (child is not null) return child;
                }
            }
            return null;
        }
        internal void Accept(SKPath child)
        {
            if (geometry is GeometryGroup) Path.AddPath(child);
            else if (childIndex == 1) first = new SKPath(child);
            else second = new SKPath(child);
        }
        internal void Complete()
        {
            if (geometry is CombinedGeometry combined)
            {
                using SKPath left = first ?? new SKPath();
                using SKPath right = second ?? new SKPath();
                first = second = null;
                SKPathOp operation = combined.Mode switch
                {
                    GeometryCombineMode.Union => SKPathOp.Union,
                    GeometryCombineMode.Intersect => SKPathOp.Intersect,
                    GeometryCombineMode.Xor => SKPathOp.Xor,
                    _ => SKPathOp.Difference
                };
                if (!left.Op(right, operation, Path))
                    throw new InvalidOperationException("The filled geometry combination could not be computed.");
            }
            if (!geometry.Transform.IsIdentity) Path.Transform(ToSkiaMatrix(geometry.Transform));
            RequireFinite(Path);
        }
        public void Dispose() { Path.Dispose(); first?.Dispose(); second?.Dispose(); }
    }

    private static void RequireFinite(SKPath path)
    {
        SKRect bounds = path.Bounds;
        if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) ||
            !float.IsFinite(bounds.Right) || !float.IsFinite(bounds.Bottom) ||
            !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height))
            throw new ArgumentException("Geometry coordinates overflowed during conversion.");
        // Skia may report empty bounds for a path containing a non-finite control point. Inspect
        // stored points without allocating an array, before area operations or publishing a cache.
        for (int i = 0; i < path.PointCount; i++)
        {
            SKPoint point = path.GetPoint(i);
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                throw new ArgumentException("Geometry coordinates overflowed during conversion.");
        }
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
                    case ArcSegment arc:
                        // Endpoint arcs omit coincident endpoints and use a line for a zero radius.
                        // Skia performs the required uniform radius correction for the remaining case.
                        if (path.LastPoint == new SKPoint(arc.Point.X, arc.Point.Y)) break;
                        if (arc.Size.Width == 0 || arc.Size.Height == 0)
                            path.LineTo(arc.Point.X, arc.Point.Y);
                        else
                            path.ArcTo(arc.Size.Width, arc.Size.Height, arc.RotationAngle % 360f,
                                arc.IsLargeArc ? SKPathArcSize.Large : SKPathArcSize.Small,
                                arc.SweepDirection == SweepDirection.Clockwise ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise,
                                arc.Point.X, arc.Point.Y);
                        break;
                }
            }

            if (figure.IsClosed)
                path.Close();
        }
    }
}
