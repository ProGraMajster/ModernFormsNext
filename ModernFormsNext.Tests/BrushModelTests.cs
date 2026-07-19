using System.Drawing;
using System.Numerics;
using ModernFormsNext.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class BrushModelTests
{
    [Fact]
    public void SolidBrushUsesOneColorValueAcrossNeutralAndCompatibilityApis()
    {
        var brush = new SolidColorBrush(Color.FromArgb(128, 12, 34, 56));
        var changes = 0;
        brush.Changed += (_, _) => changes++;

        Assert.Equal(Color.FromArgb(128, 12, 34, 56).ToArgb(), brush.PaintColor.ToArgb());
        Assert.Equal(new SKColor(12, 34, 56, 128), brush.Color);

        brush.Color = new SKColor(90, 80, 70, 60);
        brush.Color = new SKColor(90, 80, 70, 60);

        Assert.Equal(Color.FromArgb(60, 90, 80, 70).ToArgb(), brush.PaintColor.ToArgb());
        Assert.Equal(1, changes);
    }

    [Fact]
    public void OpacityAndTransformValidateAndNotifyOnlyForEffectiveChanges()
    {
        var brush = new SolidColorBrush(Color.Red);
        var changes = 0;
        brush.Changed += (_, _) => changes++;

        brush.Opacity = 0.5f;
        brush.Opacity = 0.5f;
        brush.Transform = Matrix3x2.CreateTranslation(4f, 7f);

        Assert.Equal(2, changes);
        Assert.Throws<ArgumentOutOfRangeException>(() => brush.Opacity = -0.01f);
        Assert.Throws<ArgumentOutOfRangeException>(() => brush.Opacity = float.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => brush.Opacity = 1.01f);
        Assert.Throws<ArgumentException>(() => brush.Transform = new Matrix3x2(float.NaN, 0, 0, 1, 0, 0));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.37f)]
    [InlineData(1f)]
    public void GradientStopAcceptsInclusiveNormalizedOffsets(float offset)
    {
        var stop = new GradientStop(Color.FromArgb(80, 10, 20, 30), offset);

        Assert.Equal(offset, stop.Offset);
        Assert.Equal(new SKColor(10, 20, 30, 80), stop.Color);
    }

    [Theory]
    [InlineData(-0.001f)]
    [InlineData(1.001f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void GradientStopRejectsInvalidOffsets(float offset)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new GradientStop(Color.Red, offset));

    [Fact]
    public void GradientStopColorAndOffsetChangesPropagateThroughBrush()
    {
        var stop = new GradientStop(Color.Red, 0.25f);
        var brush = new LinearGradientBrush();
        brush.GradientStops.Add(stop);
        var changes = 0;
        brush.Changed += (_, _) => changes++;

        stop.PaintColor = Color.Blue;
        stop.Offset = 0.75f;

        Assert.Equal(2, changes);
        Assert.Equal(Color.Blue.ToArgb(), stop.PaintColor.ToArgb());
        Assert.Equal(0.75f, stop.Offset);
    }

    [Fact]
    public void StopCollectionObservesMembershipOrderAndItemsWithoutDuplicateInstances()
    {
        var first = new GradientStop(Color.Red, 0.5f);
        var second = new GradientStop(Color.Blue, 0.5f);
        var third = new GradientStop(Color.Green, 1f);
        var stops = new GradientStopCollection();
        var changes = 0;
        stops.Changed += (_, _) => changes++;

        stops.AddRange([first, second]);
        stops.Insert(1, third);
        stops.Move(2, 0);
        stops[1] = new GradientStop(Color.Yellow, 0.75f);
        stops.Remove(second);
        stops.Clear();

        Assert.Equal(6, changes);
        Assert.Empty(stops);
        Assert.Throws<ArgumentException>(() => stops.AddRange([third, third]));
    }

    [Fact]
    public void EqualOffsetsKeepCollectionOrderInStableRenderSnapshot()
    {
        var first = new GradientStop(Color.Red, 0.5f);
        var second = new GradientStop(Color.Blue, 0.5f);
        var start = new GradientStop(Color.Black, 0f);
        var brush = new LinearGradientBrush();
        brush.GradientStops.AddRange([first, second, start]);

        Assert.Equal([start, first, second], brush.GetOrderedStops());

        brush.GradientStops.Move(1, 0);

        Assert.Equal([start, second, first], brush.GetOrderedStops());
    }

    [Fact]
    public void LinearCoordinatesAreNeutralRelativeValuesWithSkiaCompatibilityViews()
    {
        var brush = new LinearGradientBrush();
        var changes = 0;
        brush.Changed += (_, _) => changes++;

        brush.Start = new PointF(-0.25f, 0.5f);
        brush.EndPoint = new SKPoint(1.25f, 0.75f);

        Assert.Equal(new SKPoint(-0.25f, 0.5f), brush.StartPoint);
        Assert.Equal(new PointF(1.25f, 0.75f), brush.End);
        Assert.Equal(2, changes);
        Assert.Throws<ArgumentException>(() => brush.Start = new PointF(float.NaN, 0f));
    }

    [Fact]
    public void RadialOriginFollowsCenterUntilExplicitlyAssigned()
    {
        var brush = new RadialGradientBrush();

        brush.CenterPoint = new PointF(0.25f, 0.4f);
        Assert.Equal(brush.CenterPoint, brush.GradientOrigin);

        brush.GradientOrigin = new PointF(0.1f, 0.2f);
        brush.Center = new SKPoint(0.8f, 0.7f);

        Assert.Equal(new PointF(0.1f, 0.2f), brush.GradientOrigin);
        Assert.Equal(new PointF(0.8f, 0.7f), brush.CenterPoint);
        brush.Radius = 0f;
        Assert.Equal(0f, brush.Radius);
        Assert.Throws<ArgumentOutOfRangeException>(() => brush.Radius = -1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => brush.Radius = float.PositiveInfinity);
    }

    [Fact]
    public void SpreadAndSweepValuesValidateAndNotify()
    {
        var brush = new SweepGradientBrush();
        var changes = 0;
        brush.Changed += (_, _) => changes++;

        brush.SpreadMode = GradientSpreadMode.Reflect;
        brush.CenterPoint = new PointF(0.25f, 0.75f);
        brush.StartAngle = -90f;
        brush.EndAngle = 270f;

        Assert.Equal(4, changes);
        Assert.Throws<ArgumentOutOfRangeException>(() => brush.SpreadMode = (GradientSpreadMode)99);
        Assert.Throws<ArgumentOutOfRangeException>(() => brush.StartAngle = float.NaN);
    }
}
