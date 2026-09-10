using System.Drawing;
using ModernFormsNext.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class RenderedSnapshotTests
{
    [Fact]
    public void CaptureUsesProductionWindowPaintAndExposesSurfaceOnlyDuringPaint()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new PaintProbeForm { UseSystemDecorations = true };
        TestWindowHost window = host.Show(form, 40, 30);
        form.DuringPaint = () => Assert.Single(window.Backend.Surfaces);
        Assert.Empty(window.Backend.Surfaces);

        using RenderedSnapshot snapshot = window.CaptureRenderedSnapshot();

        Assert.Equal(1, form.PaintCalls);
        Assert.Equal(SKColors.Crimson, snapshot.GetPixel(20, 15));
        Assert.Empty(window.Backend.Surfaces);
        Assert.False(window.Backend.HasNativeWindow);
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void NestedChildrenUseProductionPaintOrderAndParentClipping(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red);
        var parent = new ColorControl(SKColors.Blue) { Bounds = new Rectangle(10, 10, 20, 20) };
        var clippedChild = new ColorControl(SKColors.Lime) { Bounds = new Rectangle(15, 0, 30, 20) };
        parent.Controls.Add(clippedChild);
        root.Controls.Add(parent);
        TestWindowHost window = host.Show(root, new TestViewport(64, 48, scale));
        Control? originalParent = root.Parent;

        using RenderedSnapshot snapshot = window.CaptureRenderedSnapshot();

        Assert.Equal((int)(64 * scale), snapshot.PixelWidth);
        Assert.Equal((int)(48 * scale), snapshot.PixelHeight);
        Assert.Equal(SKColors.Blue, snapshot.GetPixel((int)(12 * scale), (int)(15 * scale)));
        Assert.Equal(SKColors.Lime, snapshot.GetPixel((int)(27 * scale), (int)(15 * scale)));
        Assert.Equal(SKColors.Red, snapshot.GetPixel((int)(35 * scale), (int)(15 * scale)));
        Assert.Same(originalParent, root.Parent);
        Assert.Same(parent, clippedChild.Parent);
    }

    [Fact]
    public void CapturesRetainPixelsAfterMutationResizeAndWindowClose()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red);
        TestWindowHost window = host.Show(root, 30, 20);
        using RenderedSnapshot first = window.CaptureRenderedSnapshot();
        root.Color = SKColors.Blue;
        using RenderedSnapshot second = window.CaptureRenderedSnapshot();
        window.Resize(50, 40);
        root.Color = SKColors.Lime;
        using RenderedSnapshot third = window.CaptureRenderedSnapshot();
        window.Close();

        Assert.Equal(SKColors.Red, first.GetPixel(15, 10));
        Assert.Equal(SKColors.Blue, second.GetPixel(15, 10));
        Assert.Equal(SKColors.Lime, third.GetPixel(45, 35));
        Assert.Equal(50, third.PixelWidth);
        Assert.Equal(40, third.PixelHeight);
        Assert.Throws<ObjectDisposedException>(() => window.CaptureRenderedSnapshot());
    }

    [Fact]
    public void NativeShapeRendererPreservesTransparentCornersAndBrushMutation()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red);
        var fill = new SolidColorBrush(Color.Blue);
        var ellipse = new Ellipse { Bounds = new Rectangle(10, 10, 30, 20), Fill = fill, Stroke = null };
        root.Controls.Add(ellipse);
        TestWindowHost window = host.Show(root, 60, 40);
        using RenderedSnapshot first = window.CaptureRenderedSnapshot();
        ellipse.Fill = new SolidColorBrush(Color.Lime);
        using RenderedSnapshot second = window.CaptureRenderedSnapshot();

        Assert.Equal(SKColors.Red, first.GetPixel(11, 11));
        Assert.Equal(SKColors.Blue, first.GetPixel(25, 20));
        Assert.Equal(SKColors.Lime, second.GetPixel(25, 20));
    }

    [Fact]
    public void PixelAccessExpandsPremultipliedAlphaAndCopyDocumentsNativeChannelOrder()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new PaintProbeForm
        {
            UseSystemDecorations = true,
            PaintColor = new SKColor(128, 64, 32, 128)
        };
        TestWindowHost window = host.Show(form, 20, 15);
        using RenderedSnapshot snapshot = window.CaptureRenderedSnapshot();

        Assert.Equal(form.PaintColor, snapshot.GetPixel(10, 10));
        byte[] pixels = snapshot.CopyPixels();
        int offset = 10 * snapshot.RowBytes + 10 * 4;
        Assert.Equal(new byte[] { 16, 32, 64, 128 }, pixels.AsSpan(offset, 4).ToArray());
    }

    [Fact]
    public void FractionalPixelDimensionsMatchProductionWindowTruncation()
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost window = host.Show(new ColorControl(SKColors.Blue), new TestViewport(11, 9, 1.25));

        using RenderedSnapshot first = window.CaptureRenderedSnapshot();
        window.SetRenderScale(2);
        using RenderedSnapshot second = window.CaptureRenderedSnapshot();

        Assert.Equal(13, first.PixelWidth);
        Assert.Equal(11, first.PixelHeight);
        Assert.Equal(1.25, first.RenderScale);
        Assert.Equal(22, second.PixelWidth);
        Assert.Equal(18, second.PixelHeight);
        Assert.Equal(SKColors.Blue, second.GetPixel(20, 16));
    }

    [Fact]
    public void WindowsCaptureIndependentlyAndSnapshotsSurviveHostDisposal()
    {
        RenderedSnapshot first;
        RenderedSnapshot second;
        using (var host = ModernFormsTestHost.Create())
        {
            TestWindowHost redWindow = host.Show(new ColorControl(SKColors.Red), 20, 15);
            TestWindowHost blueWindow = host.Show(new ColorControl(SKColors.Blue), 40, 30);
            first = redWindow.CaptureRenderedSnapshot();
            second = blueWindow.CaptureRenderedSnapshot();
            Assert.Empty(redWindow.Backend.Surfaces);
            Assert.Empty(blueWindow.Backend.Surfaces);
        }
        using (first)
        using (second)
        {
            Assert.Equal(SKColors.Red, first.GetPixel(10, 10));
            Assert.Equal(SKColors.Blue, second.GetPixel(35, 25));
        }
    }

    [Fact]
    public void PixelCopiesAreDetachedAndPngEncodingRoundTrips()
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost window = host.Show(new ColorControl(SKColors.Crimson), 20, 15);
        using RenderedSnapshot snapshot = window.CaptureRenderedSnapshot();
        byte[] copy = snapshot.CopyPixels();
        Array.Clear(copy);
        byte[] png = snapshot.EncodePng();
        snapshot.Dispose();
        using var decoded = SKBitmap.Decode(png);

        Assert.Equal(20, decoded.Width);
        Assert.Equal(15, decoded.Height);
        Assert.Equal(SKColors.Crimson, decoded.GetPixel(10, 10));
        Assert.Equal(80, snapshot.RowBytes);
        Assert.True(snapshot.IsDisposed);
        Assert.Throws<ObjectDisposedException>(snapshot.CopyPixels);
        Assert.Throws<ObjectDisposedException>(() => snapshot.GetPixel(0, 0));
        Assert.Throws<ObjectDisposedException>(snapshot.EncodePng);
        snapshot.Dispose();
    }

    [Fact]
    public void PaintExceptionReleasesSurfaceAndNextCaptureCanRecover()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red) { FailPaint = true };
        TestWindowHost window = host.Show(root, 20, 15);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => window.CaptureRenderedSnapshot());

        Assert.Equal("Injected paint failure.", error.Message);
        Assert.Empty(window.Backend.Surfaces);
        root.FailPaint = false;
        root.Color = SKColors.Blue;
        using RenderedSnapshot snapshot = window.CaptureRenderedSnapshot();
        Assert.Equal(SKColors.Blue, snapshot.GetPixel(10, 10));
        Assert.Empty(window.Backend.Surfaces);
    }

    [Fact]
    public void CaptureDoesNotDrainWorkQueuedDuringPainting()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red);
        TestWindowHost window = host.Show(root, 20, 15);
        root.DuringPaint = () => host.Dispatcher.Post(() => root.Color = SKColors.Blue);

        using RenderedSnapshot first = window.CaptureRenderedSnapshot();

        Assert.Equal(SKColors.Red, first.GetPixel(10, 10));
        Assert.True(host.Dispatcher.PendingWorkCount > 0);
        root.DuringPaint = null;
        using RenderedSnapshot second = window.CaptureRenderedSnapshot();
        Assert.Equal(SKColors.Blue, second.GetPixel(10, 10));
    }

    [Fact]
    public void RecursiveCaptureIsRejectedAndDoesNotLeaveWindowInCaptureScope()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red);
        TestWindowHost window = host.Show(root, 20, 15);
        root.DuringPaint = () => window.CaptureRenderedSnapshot();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => window.CaptureRenderedSnapshot());

        Assert.Contains("recursively", error.Message, StringComparison.Ordinal);
        Assert.Empty(window.Backend.Surfaces);
        root.DuringPaint = null;
        using RenderedSnapshot snapshot = window.CaptureRenderedSnapshot();
        Assert.Equal(SKColors.Red, snapshot.GetPixel(10, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(16_777_217)]
    public void InvalidPixelBudgetsAreRejectedBeforePainting(int maximumPixels)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red);
        TestWindowHost window = host.Show(root, 20, 15);

        Assert.Throws<ArgumentOutOfRangeException>(() => window.CaptureRenderedSnapshot(maximumPixels));

        Assert.Equal(0, root.PaintCalls);
        Assert.Empty(window.Backend.Surfaces);
    }

    [Fact]
    public void FramebufferAreaBudgetIncludesRenderScaleAndIsInclusive()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red);
        TestWindowHost window = host.Show(root, new TestViewport(20, 15, 2));

        Assert.Throws<InvalidOperationException>(() => window.CaptureRenderedSnapshot(1199));
        Assert.Equal(0, root.PaintCalls);
        Assert.Empty(window.Backend.Surfaces);
        using RenderedSnapshot snapshot = window.CaptureRenderedSnapshot(1200);
        Assert.Equal(40, snapshot.PixelWidth);
        Assert.Equal(30, snapshot.PixelHeight);
    }

    [Fact]
    public void TinyScaleWithNoWholeDevicePixelIsRejectedBeforePainting()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red);
        TestWindowHost window = host.Show(root, new TestViewport(20, 15, 0.01));

        Assert.Throws<InvalidOperationException>(() => window.CaptureRenderedSnapshot());

        Assert.Equal(0, root.PaintCalls);
        Assert.Empty(window.Backend.Surfaces);
    }

    [Fact]
    public void CaptureRejectsForeignThreadWhileDetachedSnapshotCanBeReadThere()
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost window = host.Show(new ColorControl(SKColors.Red), 20, 15);
        using RenderedSnapshot snapshot = window.CaptureRenderedSnapshot();

        Exception? error = null;
        Exception? readError = null;
        SKColor pixel = default;
        // The test must retain the host's creating thread for disposal. A dedicated worker
        // exercises synchronous affinity rejection without awaiting onto another test thread
        // or asking the UI dispatcher to perform work while its owner waits.
        var worker = new Thread(() =>
        {
            error = Record.Exception(() => window.CaptureRenderedSnapshot());
            readError = Record.Exception(() => pixel = snapshot.GetPixel(10, 10));
        });
        worker.Start();
        worker.Join();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Null(readError);
        Assert.Equal(SKColors.Red, pixel);
        Assert.Empty(window.Backend.Surfaces);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(20, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 15)]
    public void PixelReadsRejectCoordinatesOutsideImage(int x, int y)
    {
        using var host = ModernFormsTestHost.Create();
        TestWindowHost window = host.Show(new ColorControl(SKColors.Red), 20, 15);
        using RenderedSnapshot snapshot = window.CaptureRenderedSnapshot();

        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.GetPixel(x, y));
    }

    private sealed class PaintProbeForm : Form
    {
        internal int PaintCalls { get; private set; }
        internal Action? DuringPaint { get; set; }
        internal SKColor PaintColor { get; set; } = SKColors.Crimson;

        protected override void OnPaintBackground(PaintEventArgs e) => e.Canvas.Clear(PaintColor);

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintCalls++;
            DuringPaint?.Invoke();
            base.OnPaint(e);
        }
    }

    private sealed class ColorControl(SKColor initialColor) : Control
    {
        private SKColor color = initialColor;

        internal SKColor Color
        {
            get => color;
            set
            {
                color = value;
                Invalidate();
            }
        }

        internal int PaintCalls { get; private set; }
        internal bool FailPaint { get; set; }
        internal Action? DuringPaint { get; set; }

        protected override void OnPaintBackground(PaintEventArgs e) => e.Canvas.Clear(color);

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintCalls++;
            if (FailPaint)
                throw new InvalidOperationException("Injected paint failure.");
            DuringPaint?.Invoke();
            base.OnPaint(e);
        }
    }
}
