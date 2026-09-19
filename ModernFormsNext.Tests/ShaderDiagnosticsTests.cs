using System.Drawing;
using System.Numerics;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.Drawing;
using ModernFormsNext.Rendering.Skia;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ShaderDiagnosticsTests
{
    [Fact]
    public void TransformedGradientCountsBothActualWrappersAndTheirDisposal()
    {
        var brush = Gradient();
        brush.Transform = Matrix3x2.CreateTranslation(4, 2);
        using var profiler = PerformanceProfiler.Start();
        var shader = SkiaBrushFactory.CreateOwnedGradientShader(brush, new SKRect(0, 0, 80, 40));
        Assert.NotNull(shader.Shader);
        Assert.Equal(2, profiler.Capture().UnframedWork.ShadersCreated);
        Assert.Equal(1, profiler.Capture().UnframedWork.ShadersDisposed);

        shader.Dispose();
        shader.Dispose();
        Assert.Equal(IntPtr.Zero, shader.Shader.Handle);
        Assert.Equal(2, profiler.Capture().UnframedWork.ShadersDisposed);
    }

    [Fact]
    public void DrawExceptionUnwindsOwnedPaintAndShaderWithoutASecondDisposalCount()
    {
        using var profiler = PerformanceProfiler.Start();
        SkiaBrushPaint? captured = null;
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var paint = SkiaBrushPaintFactory.Create(Gradient(), new SKRect(0, 0, 20, 20), SKPaintStyle.Fill);
            captured = paint;
            throw new InvalidOperationException("draw callback test");
        }));

        Assert.NotNull(captured);
        Assert.Equal(IntPtr.Zero, captured.Paint.Handle);
        captured.Dispose();
        PerformanceWorkMetrics work = profiler.Capture().UnframedWork;
        Assert.Equal(1, work.ShadersCreated);
        Assert.Equal(1, work.ShadersDisposed);
    }

    [Fact]
    public void FailedNativeShaderArgumentsDoNotClaimCreationAndLaterCreationStillWorks()
    {
        using var profiler = PerformanceProfiler.Start();
        Assert.Throws<ArgumentException>(() => SkiaBrushFactory.CreateOwnedLinearGradient(
            new SKPoint(0, 0), new SKPoint(10, 10), [SKColors.Black, SKColors.White],
            [0f], SKShaderTileMode.Clamp, Matrix3x2.Identity));
        Assert.Equal(0, profiler.Capture().UnframedWork.ShadersCreated);

        using (var shader = SkiaBrushFactory.CreateOwnedGradientShader(Gradient(), new SKRect(0, 0, 20, 20)))
            Assert.NotNull(shader.Shader);
        Assert.Equal(1, profiler.Capture().UnframedWork.ShadersCreated);
        Assert.Equal(1, profiler.Capture().UnframedWork.ShadersDisposed);
    }

    [Fact]
    public void DisposalAfterSessionRetirementCannotChargeTheReplacementSession()
    {
        var first = PerformanceProfiler.Start();
        var shader = SkiaBrushFactory.CreateOwnedGradientShader(Gradient(), new SKRect(0, 0, 20, 20));
        PerformanceSnapshot frozen = first.Capture();
        first.Dispose();
        using var second = PerformanceProfiler.Start();
        shader.Dispose();

        Assert.Equal(1, frozen.UnframedWork.ShadersCreated);
        Assert.Equal(0, frozen.UnframedWork.ShadersDisposed);
        Assert.Equal(0, second.Capture().UnframedWork.ShadersCreated);
        Assert.Equal(0, second.Capture().UnframedWork.ShadersDisposed);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 4)]
    public void GlassLayersCountOwnedShadersIncludingTransforms(bool transform, int expected)
    {
        var brush = new GlassBrush { Transform = transform ? Matrix3x2.CreateTranslation(1, 2) : Matrix3x2.Identity };
        using var bitmap = new SKBitmap(80, 40);
        using var canvas = new SKCanvas(bitmap);
        using var profiler = PerformanceProfiler.Start();

        SkiaExtensions.RenderBrushBackground(canvas, new SKRect(0, 0, 80, 40), brush, SKColors.Transparent);

        Assert.Equal(expected, profiler.Capture().UnframedWork.ShadersCreated);
        Assert.Equal(expected, profiler.Capture().UnframedWork.ShadersDisposed);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void ColorEditorsDisposeTheirGradientWrappersDuringThePaint(bool colorBox, int expected)
    {
        using Control control = colorBox ? new ColorBox() : new HueSlider();
        using var host = new SkiaControlSurface(control);
        host.Resize(80, 60);
        using var surface = SKSurface.Create(new SKImageInfo(80, 60));
        using var profiler = PerformanceProfiler.Start();
        host.Render(surface.Canvas);
        PerformanceWorkMetrics work = profiler.Capture().LatestFrame!.Value.Work;
        Assert.Equal(expected, work.ShadersCreated);
        Assert.Equal(expected, work.ShadersDisposed);
    }

    [Fact]
    public void RepeatedOwnedScopesRemainBalancedWithoutEstimatingNativeAllocations()
    {
        var brush = Gradient();
        using var profiler = PerformanceProfiler.Start();
        for (int index = 0; index < 256; index++)
        {
            using var shader = SkiaBrushFactory.CreateOwnedGradientShader(brush, new SKRect(0, 0, 80, 40));
            Assert.NotNull(shader.Shader);
        }
        PerformanceWorkMetrics work = profiler.Capture().UnframedWork;
        Assert.Equal(256, work.ShadersCreated);
        Assert.Equal(256, work.ShadersDisposed);
    }

    private static LinearGradientBrush Gradient()
    {
        var brush = new LinearGradientBrush();
        brush.GradientStops.AddRange([
            new GradientStop(Color.Black, 0), new GradientStop(Color.White, 1)
        ]);
        return brush;
    }
}
