using System.Drawing;
using ModernFormsNext.Designer.Layout;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerLogicalPaintScopeTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    public void DesignerPanelChromeAndContentFillScaledBackingBitmap(double dpiScale)
    {
        const int logicalWidth = 160;
        const int logicalHeight = 100;
        var deviceWidth = (int)Math.Round(logicalWidth * dpiScale);
        var deviceHeight = (int)Math.Round(logicalHeight * dpiScale);
        var imageInfo = new SKImageInfo(deviceWidth, deviceHeight, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        using var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var panel = new LogicalTestPanel
        {
            Bounds = new Rectangle(0, 0, logicalWidth, logicalHeight)
        };
        panel.Paint(new PaintEventArgs(imageInfo, canvas, dpiScale));
        canvas.Flush();

        var headerSampleY = Math.Min(deviceHeight - 1, (int)Math.Round(10 * dpiScale));
        Assert.Equal(DesignerColors.PanelHeader, bitmap.GetPixel(deviceWidth - 4, headerSampleY));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(deviceWidth - 4, deviceHeight - 4));
    }

    [Fact]
    public void DesignerPanelComposesChildAfterRestoringLogicalCanvasTransform()
    {
        const double dpiScale = 1.25;
        const int logicalWidth = 160;
        const int logicalHeight = 100;
        var imageInfo = new SKImageInfo(
            (int)Math.Round(logicalWidth * dpiScale),
            (int)Math.Round(logicalHeight * dpiScale),
            SKImageInfo.PlatformColorType,
            SKAlphaType.Premul);

        using var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var panel = new ChildCompositionTestPanel
        {
            Bounds = new Rectangle(0, 0, logicalWidth, logicalHeight)
        };
        panel.Paint(new PaintEventArgs(imageInfo, canvas, dpiScale));
        canvas.Flush();

        // This detached test control has a 1x backing bitmap. If base.OnPaint is accidentally
        // invoked inside the 1.25x designer scope, its visible origin moves from (20, 40) to
        // (25, 50). The sample therefore protects the transform boundary directly.
        Assert.True(panel.Child.WasPainted);
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(21, 41));
    }

    private sealed class LogicalTestPanel : DesignerPanelBase
    {
        public LogicalTestPanel()
            : base("DPI test")
        {
        }

        public void Paint(PaintEventArgs e)
            => OnPaint(e);

        protected override void OnPaintContent(PaintEventArgs e)
        {
            Assert.Equal(1d, e.Scaling);
            e.Canvas.FillRectangle(0, HeaderHeight, Width, Height - HeaderHeight, SKColors.Red);
        }
    }

    private sealed class ChildCompositionTestPanel : DesignerPanelBase
    {
        public ChildCompositionTestPanel()
            : base("Child composition test")
        {
            Child = Controls.Add(new ChildPaintProbe
            {
                Bounds = new Rectangle(20, 40, 20, 10)
            });
        }

        public ChildPaintProbe Child { get; }

        // Detached framework controls normally report Visible=false because they have no window.
        // The paint test supplies its own backing canvas, so expose the panel as a visible host.
        public override bool Visible
        {
            get => true;
            set { }
        }

        public void Paint(PaintEventArgs e)
            => OnPaint(e);

        protected override void OnPaintContent(PaintEventArgs e)
            => e.Canvas.FillRectangle(0, HeaderHeight, Width, Height - HeaderHeight, SKColors.Red);
    }

    private sealed class ChildPaintProbe : Control
    {
        public bool WasPainted { get; private set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            WasPainted = true;
            e.Canvas.Clear(SKColors.Lime);
        }
    }
}
