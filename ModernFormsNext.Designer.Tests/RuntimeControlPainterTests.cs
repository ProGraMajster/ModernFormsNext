using System.Drawing;
using ModernFormsNext.Designer.Surface;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class RuntimeControlPainterTests
{
    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.25, 1.0)]
    [InlineData(1.5, 0.8)]
    [InlineData(1.75, 0.8)]
    [InlineData(2.0, 0.75)]
    public void DetachedControlPaintIsUniformlyScaledToPreviewDestination(double dpiScale, double previewScale)
    {
        const int logicalWidth = 100;
        const int logicalHeight = 40;
        var destinationWidth = (int)Math.Round(logicalWidth * dpiScale * previewScale);
        var destinationHeight = (int)Math.Round(logicalHeight * dpiScale * previewScale);
        var destination = new Rectangle(20, 10, destinationWidth, destinationHeight);
        var targetInfo = new SKImageInfo(240, 140, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        using var targetBitmap = new SKBitmap(targetInfo);
        using var targetCanvas = new SKCanvas(targetBitmap);
        targetCanvas.Clear(SKColors.Transparent);
        var targetArgs = new PaintEventArgs(targetInfo, targetCanvas, dpiScale);
        var control = new LogicalFillControl
        {
            Bounds = new Rectangle(0, 0, logicalWidth, logicalHeight)
        };

        var painted = RuntimeControlPainter.TryPaint(
            targetArgs,
            control,
            new Size(logicalWidth, logicalHeight),
            destination,
            out var diagnostics,
            out var error);
        targetCanvas.Flush();

        Assert.True(painted, error);
        Assert.Equal(destinationWidth, diagnostics.Width);
        Assert.Equal(destinationHeight, diagnostics.Height);
        Assert.Equal(SKColors.Red, targetBitmap.GetPixel(destination.Right - 2, destination.Bottom - 2));
        Assert.Equal(0, targetBitmap.GetPixel(destination.Right + 1, destination.Bottom + 1).Alpha);
    }

    [Theory]
    [InlineData(8193, 1)]
    [InlineData(1, 8193)]
    [InlineData(8192, 2049)]
    [InlineData(4097, 4096)]
    public void OversizedDevicePreviewIsRejectedBeforePainting(int width, int height)
    {
        var info = new SKImageInfo(16, 16, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var control = new LogicalFillControl { Bounds = new Rectangle(0, 0, 16, 16) };

        bool painted = RuntimeControlPainter.TryPaint(new PaintEventArgs(info, canvas, 1d), control,
            new Size(16, 16), new Rectangle(0, 0, width, height), out var diagnostics, out var error);

        Assert.False(painted);
        Assert.Contains("Preview bitmap exceeds", error);
        Assert.Equal(0, control.PaintCount);
        Assert.Equal(default, diagnostics);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
    }

    private sealed class LogicalFillControl : Control
    {
        public int PaintCount { get; private set; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintCount++;
            e.Canvas.FillRectangle(new Rectangle(0, 0, Width, Height), SKColors.Red);
        }
    }
}
