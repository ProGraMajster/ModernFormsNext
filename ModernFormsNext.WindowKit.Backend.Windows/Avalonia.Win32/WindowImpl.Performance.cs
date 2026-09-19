using ModernFormsNext.WindowKit.Diagnostics;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

internal partial class WindowImpl
{
    private PlatformPerformanceFrameScope BeginPerformanceFrame()
    {
        if (!PlatformPerformanceDiagnostics.IsEnabled) return default;
        try { return PlatformPerformanceDiagnostics.BeginFrame(this, CapturePerformanceInfo()); }
        catch { return default; } // Optional metadata must never prevent native painting/cleanup.
    }

    private void UpdatePerformanceFrame(in PlatformPerformanceFrameScope frame)
    {
        if (!PlatformPerformanceDiagnostics.IsEnabled || _hwnd == IntPtr.Zero) return;
        try { frame.UpdateInfo(CapturePerformanceInfo()); }
        catch { /* Only optional metadata failed; preserve the original painting outcome. */ }
    }

    // Called only when profiling is enabled. Existing native size/scaling reads are observational;
    // no surface lock, allocation, invalidation or accessibility/layout getter is required.
    private PlatformRenderInfo CapturePerformanceInfo()
    {
        var logical = ClientSize;
        double scale = RenderScaling;
        var backing = _framebuffer.AllocatedSize;
        int? rowBytes = _framebuffer.AllocatedRowBytes;
        return new PlatformRenderInfo(PlatformRenderBoundary.WindowPaint,
            PlatformRenderBackend.Windows, PlatformRenderMode.Software,
            logical.Width, logical.Height,
            backing?.Width, backing?.Height, rowBytes, "BGRA8888", scale,
            backing is { } size && rowBytes is { } stride ? (long)stride * size.Height : null,
            HostGeneration: 1, BackingGeneration: _framebuffer.BackingGeneration,
            FullRedraw: _framebuffer.IsFullPaint);
    }
}
