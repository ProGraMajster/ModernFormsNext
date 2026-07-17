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

    private sealed class LogicalFillControl : Control
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Canvas.FillRectangle(new Rectangle(0, 0, Width, Height), SKColors.Red);
        }
    }
}
