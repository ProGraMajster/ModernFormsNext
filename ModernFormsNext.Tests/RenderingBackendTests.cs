using System.Diagnostics;
using Xunit;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.Rendering;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Controls;
using ModernFormsNext.WindowKit.Controls.Platform.Surfaces;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Raw;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Skia;
using SkiaSharp;

namespace ModernFormsNext.Tests;

public sealed class RenderingBackendTests
{
    [Theory]
    [InlineData(RenderingBackend.Auto)]
    [InlineData(RenderingBackend.Software)]
    public void PolicyResolvesToHonestSoftwareMetadata(RenderingBackend requested)
    {
        var backend = new SoftwareRenderingBackend(requested);
        using var pixels = new Pixels(60, 40);
        var window = new Target { Surfaces = new object[] { pixels }, ClientSize = new Size(40, 26.5), RenderScaling = 1.5 };
        using var surface = backend.CreateSurface(window);
        using var frame = surface.AcquireFrame(new Rect(2.25, 3.75, 10.5, 9.25));
        Assert.Equal(requested, backend.RequestedBackend);
        Assert.Equal(RenderingBackend.Software, backend.ActiveBackend);
        Assert.Equal(new SKImageInfo(60, 40, SKColorType.Bgra8888, SKAlphaType.Premul), frame.ImageInfo);
        Assert.Equal(window.ClientSize, frame.LogicalSize);
        Assert.Equal(1.5, frame.Scale);
        Assert.Equal(new Rect(2.25, 3.75, 10.5, 9.25), frame.Damage);
        Assert.Equal(requested, frame.RenderInfo.RequestedBackend);
        Assert.Equal(RenderingBackend.Software, frame.RenderInfo.ActiveBackend);
        Assert.Equal("Skia Raster", frame.RenderInfo.Renderer);
        Assert.Equal(PerformanceAcceleration.Software, frame.RenderInfo.Acceleration);
        Assert.Null(frame.RenderInfo.FallbackReason);
        Assert.Equal(60 * 40 * 4, frame.RenderInfo.BackingBytes);
        Assert.Equal(PerformanceRedraw.PartialSurface, frame.RenderInfo.Redraw);
        frame.Canvas.DrawColor(SKColors.Crimson);
        frame.Complete();
        Assert.Equal(SKColors.Crimson, pixels.Bitmap.GetPixel(5, 5));
    }

