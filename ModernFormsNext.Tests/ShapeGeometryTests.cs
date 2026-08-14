using System.Drawing;
using System.Numerics;
using ModernFormsNext.Drawing;
using ModernFormsNext.Rendering.Skia;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ShapeGeometryTests
{
    [Fact]
    public void EllipseFollowsCurrentBoundsAndRejectsCornerHits()
    {
        using var shape = Filled(new Ellipse { Size = new Size(100, 60) });

        Assert.True(shape.HitTestClient(new PointF(50, 30)));
        Assert.False(shape.HitTestClient(new PointF(2, 2)));
    }

    [Fact]
    public void CircleUsesTheSmallerCenteredDimension()
    {
        using var shape = Filled(new Circle { Size = new Size(120, 80) });

        Assert.False(shape.HitTestClient(new PointF(5, 40)));
        Assert.True(shape.HitTestClient(new PointF(60, 40)));

        shape.Size = new Size(80, 120);
        Assert.False(shape.HitTestClient(new PointF(40, 5)));
        Assert.True(shape.HitTestClient(new PointF(40, 60)));

        shape.Size = new Size(45, 20);
        Assert.False(shape.HitTestClient(new PointF(3, 10)));
        Assert.True(shape.HitTestClient(new PointF(22.5f, 10)));

        using var stroked = Stroked(new Circle { Size = new Size(120, 80), StrokeThickness = 6 });
        Assert.True(Render(stroked, 60, 5).Alpha > 0);
    }

    [Fact]
    public void LineGeometryPreservesEndpoints()
    {
        var geometry = new LineGeometry(new PointF(-3, 7), new PointF(41, 19));
        using SKPath path = SkiaGeometryConverter.CreatePath(geometry, new SizeF(1, 1));

        Assert.Equal(new SKRect(-3, 7, 41, 19), path.Bounds);
    }

    [Fact]
    public void PolygonClosesItsStrokeButPolylineDoesNot()
    {
        PointCollection points = [new(10, 10), new(90, 10), new(90, 90)];
        using var polygon = Stroked(new Polygon { Size = new Size(100, 100), Points = points });
        using var polyline = Stroked(new Polyline { Size = new Size(100, 100), Points = new PointCollection(points) });

        Assert.True(polygon.HitTestClient(new PointF(50, 50)));
        Assert.False(polyline.HitTestClient(new PointF(50, 50)));
    }

    [Fact]
    public void PolylineIgnoresFillInsteadOfCreatingAnImplicitlyClosedHitArea()
    {
        using var shape = new Polyline
        {
            Size = new Size(100, 100),
            Points = [new(10, 10), new(90, 10), new(90, 90)],
            Fill = new SolidColorBrush(Color.CornflowerBlue)
        };

        Assert.Null(shape.Fill);
        Assert.False(shape.HitTestClient(new PointF(60, 40)));
        Assert.Equal(0, Render(shape, 60, 40).Alpha);
    }

    [Fact]
    public void PointCollectionPreservesOrderAndRaisesOneAddRangeChange()
    {
        var points = new PointCollection();
        int changed = 0;
        points.Changed += (_, _) => changed++;

        points.AddRange([new(9, 2), new(4, 8), new(-1, 3)]);

        Assert.Equal(1, changed);
        Assert.Equal([new PointF(9, 2), new PointF(4, 8), new PointF(-1, 3)], points);
    }

    [Fact]
    public void PathRendersReusableGeometry()
    {
        var geometry = TriangleGeometry();
        using var shape = Filled(new ModernFormsNext.Path { Size = new Size(100, 100), Data = geometry });

        Assert.True(shape.HitTestClient(new PointF(50, 40)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NullAndNoBrushFillLeaveDestinationTransparent(bool useNull)
    {
        using var shape = new Ellipse
        {
            Size = new Size(40, 40),
            Fill = useNull ? null : new NoBrush()
        };

        Assert.Equal(0, Render(shape, 20, 20).Alpha);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NullAndNoBrushStrokeLeaveDestinationTransparent(bool useNull)
    {
        using var shape = new Ellipse
        {
            Size = new Size(40, 40),
            Stroke = useNull ? null : new NoBrush(),
            StrokeThickness = 8
        };

        Assert.Equal(0, Render(shape, 20, 2).Alpha);
    }

    [Fact]
    public void StrokeThicknessChangesLineHitArea()
    {
        using var thin = Stroked(new Line { Size = new Size(120, 40) });
        using var thick = Stroked(new Line { Size = new Size(120, 40), StrokeThickness = 16 });

        Assert.False(thin.HitTestClient(new PointF(60, 29)));
        Assert.True(thick.HitTestClient(new PointF(60, 29)));
    }

    [Theory]
    [InlineData(StrokeLineCap.Flat, SKStrokeCap.Butt)]
    [InlineData(StrokeLineCap.Round, SKStrokeCap.Round)]
    [InlineData(StrokeLineCap.Square, SKStrokeCap.Square)]
    public void LineCapsMapWithoutLeakingSkia(StrokeLineCap source, SKStrokeCap expected)
        => Assert.Equal(expected, SkiaShapeRenderer.MapLineCap(source));

    [Theory]
    [InlineData(StrokeLineJoin.Miter, SKStrokeJoin.Miter)]
    [InlineData(StrokeLineJoin.Round, SKStrokeJoin.Round)]
    [InlineData(StrokeLineJoin.Bevel, SKStrokeJoin.Bevel)]
    public void LineJoinsMapWithoutLeakingSkia(StrokeLineJoin source, SKStrokeJoin expected)
        => Assert.Equal(expected, SkiaShapeRenderer.MapLineJoin(source));

    [Fact]
    public void SolidBrushFillUsesSharedBrushRendering()
    {
        using var shape = new Ellipse
        {
            Size = new Size(40, 40),
            Fill = new SolidColorBrush(Color.CornflowerBlue)
        };

        Assert.Equal(new SKColor(100, 149, 237), Render(shape, 20, 20));
    }

    [Fact]
    public void GradientFillRendersAcrossGeometryBounds()
    {
        var fill = Gradient(new LinearGradientBrush { Start = new PointF(0, 0), End = new PointF(1, 0) });
        using var shape = new Ellipse { Size = new Size(80, 40), Fill = fill };

        Assert.True(Render(shape, 12, 20).Red > Render(shape, 68, 20).Red);
    }

    [Fact]
    public void GradientStrokeRendersThroughTheSamePipeline()
    {
        var stroke = Gradient(new SweepGradientBrush());
        using var shape = new Ellipse { Size = new Size(80, 80), Stroke = stroke, StrokeThickness = 8 };

        Assert.True(Render(shape, 40, 6).Alpha > 0);

        shape.StrokeThickness = 12;
        Assert.True(Render(shape, 40, 10).Alpha > 0);
    }

    [Fact]
    public void GeometryTransformAffectsRenderingAndHitTesting()
    {
        var geometry = new EllipseGeometry(new RectangleF(0, 0, 20, 20))
        {
            Transform = Matrix3x2.CreateTranslation(40, 10)
        };
        using var shape = Filled(new GeometryProbe { Size = new Size(100, 60), Geometry = geometry });

        Assert.False(shape.HitTestClient(new PointF(10, 10)));
        Assert.True(shape.HitTestClient(new PointF(50, 20)));
        Assert.True(Render(shape, 50, 20).Alpha > 0);
    }

    [Fact]
    public void ControlRenderTransformAffectsPointerHitTesting()
    {
        using var shape = Filled(new Ellipse
        {
            Bounds = new Rectangle(0, 0, 60, 40),
            TranslationX = 40
        });

        Assert.False(shape.PresentationContains(new Point(5, 20)));
        Assert.True(shape.PresentationContains(new Point(70, 20)));
    }

    [Fact]
    public void GeometryAndControlTransformsUseTheSameOrderForRenderingAndHitTesting()
    {
        var geometry = new EllipseGeometry(new RectangleF(0, 0, 24, 16))
        {
            Transform = Matrix3x2.CreateTranslation(28, 18)
        };
        using var shape = Filled(new GeometryProbe
        {
            Bounds = new Rectangle(12, 8, 100, 70),
            Geometry = geometry,
            TranslationX = 17,
            ScaleX = 1.25f,
            ScaleY = 0.8f,
            Rotation = 19
        });

        Point transformedHit = shape.ClientPointToParentPresentation(new Point(40, 26));
        Point transformedMiss = shape.ClientPointToParentPresentation(new Point(8, 8));

        Assert.True(shape.PresentationContains(transformedHit));
        Assert.False(shape.PresentationContains(transformedMiss));
    }

    [Fact]
    public void SingularAndMirroredControlTransformsHaveSafeHitTesting()
    {
        using var shape = Filled(new Ellipse { Bounds = new Rectangle(10, 20, 60, 40) });

        shape.ScaleX = 0;
        Assert.False(shape.PresentationContains(new Point(40, 40)));

        shape.ScaleX = 0.0000001f;
        Assert.False(shape.PresentationContains(new Point(40, 40)));

        shape.ScaleX = -1;
        Point mirroredCenter = shape.ClientPointToParentPresentation(new Point(30, 20));
        Assert.True(shape.PresentationContains(mirroredCenter));
    }

    [Fact]
    public void PolygonFillHitTestingUsesFillRule()
    {
        using var shape = Filled(new Polygon
        {
            Size = new Size(100, 100),
            Points = [new(10, 10), new(90, 10), new(90, 90), new(10, 90)]
        });

        Assert.True(shape.HitTestClient(new PointF(50, 50)));
        Assert.False(shape.HitTestClient(new PointF(3, 3)));
    }

    [Fact]
    public void SelfIntersectingPolygonUsesItsSelectedFillRule()
    {
        PointCollection doubleWoundSquare =
        [
            new(10, 10), new(90, 10), new(90, 90), new(10, 90),
            new(10, 10), new(90, 10), new(90, 90), new(10, 90)
        ];
        using var winding = Filled(new Polygon { Size = new Size(100, 100), Points = doubleWoundSquare });
        using var evenOdd = Filled(new Polygon
        {
            Size = new Size(100, 100),
            Points = new PointCollection(doubleWoundSquare),
            FillRule = GeometryFillRule.EvenOdd
        });

        Assert.True(winding.HitTestClient(new PointF(50, 50)));
        Assert.False(evenOdd.HitTestClient(new PointF(50, 50)));
    }

    [Fact]
    public void LineHitTestingIncludesReasonableTolerance()
    {
        using var shape = Stroked(new Line { Size = new Size(120, 40), StrokeThickness = 1 });

        Assert.True(shape.HitTestClient(new PointF(60, 22)));
        Assert.False(shape.HitTestClient(new PointF(60, 28)));
    }

    [Fact]
    public void ShapeRenderingIsHardClippedToItsControlBuffer()
    {
        using var shape = Filled(new GeometryProbe
        {
            Size = new Size(30, 30),
            Geometry = new EllipseGeometry(new RectangleF(-40, -40, 110, 110))
        });
        using var bitmap = new SKBitmap(60, 60);
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);
        canvas.ClipRect(new SKRect(0, 0, 30, 30));
        shape.RaisePaint(new PaintEventArgs(bitmap.Info, canvas, 1));
        canvas.Flush();

        Assert.True(bitmap.GetPixel(15, 15).Alpha > 0);
        Assert.Equal(0, bitmap.GetPixel(45, 45).Alpha);
    }

    [Fact]
    public void TransformedPathAndThickStrokeRemainClippedToTheControlBuffer()
    {
        var geometry = new PathGeometry { Transform = Matrix3x2.CreateTranslation(12.5f, -3.25f) };
        var figure = new PathFigure(new PointF(-20, 15));
        figure.Segments.Add(new BezierSegment(new PointF(5, -15), new PointF(35, 45), new PointF(60, 12)));
        geometry.Figures.Add(figure);
        using var shape = new ModernFormsNext.Path
        {
            Size = new Size(30, 30),
            Data = geometry,
            Stroke = new SolidColorBrush(Color.DarkRed),
            StrokeThickness = 11.5f,
            StrokeLineCap = StrokeLineCap.Round
        };
        using var bitmap = new SKBitmap(60, 60);
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);
        canvas.ClipRect(new SKRect(0, 0, 30, 30));

        shape.RaisePaint(new PaintEventArgs(bitmap.Info, canvas, 1));
        canvas.Flush();

        int paintedPixelCount = 0;
        for (int x = 0; x < 30; x++)
        {
            for (int y = 0; y < 30; y++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 0)
                    paintedPixelCount++;
            }
        }
        Assert.NotEqual(0, paintedPixelCount);
        Assert.Equal(0, bitmap.GetPixel(45, 15).Alpha);
        Assert.False(shape.HitTestClient(new PointF(45, 15)));
    }

    [Fact]
    public void ResizeRebuildsEllipseGeometryCache()
    {
        using var shape = Filled(new Ellipse { Size = new Size(30, 30) });
        Assert.False(shape.HitTestClient(new PointF(55, 30)));

        shape.Size = new Size(70, 60);

        Assert.True(shape.HitTestClient(new PointF(55, 30)));
    }

    [Fact]
    public void PointMutationInvalidatesAndRebuildsPolygon()
    {
        var points = new PointCollection([new(5, 5), new(30, 5), new(5, 30)]);
        using var shape = Filled(new Polygon { Size = new Size(100, 100), Points = points });
        Assert.False(shape.HitTestClient(new PointF(70, 20)));

        points[1] = new PointF(95, 5);
        points[2] = new PointF(95, 40);

        Assert.True(shape.HitTestClient(new PointF(70, 20)));
    }

    [Fact]
    public void CollectionMoveRaisesOneChangeAndGeometryVersionIncrement()
    {
        var points = new PointCollection([new(1, 1), new(2, 2), new(3, 3)]);
        int pointChanges = 0;
        points.Changed += (_, _) => pointChanges++;
        points.Move(0, 2);

        var geometry = TriangleGeometry();
        int version = geometry.Version;
        geometry.Figures[0].Segments.Move(0, 1);
        int movedVersion = geometry.Version;

        Assert.Throws<ArgumentOutOfRangeException>(() => points.Move(-1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => geometry.Figures.Move(-1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => geometry.Figures[0].Segments.Move(-1, -1));

        Assert.Equal([new PointF(2, 2), new PointF(3, 3), new PointF(1, 1)], points);
        Assert.Equal(1, pointChanges);
        Assert.Equal(unchecked(version + 1), geometry.Version);
        Assert.Equal(movedVersion, geometry.Version);
    }

    [Fact]
    public void ExistingSegmentPropertyMutationInvalidatesRenderingAndHitTesting()
    {
        var geometry = new PathGeometry();
        var segment = new LineSegment(new PointF(25, 5));
        var figure = new PathFigure(new PointF(5, 5), isClosed: true);
        figure.Segments.Add(segment);
        figure.Segments.Add(new LineSegment(new PointF(5, 25)));
        geometry.Figures.Add(figure);
        using var shape = Filled(new GeometryProbe { Size = new Size(100, 60), Geometry = geometry });
        using var surface = new SkiaControlSurface(shape);
        surface.Resize(100, 60);
        Assert.False(shape.HitTestClient(new PointF(60, 15)));
        int baseline = shape.InvalidationCount;

        segment.Point = new PointF(90, 5);

        Assert.True(shape.InvalidationCount > baseline);
        Assert.True(shape.HitTestClient(new PointF(60, 10)));
    }

    [Fact]
    public void SharedGeometryInvalidatesEveryConsumer()
    {
        var geometry = new EllipseGeometry(new RectangleF(0, 0, 20, 20));
        using var first = Filled(new GeometryProbe { Size = new Size(100, 60), Geometry = geometry });
        using var second = Filled(new GeometryProbe { Size = new Size(100, 60), Geometry = geometry });
        using var firstSurface = new SkiaControlSurface(first);
        using var secondSurface = new SkiaControlSurface(second);
        firstSurface.Resize(100, 60);
        secondSurface.Resize(100, 60);
        int firstBaseline = first.InvalidationCount;
        int secondBaseline = second.InvalidationCount;

        geometry.Rect = new RectangleF(50, 10, 30, 30);

        Assert.True(first.HitTestClient(new PointF(65, 25)));
        Assert.True(second.HitTestClient(new PointF(65, 25)));
        Assert.True(first.InvalidationCount > firstBaseline);
        Assert.True(second.InvalidationCount > secondBaseline);
    }

    [Fact]
    public void ReplacingSharedGeometryDetachesOnlyTheReassignedConsumer()
    {
        var original = new EllipseGeometry(new RectangleF(0, 0, 20, 20));
        var replacement = new EllipseGeometry(new RectangleF(40, 0, 20, 20));
        using var first = Filled(new GeometryProbe { Size = new Size(80, 30), Geometry = original });
        using var second = Filled(new GeometryProbe { Size = new Size(80, 30), Geometry = original });
        using var firstSurface = new SkiaControlSurface(first);
        using var secondSurface = new SkiaControlSurface(second);
        firstSurface.Resize(80, 30);
        secondSurface.Resize(80, 30);

        first.Geometry = replacement;
        int firstBaseline = first.InvalidationCount;
        int secondBaseline = second.InvalidationCount;
        original.Rect = new RectangleF(5, 0, 20, 20);

        Assert.Equal(firstBaseline, first.InvalidationCount);
        Assert.True(second.InvalidationCount > secondBaseline);

        replacement.Rect = new RectangleF(45, 0, 20, 20);
        Assert.True(first.InvalidationCount > firstBaseline);
    }

    [Fact]
    public void SharedBrushMutationInvalidatesOnceAndReplacementCleansUpSubscriptions()
    {
        var shared = new SolidColorBrush(Color.CornflowerBlue);
        var replacement = new SolidColorBrush(Color.Gold);
        using var shape = new GeometryProbe
        {
            Size = new Size(40, 40),
            Geometry = new EllipseGeometry(new RectangleF(0, 0, 40, 40)),
            Fill = shared,
            Stroke = shared
        };
        using var surface = new SkiaControlSurface(shape);
        surface.Resize(40, 40);
        int baseline = shape.InvalidationCount;

        shared.Opacity = 0.5f;

        Assert.Equal(baseline + 1, shape.InvalidationCount);

        shape.Fill = replacement;
        shape.Stroke = replacement;
        baseline = shape.InvalidationCount;
        shared.PaintColor = Color.Red;
        Assert.Equal(baseline, shape.InvalidationCount);

        replacement.Transform = Matrix3x2.CreateTranslation(1.5f, 2.5f);
        Assert.Equal(baseline + 1, shape.InvalidationCount);
    }

    [Fact]
    public void AccessibilityBoundsEncloseRotatedAndMirroredPresentation()
    {
        using var root = new Panel { Size = new Size(300, 200) };
        using var shape = new Ellipse
        {
            Bounds = new Rectangle(40, 30, 80, 40),
            Rotation = 45,
            ScaleX = -1
        };
        root.Controls.Add(shape);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(300, 200);

        Rectangle bounds = shape.AccessibilityObject.Bounds;

        Assert.True(bounds.Width > shape.ScaledWidth);
        Assert.True(bounds.Height > shape.ScaledHeight);
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void DisposedShapeUnsubscribesFromSharedGeometry()
    {
        var geometry = new EllipseGeometry(new RectangleF(0, 0, 20, 20));
        var shape = new GeometryProbe { Size = new Size(30, 30), Geometry = geometry };
        shape.Dispose();
        int invalidations = shape.InvalidationCount;

        geometry.Rect = new RectangleF(1, 1, 20, 20);

        Assert.Equal(invalidations, shape.InvalidationCount);
    }

    [Fact]
    public void EmptyAndOnePointCollectionsAreDeterministic()
    {
        using var empty = Filled(new Polygon { Size = new Size(30, 30) });
        using var one = Stroked(new Polyline { Size = new Size(30, 30), Points = [new(10, 10)] });

        Assert.False(empty.HitTestClient(new PointF(10, 10)));
        Assert.False(one.HitTestClient(new PointF(10, 10)));
    }

    [Fact]
    public void InvalidFiniteInputsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new PointCollection { new(float.NaN, 0) });
        Assert.Throws<ArgumentException>(() => new LineGeometry(new PointF(float.PositiveInfinity, 0), PointF.Empty));
        using var shape = new Ellipse();
        Assert.Throws<ArgumentOutOfRangeException>(() => shape.StrokeThickness = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => shape.StrokeThickness = float.NaN);
    }

    [Fact]
    public void ZeroAndVeryLargeStrokeWidthsRemainDeterministic()
    {
        using var zero = Stroked(new Ellipse { Size = new Size(40, 40), StrokeThickness = 0 });
        using var large = Stroked(new Ellipse { Size = new Size(40, 40), StrokeThickness = 200 });

        Assert.Equal(0, Render(zero, 20, 2).Alpha);
        Assert.Equal(Render(large, 20, 2), Render(large, 20, 2));
    }

    [Fact]
    public void ShapeUsesGraphicAccessibilityRole()
    {
        using var shape = new Ellipse();

        Assert.Equal(ModernFormsNext.Accessibility.AccessibleRole.Graphic, shape.AccessibilityObject.Role);
    }

    private static T Filled<T>(T shape) where T : Shape
    {
        shape.Fill = new SolidColorBrush(Color.CornflowerBlue);
        return shape;
    }

    private static T Stroked<T>(T shape) where T : Shape
    {
        shape.Stroke = new SolidColorBrush(Color.Navy);
        return shape;
    }

    private static T Gradient<T>(T brush) where T : GradientBrush
    {
        brush.GradientStops.AddRange([
            new GradientStop(Color.White, 0),
            new GradientStop(Color.Navy, 1)
        ]);
        return brush;
    }

    private static PathGeometry TriangleGeometry()
    {
        var result = new PathGeometry();
        var figure = new PathFigure(new PointF(5, 90), true);
        figure.Segments.Add(new LineSegment(new PointF(50, 5)));
        figure.Segments.Add(new LineSegment(new PointF(95, 90)));
        result.Figures.Add(figure);
        return result;
    }

    private static SKColor Render(Shape shape, int x, int y)
    {
        using var bitmap = new SKBitmap(Math.Max(1, shape.Width), Math.Max(1, shape.Height));
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);
        shape.RaisePaint(new PaintEventArgs(bitmap.Info, canvas, 1));
        canvas.Flush();
        return bitmap.GetPixel(x, y);
    }

    private sealed class GeometryProbe : Shape
    {
        public int InvalidationCount { get; private set; }

        public Geometry? Geometry
        {
            get => DefiningGeometry;
            set => SetDefiningGeometry(value);
        }

        protected override void OnInvalidated(EventArgs<Rectangle> e)
        {
            InvalidationCount++;
            base.OnInvalidated(e);
        }
    }
}
