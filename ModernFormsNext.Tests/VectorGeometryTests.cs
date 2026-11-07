using System.Drawing;
using System.Globalization;
using System.Numerics;
using ModernFormsNext.Drawing;
using ModernFormsNext.Rendering.Skia;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class VectorGeometryTests
{
    [Theory]
    [InlineData(false, SweepDirection.Clockwise)]
    [InlineData(true, SweepDirection.Clockwise)]
    [InlineData(false, SweepDirection.CounterClockwise)]
    [InlineData(true, SweepDirection.CounterClockwise)]
    public void EndpointArcRendersItsSweepAndHasTransformedTightBounds(bool large, SweepDirection sweep)
    {
        var arc = new ArcSegment(new(70, 40), new(35, 20), 25, large, sweep);
        var figure = new PathFigure(new(20, 30));
        figure.Segments.Add(arc);
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        using var original = Convert(geometry);
        Assert.Equal(new SKPoint(70, 40), original.LastPoint);
        Assert.True(original.PointCount > 2);
        SKRect bounds = original.ComputeTightBounds();
        geometry.Transform = Matrix3x2.CreateScale(2, 3) * Matrix3x2.CreateTranslation(7, 11);
        RectangleF transformed = geometry.GetBounds();
        Assert.Equal(bounds.Left * 2 + 7, transformed.Left, 3);
        Assert.Equal(bounds.Top * 3 + 11, transformed.Top, 3);
        Assert.Equal(bounds.Right * 2 + 7, transformed.Right, 3);
        Assert.Equal(bounds.Bottom * 3 + 11, transformed.Bottom, 3);
        using var density = SkiaGeometryConverter.CreatePath(geometry, new(1.5f, 2));
        Assert.Equal(transformed.Left * 1.5f, density.ComputeTightBounds().Left, 3);
        Assert.Equal(transformed.Bottom * 2, density.ComputeTightBounds().Bottom, 3);
    }

    [Fact]
    public void ArcDegenerationsAndRadiusCorrectionHaveExplicitSemantics()
    {
        using var equal = Convert(PathGeometry.Parse("M10 20 A30 40 0 1 1 10 20"));
        Assert.Equal(1, equal.PointCount);
        using var line = Convert(PathGeometry.Parse("M10 20 A0 40 0 0 1 50 20"));
        Assert.Equal(2, line.PointCount);
        Assert.Equal(new SKRect(10, 20, 50, 20), line.Bounds);
        var corrected = PathGeometry.Parse("M0 20 A1 1 0 0 1 40 20");
        Assert.Equal(40, corrected.GetBounds().Width, 3);
        Assert.Equal(20, corrected.GetBounds().Height, 3);
    }

    [Fact]
    public void SweepDirectionSelectsTheExpectedFilledSemicircleInDownwardYCoordinates()
    {
        using var clockwise = Convert(PathGeometry.Parse("M0 30 A30 30 0 0 1 60 30 Z"));
        using var counterClockwise = Convert(PathGeometry.Parse("M0 30 A30 30 0 0 0 60 30 Z"));
        Assert.True(clockwise.Contains(30, 15));
        Assert.False(clockwise.Contains(30, 45));
        Assert.False(counterClockwise.Contains(30, 15));
        Assert.True(counterClockwise.Contains(30, 45));
    }

    [Fact]
    public void ArcMutationPropagatesThroughSharedGroupsAndCombinerAndDetaches()
    {
        var path = PathGeometry.Parse("M0 0 A20 20 0 0 1 40 0");
        var arc = Assert.IsType<ArcSegment>(path.Figures[0].Segments[0]);
        var group = new GeometryGroup { Children = { path, path } };
        var combined = new CombinedGeometry(GeometryCombineMode.Union, group, group);
        int groupChanges = 0, combinedChanges = 0;
        group.Changed += (_, _) => groupChanges++;
        combined.Changed += (_, _) => combinedChanges++;
        arc.Size = new(30, 40);
        Assert.Equal(1, groupChanges);
        Assert.Equal(1, combinedChanges);
        group.Children.RemoveAt(0);
        arc.Point = new(50, 0);
        Assert.Equal(3, groupChanges);
        group.Children.Clear();
        int baseline = groupChanges;
        arc.RotationAngle = 20;
        Assert.Equal(baseline, groupChanges);
        combined.Geometry1 = null;
        combined.Geometry2 = null;
        int detached = combinedChanges;
        group.FillRule = GeometryFillRule.EvenOdd;
        Assert.Equal(detached, combinedChanges);
    }

    [Theory]
    [InlineData(GeometryCombineMode.Union, true, true, true)]
    [InlineData(GeometryCombineMode.Intersect, false, true, false)]
    [InlineData(GeometryCombineMode.Xor, true, false, true)]
    [InlineData(GeometryCombineMode.Exclude, true, false, false)]
    public void BooleanOperationsUseFilledAreasAndOperandTransforms(GeometryCombineMode mode, bool left, bool overlap, bool right)
    {
        var first = new RectangleGeometry(new(0, 0, 40, 30));
        var second = new RectangleGeometry(new(0, 0, 40, 30)) { Transform = Matrix3x2.CreateTranslation(20, 0) };
        var combined = new CombinedGeometry(mode, first, second) { Transform = Matrix3x2.CreateTranslation(5, 5) };
        using var path = Convert(combined);
        Assert.Equal(left, path.Contains(15, 15));
        Assert.Equal(overlap, path.Contains(35, 15));
        Assert.Equal(right, path.Contains(55, 15));
        using var shape = new ModernFormsNext.Path { Size = new(80, 50), Data = combined, Fill = new SolidColorBrush(Color.Gold) };
        Assert.Equal(overlap, shape.HitTestClient(new(35, 15)));
    }

    [Theory]
    [InlineData(GeometryCombineMode.Union, true, true)]
    [InlineData(GeometryCombineMode.Intersect, false, false)]
    [InlineData(GeometryCombineMode.Xor, true, true)]
    [InlineData(GeometryCombineMode.Exclude, true, false)]
    public void NullOperandsAreEmptyAreas(GeometryCombineMode mode, bool firstOnly, bool secondOnly)
    {
        var rectangle = new RectangleGeometry(new(0, 0, 10, 10));
        using var left = Convert(new CombinedGeometry(mode, rectangle, null));
        using var right = Convert(new CombinedGeometry(mode, null, rectangle));
        Assert.Equal(firstOnly, left.Contains(5, 5));
        Assert.Equal(secondOnly, right.Contains(5, 5));
    }

    [Fact]
    public void GroupContoursRespectFillRuleAndChildThenParentTransforms()
    {
        var group = new GeometryGroup
        {
            FillRule = GeometryFillRule.EvenOdd,
            Transform = Matrix3x2.CreateTranslation(5, 7),
            Children = { new RectangleGeometry(new(0, 0, 30, 30)), new RectangleGeometry(new(0, 0, 30, 30)) { Transform = Matrix3x2.CreateTranslation(10, 0) } }
        };
        using var odd = Convert(group);
        Assert.True(odd.Contains(10, 15));
        Assert.False(odd.Contains(25, 15));
        group.FillRule = GeometryFillRule.Winding;
        using var winding = Convert(group);
        Assert.True(winding.Contains(25, 15));
        Assert.Equal(new RectangleF(5, 7, 40, 30), group.GetBounds());
    }

    [Fact]
    public void CyclesAreRejectedBeforeMutationButSharingRemainsAllowed()
    {
        var parent = new GeometryGroup();
        var child = new GeometryGroup();
        parent.Children.Add(child);
        var combined = new CombinedGeometry(GeometryCombineMode.Union, parent, child);
        Assert.Throws<ArgumentException>(() => child.Children.Add(combined));
        Assert.Empty(child.Children);
        Assert.Throws<ArgumentException>(() => combined.Geometry1 = combined);
        Assert.Same(parent, combined.Geometry1);
        Assert.Throws<ArgumentException>(() => parent.Children[0] = parent);
        Assert.Same(child, parent.Children[0]);
        parent.Children.Add(child);
        Assert.Equal(2, parent.Children.Count);
    }

    [Fact]
    public void DeepAcyclicGeometryDoesNotUseRecursiveConversion()
    {
        Geometry geometry = new RectangleGeometry(new(0, 0, 10, 20));
        for (int i = 0; i < 600; i++) geometry = new GeometryGroup { Children = { geometry } };
        Assert.Equal(new RectangleF(0, 0, 10, 20), geometry.GetBounds());
    }

    [Fact]
    public void GrammarSupportsRelativeRepeatsSmoothCurvesAndInvariantRoundTrip()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            var parsed = PathGeometry.Parse("m1.5 2.5 10 0 h5 v5 c1 2 3 4 5 6 s7 8 9 10 q2 3 4 5 t6 7 a-8 -9 30 01 10 11 z");
            PathFigure figure = Assert.Single(parsed.Figures);
            Assert.Equal(new PointF(1.5f, 2.5f), figure.StartPoint);
            Assert.True(figure.IsClosed);
            Assert.Equal(8, figure.Segments.Count);
            var smooth = Assert.IsType<BezierSegment>(figure.Segments[4]);
            Assert.Equal(new PointF(23.5f, 15.5f), smooth.ControlPoint1);
            var arc = Assert.IsType<ArcSegment>(figure.Segments[7]);
            Assert.Equal(new SizeF(8, 9), arc.Size);
            Assert.Equal(SweepDirection.Clockwise, arc.SweepDirection);
            string text = parsed.ToPathData();
            Assert.Equal(text, PathGeometry.Parse(text).ToPathData());
            Assert.Equal(parsed.GetBounds(), PathGeometry.Parse(text).GetBounds());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("L 1 2")]
    [InlineData("M,1 2")]
    [InlineData("M1 2,")]
    [InlineData("M1 2 L3")]
    [InlineData("M1 2 R3 4")]
    [InlineData("M1 2 L1e60 3")]
    [InlineData("M1 2 L2e+ 3")]
    [InlineData("M1 2 A10 10 0 2 0 20 20")]
    [InlineData("M1 2 Z 3 4")]
    [InlineData("M3e38 1 l3e38 0")]
    public void InvalidPathDataIsRejectedWithoutNativeConversion(string text)
        => Assert.Throws<FormatException>(() => PathGeometry.Parse(text));

    [Fact]
    public void ParserAdmissionLimitsApplyBeforeLargeNativeWork()
    {
        Assert.Throws<FormatException>(() => PathGeometry.Parse(new string(' ', 262145)));
        Assert.Throws<FormatException>(() => PathGeometry.Parse(string.Concat(Enumerable.Repeat("M0 0 ", 1025))));
        Assert.Throws<FormatException>(() => PathGeometry.Parse("M0 0 " + string.Concat(Enumerable.Repeat("L1 1 ", 16385))));
    }

    [Fact]
    public void ArcRejectsInvalidTypedValuesBeforeNotifying()
    {
        var arc = new ArcSegment();
        int changed = 0;
        arc.Changed += (_, _) => changed++;
        Assert.Throws<ArgumentOutOfRangeException>(() => arc.Size = new(-1, 10));
        Assert.Throws<ArgumentException>(() => arc.Point = new(float.NaN, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => arc.RotationAngle = float.PositiveInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => arc.SweepDirection = (SweepDirection)3);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void TransformedCoordinateOverflowCannotPublishANativePath()
    {
        var geometry = new LineGeometry(new(1, 1), new(10, 10)) { Transform = Matrix3x2.CreateScale(float.MaxValue) };
        Assert.Throws<ArgumentException>(() => geometry.GetBounds());
    }

    [Fact]
    public void ParentClipMutationAndRemovalRecomposeRetainedChildPixels()
    {
        using var root = new Panel { Size = new(100, 80), BackgroundBrush = new SolidColorBrush(Color.White) };
        var clip = new RectangleGeometry(new(0, 0, 40, 60));
        var parent = root.Controls.Add(new Panel { Bounds = new(10, 10, 80, 60), ClipGeometry = clip });
        var child = parent.Controls.Add(new ModernFormsNext.Path
        {
            Dock = DockStyle.Fill, Data = new RectangleGeometry(new(0, 0, 80, 60)), Fill = new SolidColorBrush(Color.Gold)
        });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(100, 80);
        using var bitmap = new SKBitmap(100, 80);
        using var canvas = new SKCanvas(bitmap);
        surface.Render(canvas);
        Assert.Equal(SKColors.Gold, bitmap.GetPixel(30, 30));
        Assert.Equal(SKColors.White, bitmap.GetPixel(70, 30));
        int clicks = 0;
        child.Click += (_, _) => clicks++;
        surface.ProcessPointer(ControlSurfacePointerAction.Down, 30, 30);
        surface.ProcessPointer(ControlSurfacePointerAction.Up, 30, 30);
        surface.ProcessPointer(ControlSurfacePointerAction.Down, 70, 30);
        surface.ProcessPointer(ControlSurfacePointerAction.Up, 70, 30);
        Assert.Equal(1, clicks);
        clip.Rect = new(40, 0, 40, 60);
        surface.Render(canvas);
        Assert.Equal(SKColors.White, bitmap.GetPixel(30, 30));
        Assert.Equal(SKColors.Gold, bitmap.GetPixel(70, 30));
        parent.ClipGeometry = null;
        surface.Render(canvas);
        Assert.Equal(SKColors.Gold, bitmap.GetPixel(30, 30));
        Assert.False(parent.HasUnsupportedNativeComposition);
    }

    [Fact]
    public void ClipFinalizationPathPreservesManagedOwnershipUntilExplicitDisposal()
    {
        var geometry = new RectangleGeometry(new(0, 0, 20, 20));
        var control = new Panel { Size = new(40, 40), ClipGeometry = geometry };
        Assert.True(control.HitTestClient(new(10, 10)));
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var state = typeof(Control).GetProperty("GeometryClipState", flags)!.GetValue(control)!;
        var field = state.GetType().GetField("path", flags)!;
        var original = Assert.IsType<SKPath>(field.GetValue(state));
        var dispose = typeof(Control).GetMethod("Dispose", flags, [typeof(bool)])!;
        Exception? failure = null;
        var thread = new Thread(() => failure = Record.Exception(() => dispose.Invoke(control, [false])));
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
        Assert.NotEqual(IntPtr.Zero, original.Handle);
        Assert.Same(geometry, control.ClipGeometry);
        geometry.Rect = new(20, 20, 20, 20);
        Assert.Equal(IntPtr.Zero, original.Handle);
        Assert.False(control.HitTestClient(new(10, 10)));
        Assert.True(control.HitTestClient(new(30, 30)));
        var replacement = Assert.IsType<SKPath>(field.GetValue(state));
        control.Dispose();
        Assert.Equal(IntPtr.Zero, replacement.Handle);
        Assert.Null(control.ClipGeometry);
    }

    [Theory]
    [InlineData(GeometryStretch.None, false, false)]
    [InlineData(GeometryStretch.Fill, true, true)]
    [InlineData(GeometryStretch.Uniform, true, false)]
    [InlineData(GeometryStretch.UniformToFill, true, true)]
    public void StretchCentersNonzeroOriginGeometryAndUsesTheSameHitPath(GeometryStretch stretch, bool center, bool top)
    {
        using var shape = new ModernFormsNext.Path
        {
            Size = new(100, 100), Data = new RectangleGeometry(new(200, 300, 20, 10)),
            Fill = new SolidColorBrush(Color.Gold), StrokeThickness = 0, Stretch = stretch
        };
        Assert.Equal(center, shape.HitTestClient(new(50, 50)));
        Assert.Equal(top, shape.HitTestClient(new(50, 10)));
    }

    [Fact]
    public void StretchRebuildsForSizePaddingAndStrokeAndCentersDegenerateAxis()
    {
        using var shape = new ModernFormsNext.Path
        {
            Size = new(100, 100), Data = new RectangleGeometry(new(20, 30, 10, 10)),
            Fill = new SolidColorBrush(Color.Gold), StrokeThickness = 0, Stretch = GeometryStretch.Fill
        };
        Assert.True(shape.HitTestClient(new(5, 50)));
        shape.Padding = new Padding(10);
        Assert.False(shape.HitTestClient(new(5, 50)));
        shape.StrokeThickness = 20;
        Assert.False(shape.HitTestClient(new(15, 50)));
        shape.Size = new(60, 60);
        Assert.True(shape.HitTestClient(new(30, 30)));
        shape.Data = new LineGeometry(new(20, 30), new(20, 40));
        shape.Stroke = new SolidColorBrush(Color.Navy);
        shape.StrokeThickness = 2;
        Assert.True(shape.HitTestClient(new(30, 30)));
        Assert.False(shape.HitTestClient(new(20, 30)));
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(30f, 1.2f)]
    [InlineData(0f, -1f)]
    public void ClipComposesAndHitTestsInTheSamePresentationSpace(float rotation, float scale)
    {
        var clip = new RectangleGeometry(new(0, 0, 20, 40));
        using var control = new Panel { Bounds = new(50, 50, 40, 40), ClipGeometry = clip, Rotation = rotation, ScaleX = scale };
        using var buffer = new SKBitmap(40, 40);
        buffer.Erase(SKColors.Gold);
        using var destination = new SKBitmap(160, 160);
        destination.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(destination);
        control.DrawBackBuffer(canvas, buffer);
        Point inside = control.ClientPointToParentPresentation(new Point(10, 20));
        Point outside = control.ClientPointToParentPresentation(new Point(30, 20));
        Assert.True(control.PresentationContains(inside));
        Assert.False(control.PresentationContains(outside));
        Assert.True(destination.GetPixel(inside.X, inside.Y).Alpha > 0);
        Assert.Equal(0, destination.GetPixel(outside.X, outside.Y).Alpha);
        Assert.True(control.HasUnsupportedNativeComposition);
        clip.Rect = new(20, 0, 20, 40);
        Assert.False(control.PresentationContains(inside));
        Assert.True(control.PresentationContains(outside));
        control.ClipGeometry = null;
        Assert.True(control.PresentationContains(inside));
        destination.Erase(SKColors.Transparent);
        control.DrawBackBuffer(canvas, buffer);
        Assert.True(destination.GetPixel(outside.X, outside.Y).Alpha > 0);
    }

    private static SKPath Convert(Geometry geometry) => SkiaGeometryConverter.CreatePath(geometry, new(1, 1));
}