    [Fact]
    public void ResizeAndRecreatedPresentationAreResolvedAtEveryAcquisition()
    {
        using var first = new Pixels(40, 30);
        using var second = new Pixels(125, 75);
        var window = new Target { Surfaces = new object[] { first }, ClientSize = new Size(40, 30) };
        using var surface = new SoftwareRenderingBackend(RenderingBackend.Auto).CreateSurface(window);
        using (var frame = surface.AcquireFrame(new Rect(window.ClientSize))) frame.Complete();
        window.Surfaces = Array.Empty<object>();
        Assert.Throws<InvalidOperationException>(() => surface.AcquireFrame(default));
        window.Surfaces = new object[] { second };
        window.ClientSize = new Size(100, 60);
        window.RenderScaling = 1.25;
        using (var frame = surface.AcquireFrame(new Rect(window.ClientSize))) {
            Assert.Equal(125, frame.ImageInfo.Width);
            Assert.Equal(75, frame.ImageInfo.Height);
            Assert.Equal(1.25, frame.Scale);
            Assert.Equal(PerformanceRedraw.FullSurface, frame.RenderInfo.Redraw);
        }
        Assert.Equal(1, first.Locks);
        Assert.Equal(1, second.Locks);
        Assert.Equal(0, first.ActiveLocks);
        Assert.Equal(0, second.ActiveLocks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PixelMappingMatchesThePreviousRasterPath(int kind)
    {
        using var pixels = new Pixels(20, 12, kind == 0 ? SKColorType.Bgra8888 : kind == 1 ? SKColorType.Rgba8888 : SKColorType.Rgb565);
        using var surface = new SoftwareRenderingBackend(RenderingBackend.Software).CreateSurface(
            new Target { Surfaces = new object[] { pixels } });
        using var frame = surface.AcquireFrame(default);
        Assert.Equal(pixels.Bitmap.ColorType, frame.ImageInfo.ColorType);
        Assert.Equal(kind == 2 ? SKAlphaType.Opaque : SKAlphaType.Premul, frame.ImageInfo.AlphaType);
        Assert.Equal(pixels.Bitmap.RowBytes, frame.RenderInfo.RowBytes);
    }

    [Theory]
    [InlineData("lock")]
    [InlineData("metadata")]
    [InlineData("surface")]
    [InlineData("paint")]
    [InlineData("unlock")]
    public void FailuresReleaseTheFrameAndPermitSubsequentPainting(string failure)
    {
        using var pixels = new Pixels(20, 12);
        using var surface = new SoftwareRenderingBackend(RenderingBackend.Auto).CreateSurface(
            new Target { Surfaces = new object[] { pixels } });
        pixels.Failure = failure;
        Assert.Throws<InvalidOperationException>(() => {
            using var frame = surface.AcquireFrame(default);
            if (failure == "paint") throw new InvalidOperationException("paint");
            frame.Complete();
        });
        Assert.Equal(0, pixels.ActiveLocks);
        pixels.Failure = null;
        using (var frame = surface.AcquireFrame(default)) frame.Complete();
        Assert.Equal(0, pixels.ActiveLocks);
    }

    [Fact]
    public void NestedFramesAreDistinctAndSurfaceDisposalDefersActiveLeaseCleanup()
    {
        using var pixels = new Pixels(20, 12);
        var surface = new SoftwareRenderingBackend(RenderingBackend.Auto).CreateSurface(
            new Target { Surfaces = new object[] { pixels } });
        var outer = surface.AcquireFrame(default);
        var inner = surface.AcquireFrame(default);
        Assert.NotSame(outer, inner);
        Assert.NotSame(outer.Canvas, inner.Canvas);
        Assert.Equal(2, pixels.ActiveLocks);
        inner.Complete();
        inner.Dispose();
        Assert.Equal(1, pixels.ActiveLocks);
        surface.Dispose();
        surface.Dispose();
        Assert.Throws<ObjectDisposedException>(() => surface.AcquireFrame(default));
        Assert.Equal(1, pixels.ActiveLocks);
        outer.Dispose();
        outer.Dispose();
        Assert.Equal(0, pixels.ActiveLocks);
        Assert.Throws<ObjectDisposedException>(() => _ = outer.Canvas);
    }

    [Fact]
    public void DisposedLeaseCannotAccessOrReleaseAReusedFrame()
    {
        using var pixels = new Pixels(20, 12);
        using var surface = new SoftwareRenderingBackend(RenderingBackend.Auto).CreateSurface(
            new Target { Surfaces = new object[] { pixels } });
        var retired = surface.AcquireFrame(default);
        retired.Dispose();
        using var current = surface.AcquireFrame(default);
        Assert.Throws<ObjectDisposedException>(() => _ = retired.Canvas);
        Assert.Throws<ObjectDisposedException>(() => _ = retired.ImageInfo);
        Assert.Throws<ObjectDisposedException>(() => _ = retired.RenderInfo);
        Assert.Throws<ObjectDisposedException>(retired.Complete);
        retired.Dispose();
        Assert.Equal(1, pixels.ActiveLocks);
        current.Canvas.DrawColor(SKColors.Crimson);
        current.Complete();
        Assert.Throws<ObjectDisposedException>(() => _ = current.Canvas);
        Assert.Throws<ObjectDisposedException>(() => _ = current.RenderInfo);
    }

    [Fact]
    public void ForeignThreadCannotReadDrawCompleteOrReleaseAFrame()
    {
        using var pixels = new Pixels(20, 12);
        using var surface = new SoftwareRenderingBackend(RenderingBackend.Auto).CreateSurface(
            new Target { Surfaces = new object[] { pixels } });
        using var frame = surface.AcquireFrame(default);
        Exception? failure = null;
        var thread = new Thread(() => {
            try {
                Assert.Throws<InvalidOperationException>(() => surface.AcquireFrame(default));
                Assert.Throws<InvalidOperationException>(() => _ = frame.Canvas);
                Assert.Throws<InvalidOperationException>(() => _ = frame.ImageInfo);
                Assert.Throws<InvalidOperationException>(() => _ = frame.RenderInfo);
                Assert.Throws<InvalidOperationException>(frame.Complete);
                Assert.Throws<InvalidOperationException>(frame.Dispose);
                Assert.Throws<InvalidOperationException>(surface.Dispose);
            }
            catch (Exception error) { failure = error; }
        });
        thread.Start();
        thread.Join();
        Assert.Null(failure);
        Assert.Equal(1, pixels.ActiveLocks);
        frame.Canvas.DrawColor(SKColors.Crimson);
        frame.Complete();
    }

    [Fact]
    public void MissingDiagnosticRendererDoesNotEraseExistingIdentity()
    {
        using var root = new Panel();
        using var profiler = PerformanceProfiler.StartForTesting(null, () => 0, 1000);
        using (var outer = PerformanceRecorder.BeginRender(root, new() { Renderer = "Borrowed canvas" })) {
            using (var nested = PerformanceRecorder.BeginRender(root, default)) nested.Complete();
            outer.Complete();
        }
        Assert.Equal("Borrowed canvas", profiler.Capture().LatestFrame!.Value.RenderInfo.Renderer);
    }

    [Fact]
    public void WarmFrameAcquisitionDoesNotAddPerFrameAllocationComparedWithOldPath()
    {
        using var pixels = new Pixels(80, 60);
        var target = new Target { Surfaces = new object[] { pixels } };
        using var surface = new SoftwareRenderingBackend(RenderingBackend.Auto).CreateSurface(target);
        void Current() { using var frame = surface.AcquireFrame(default); _ = frame.Canvas; frame.Complete(); }
        void Baseline() {
            var platform = target.Surfaces.OfType<IFramebufferPlatformSurface>().First();
            using var backing = platform.Lock();
            var info = new SKImageInfo(backing.Size.Width, backing.Size.Height, backing.Format.ToSkColorType(), SKAlphaType.Premul);
            using var raster = SKSurface.Create(info, backing.Address, backing.RowBytes);
            _ = raster.Canvas;
        }
        for (int i = 0; i < 256; i++) { Baseline(); Current(); }
        static (long Bytes, double Microseconds) Measure(Action draw) {
            const int count = 2000;
            long bytes = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++) draw();
            return ((GC.GetAllocatedBytesForCurrentThread() - bytes) / count,
                Stopwatch.GetElapsedTime(started).TotalMicroseconds / count);
        }
        var baseline = Measure(Baseline);
        var current = Measure(Current);
        Assert.True(current.Bytes <= baseline.Bytes,
            $"Software frame allocated {current.Bytes} bytes; legacy raster allocated {baseline.Bytes} bytes.");
        if (Environment.GetEnvironmentVariable("MFN_RENDER_OUTPUT") is { Length: > 0 } output) {
            Directory.CreateDirectory(output);
            File.WriteAllText(System.IO.Path.Combine(output, "frame-measurements.json"),
                System.Text.Json.JsonSerializer.Serialize(new {
                    baselineBytes = baseline.Bytes, currentBytes = current.Bytes,
                    baselineMicroseconds = baseline.Microseconds, currentMicroseconds = current.Microseconds }));
        }
    }

    private sealed class Pixels : IFramebufferPlatformSurface, IDisposable
    {
        internal SKBitmap Bitmap { get; }
        internal int Locks, ActiveLocks;
        internal string? Failure;
        private readonly Action unlock;
        internal Pixels(int width, int height, SKColorType type = SKColorType.Bgra8888) {
            Bitmap = new SKBitmap(width, height, type, type == SKColorType.Rgb565 ? SKAlphaType.Opaque : SKAlphaType.Premul);
            unlock = () => { ActiveLocks--; if (Failure == "unlock") throw new InvalidOperationException("unlock"); };
        }
        public ILockedFramebuffer Lock() {
            if (Failure == "lock") throw new InvalidOperationException("lock");
            Locks++; ActiveLocks++;
            var format = Bitmap.ColorType == SKColorType.Rgb565 ? PixelFormat.Rgb565 :
                Bitmap.ColorType == SKColorType.Rgba8888 ? PixelFormat.Rgba8888 : PixelFormat.Bgra8888;
            var result = new LockedFramebuffer(Bitmap.GetPixels(), new PixelSize(Bitmap.Width, Bitmap.Height),
                Failure == "surface" ? 1 : Bitmap.RowBytes, new Vector(96, 96), format, unlock);
            return Failure == "metadata" ? new BadMetadata(result) : result;
        }
        public void Dispose() => Bitmap.Dispose();
    }
    private sealed class BadMetadata(ILockedFramebuffer backing) : ILockedFramebuffer
    {
        public IntPtr Address => backing.Address;
        public PixelSize Size => throw new InvalidOperationException("metadata");
        public int RowBytes => backing.RowBytes;
        public Vector Dpi => backing.Dpi;
        public PixelFormat Format => backing.Format;
        public void Dispose() => backing.Dispose();
    }
    private sealed class Target : ITopLevelImpl
    {
        public Size ClientSize { get; set; } = new(80, 60);
        public Size? FrameSize => ClientSize;
        public double RenderScaling { get; set; } = 1;
        public IEnumerable<object> Surfaces { get; set; } = Array.Empty<object>();
        public Action<RawInputEventArgs>? Input { get; set; }
        public Action<Rect>? Paint { get; set; }
        public Action<Size, WindowResizeReason>? Resized { get; set; }
        public Action<double>? ScalingChanged { get; set; }
        public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }
        public Action? Closed { get; set; }
        public Action? LostFocus { get; set; }
        public WindowTransparencyLevel TransparencyLevel => default;
        public AcrylicPlatformCompensationLevels AcrylicCompensationLevels => default;
        public void SetInputRoot(IInputRoot inputRoot) { }
        public void Invalidate(Rect rect) { }
        public Point PointToClient(PixelPoint point) => default;
        public PixelPoint PointToScreen(Point point) => default;
        public void SetCursor(ICursorImpl? cursor) { }
        public IPopupImpl? CreatePopup() => null;
        public void SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> levels) { }
        public void SetFrameThemeVariant(PlatformThemeVariant variant) { }
        public object? TryGetFeature(Type type) => null;
        public void Dispose() { }
    }
}
