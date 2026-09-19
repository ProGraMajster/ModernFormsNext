using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Diagnostics;

// The null check precedes every timestamp, allocation sample, identity and geometry operation.
// Framework hooks intentionally do not initialize the profiler or schedule any work.
internal static class PerformanceRecorder
{
    internal static bool IsEnabled => PerformanceProfiler.Current is not null;
    internal static bool ShouldRecordRegions => PerformanceProfiler.Current?.ShouldRecordRegions == true;
    internal static PerformanceScope Measure(PerformanceActivityKind activity, Control? control = null)
    {
        var profiler = PerformanceProfiler.Current;
        return profiler is null ? default : new(profiler, profiler.BeginActivity(activity, control));
    }
    internal static PerformanceScope BeginInput(Control? control = null) => PerformanceProfiler.Current?.BeginInput(control) ?? default;
    internal static void Count(PerformanceCounterKind counter, long delta = 1, Control? control = null)
        => PerformanceProfiler.Current?.Count(counter, delta, control);
    internal static PerformanceRenderScope BeginRender(Control root, in PerformanceRenderInfo info)
        => PerformanceProfiler.Current?.BeginRender(root, in info) ?? default;
    internal static void RenderOverlay(SKCanvas canvas, Control root, int logicalWidth, int logicalHeight, double canvasScale)
        => PerformanceProfiler.Current?.RenderOverlay(canvas, root, logicalWidth, logicalHeight, canvasScale);
    internal static void RecordRegion(Control control, RectangleF rootBounds, PerformanceRegionKind kind)
        => PerformanceProfiler.Current?.RecordRegion(control, rootBounds, kind);
    internal static PerformanceResourceToken CaptureResource(PerformanceCounterKind created, PerformanceCounterKind disposed, Control? control = null)
        => PerformanceProfiler.Current?.CaptureResource(created, disposed, control) ?? default;
}
