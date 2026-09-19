using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext;

public abstract partial class WindowBase
{
    private PerformanceRenderScope BeginPerformanceRender(ILockedFramebuffer framebuffer, Rect damage)
    {
        if (!PerformanceRecorder.IsEnabled) return default;
        // The native boundary supplies backend/host generation. These are the actual locked
        // raster facts; obtaining them neither allocates another surface nor guesses GPU usage.
        return PerformanceRecorder.BeginRender(adapter, new PerformanceRenderInfo
        {
            Acceleration = PerformanceAcceleration.Software,
            Boundary = PerformanceFrameBoundary.SharedRender,
            Scale = Scaling,
            LogicalWidth = (int)window.ClientSize.Width,
            LogicalHeight = (int)window.ClientSize.Height,
            PixelWidth = framebuffer.Size.Width,
            PixelHeight = framebuffer.Size.Height,
            PixelFormat = framebuffer.Format == PixelFormat.Bgra8888 ? "BGRA8888" :
                framebuffer.Format == PixelFormat.Rgba8888 ? "RGBA8888" :
                framebuffer.Format == PixelFormat.Rgb565 ? "RGB565" : null,
            RowBytes = framebuffer.RowBytes,
            BackingBytes = (long)framebuffer.RowBytes * framebuffer.Size.Height,
            Redraw = damage.Contains(new Rect(window.ClientSize)) ? PerformanceRedraw.FullSurface : PerformanceRedraw.PartialSurface
        });
    }
}
