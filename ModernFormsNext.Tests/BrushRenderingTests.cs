using System.Drawing;
using System.Numerics;
using ModernFormsNext.Drawing;
using ModernFormsNext.Rendering.Skia;
using SkiaSharp;
using Xunit;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext.Tests;

public sealed class BrushRenderingTests
{
    [Fact]
    public void SolidOpacityMultipliesColorAlpha()
    {
        var brush = new SolidColorBrush(Color.FromArgb(128, 20, 40, 60)) { Opacity = 0.5f };

        SKColor pixel = RenderPixel(brush, 4, 4, 2, 2);

        Assert.InRange(pixel.Alpha, (byte)63, (byte)65);
        Assert.Equal((byte)20, pixel.Red);
        Assert.Equal((byte)40, pixel.Green);
        Assert.Equal((byte)60, pixel.Blue);
    }

    [Fact]
    public void NoBrushAndEmptyGradientLeaveTheDestinationUntouched()
    {
        Assert.Equal(0, RenderPixel(new NoBrush(), 4, 4, 2, 2).Alpha);
        Assert.Equal(0, RenderPixel(new LinearGradientBrush(), 4, 4, 2, 2).Alpha);
    }

    [Fact]
    public void BackgroundBrushOverridesLegacyBackColorAndNullRestoresIt()
    {
        using var control = new Control
        {
            BackColor = SKColors.Red,
            BackgroundBrush = new SolidColorBrush(Color.Blue)
        };
        using var adapter = new SkiaControlSurface(control);
        using var bitmap = new SKBitmap(12, 12, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        adapter.Resize(12, 12);

        Assert.Equal(SKColors.Blue, RenderControlPixel(adapter, canvas, bitmap));

        control.BackgroundBrush = null;
        Assert.Equal(SKColors.Red, RenderControlPixel(adapter, canvas, bitmap));
    }

    [Fact]
    public void OneStopAndZeroRadiusGradientsHaveDeterministicSolidResults()
    {
        var oneStop = new LinearGradientBrush();
        oneStop.GradientStops.Add(new GradientStop(Color.CornflowerBlue, 0.4f));

        var zeroRadius = new RadialGradientBrush { Radius = 0f };
        zeroRadius.GradientStops.AddRange([
            new GradientStop(Color.Red, 0f),
            new GradientStop(Color.Lime, 1f)
        ]);

        Assert.Equal(new SKColor(100, 149, 237), RenderPixel(oneStop, 6, 6, 3, 3));
        Assert.Equal(SKColors.Lime, RenderPixel(zeroRadius, 6, 6, 0, 0));
    }

    [Theory]
    [InlineData(GradientSpreadMode.Pad, SKShaderTileMode.Clamp)]
    [InlineData(GradientSpreadMode.Repeat, SKShaderTileMode.Repeat)]
    [InlineData(GradientSpreadMode.Reflect, SKShaderTileMode.Mirror)]
    public void SpreadModesMapToSkiaTileModes(GradientSpreadMode source, SKShaderTileMode expected)
        => Assert.Equal(expected, SkiaBrushFactory.MapSpreadMode(source));

    [Fact]
    public void RelativeCoordinatesResolveAgainstEachCurrentBoundsWithoutDpiRescaling()
    {
        var point = new PointF(0.25f, 0.75f);

        Assert.Equal(new SKPoint(35f, 35f), SkiaBrushFactory.ResolveRelativePoint(point, new SKRect(10, 20, 110, 40)));
        Assert.Equal(new SKPoint(60f, 80f), SkiaBrushFactory.ResolveRelativePoint(point, new SKRect(10, 20, 210, 100)));
    }

    [Fact]
    public void LinearRadialFocalAndSweepBrushesCreateBoundsSpecificShaders()
    {
        GradientBrush[] brushes =
        [
            Gradient(new LinearGradientBrush { Start = new PointF(0, 0), End = new PointF(1, 0) }),
            Gradient(new RadialGradientBrush { CenterPoint = new PointF(0.5f, 0.5f), Radius = 0.5f }),
            Gradient(new RadialGradientBrush { CenterPoint = new PointF(0.5f, 0.5f), GradientOrigin = new PointF(0.25f, 0.5f), Radius = 0.5f }),
            Gradient(new SweepGradientBrush { CenterPoint = new PointF(0.5f, 0.5f), StartAngle = 0, EndAngle = 360 })
        ];

        foreach (GradientBrush brush in brushes)
        {
            using SKShader? first = SkiaBrushFactory.CreateGradientShader(brush, new SKRect(0, 0, 100, 40));
            using SKShader? resized = SkiaBrushFactory.CreateGradientShader(brush, new SKRect(0, 0, 220, 80));
            Assert.NotNull(first);
            Assert.NotNull(resized);
            Assert.NotSame(first, resized);
        }
    }

    [Fact]
    public void TransformCreatesAnOwnedShaderAndRepeatedScopesDisposeCleanly()
    {
        var brush = Gradient(new LinearGradientBrush
        {
            Start = new PointF(0, 0),
            End = new PointF(1, 0),
            Transform = Matrix3x2.CreateTranslation(5f, 0f)
        });

        for (var index = 0; index < 250; index++)
        {
            using SKShader? shader = SkiaBrushFactory.CreateGradientShader(brush, new SKRect(0, 0, 100 + index, 40));
            Assert.NotNull(shader);
        }
    }

    [Fact]
    public void RepeatAndReflectProduceDifferentAlternateIntervals()
    {
        var repeat = CreateShortLinearGradient(GradientSpreadMode.Repeat);
        var reflect = CreateShortLinearGradient(GradientSpreadMode.Reflect);

        SKColor repeatSecondInterval = RenderPixel(repeat, 100, 4, 31, 2);
        SKColor reflectSecondInterval = RenderPixel(reflect, 100, 4, 31, 2);

        Assert.True(repeatSecondInterval.Red < 150, $"Expected repeated interval near dark; actual {repeatSecondInterval}.");
        Assert.True(reflectSecondInterval.Red > 150, $"Expected reflected interval near light; actual {reflectSecondInterval}.");
    }

    private static T Gradient<T>(T brush) where T : GradientBrush
    {
        brush.GradientStops.AddRange([
            new GradientStop(Color.Black, 0f),
            new GradientStop(Color.White, 1f)
        ]);
        return brush;
    }

    private static LinearGradientBrush CreateShortLinearGradient(GradientSpreadMode mode)
        => Gradient(new LinearGradientBrush
        {
            Start = new PointF(0f, 0f),
            End = new PointF(0.25f, 0f),
            SpreadMode = mode
        });

    private static SKColor RenderPixel(MfnBrush brush, int width, int height, int x, int y)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        SkiaExtensions.RenderBrushBackground(canvas, new SKRect(0, 0, width, height), brush, SKColors.Magenta);
        canvas.Flush();
        return bitmap.GetPixel(x, y);
    }

    private static SKColor RenderControlPixel(SkiaControlSurface adapter, SKCanvas canvas, SKBitmap bitmap)
    {
        canvas.Clear(SKColors.Transparent);
        adapter.Render(canvas);
        canvas.Flush();
        return bitmap.GetPixel(6, 6);
    }
}
