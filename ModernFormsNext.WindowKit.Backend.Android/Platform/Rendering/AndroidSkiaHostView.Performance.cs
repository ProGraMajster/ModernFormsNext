using ModernFormsNext.WindowKit.Diagnostics;
using SkiaSharp;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

public sealed partial class AndroidSkiaHostView
{
    private long performanceHostGeneration;
    private int? performancePixelWidth;
    private int? performancePixelHeight;
    private int? performanceRowBytes;
    private string? performanceFormat;

    /// <inheritdoc/>
    protected override void OnDraw(global::Android.Graphics.Canvas canvas)
    {
        using var performance = BeginPerformanceFrame();
        try
        {
            // Pinned SKCanvasView rasterizes through OnPaintSurface, then draws its bitmap to
            // the Android Canvas. Wrapping base observes both without copying that pipeline.
            // Callback elapsed time still is not GPU execution or compositor presentation.
            base.OnDraw(canvas);
            performance.Complete();
        }
        finally
        {
            if (!disposed && PlatformPerformanceDiagnostics.IsEnabled)
            {
                try { performance.UpdateInfo(CapturePerformanceInfo()); }
                catch { /* Metadata failure must not replace renderer failure or native cleanup. */ }
            }
        }
    }

    private PlatformPerformanceFrameScope BeginPerformanceFrame()
    {
        if (disposed || !state.CanRender || !PlatformPerformanceDiagnostics.IsEnabled) return default;
        try { return PlatformPerformanceDiagnostics.BeginFrame(this, CapturePerformanceInfo()); }
        catch { return default; }
    }

    private PlatformRenderInfo CapturePerformanceInfo()
    {
        float density = Density;
        return new PlatformRenderInfo(PlatformRenderBoundary.AndroidViewDraw,
            PlatformRenderBackend.Android, PlatformRenderMode.Software,
            state.LogicalWidth, state.LogicalHeight,
            performancePixelWidth, performancePixelHeight, performanceRowBytes, performanceFormat,
            density, performancePixelHeight is { } height && performanceRowBytes is { } stride
                ? (long)height * stride : null,
            HostGeneration: performanceHostGeneration, FullRedraw: true);
    }

    private void CapturePaintPerformanceInfo(in SKImageInfo info)
    {
        if (!PlatformPerformanceDiagnostics.IsEnabled) return;
        performancePixelWidth = info.Width;
        performancePixelHeight = info.Height;
        performanceRowBytes = info.RowBytes;
        performanceFormat = info.ColorType switch
        {
            SKColorType.Bgra8888 => "BGRA8888",
            SKColorType.Rgba8888 => "RGBA8888",
            SKColorType.Rgb565 => "RGB565",
            SKColorType.RgbaF16 => "RGBAF16",
            SKColorType.Gray8 => "Gray8",
            _ => null
        };
    }

    private void ResetPerformanceBackingInfo()
    {
        // SKCanvasView owns its SurfaceFactory. Attachment/resize are observable, but its
        // allocation identity is private; never label a Java attach count a GPU/backing reset.
        performancePixelWidth = performancePixelHeight = performanceRowBytes = null;
        performanceFormat = null;
    }
}
