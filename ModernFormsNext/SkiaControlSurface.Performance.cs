using ModernFormsNext.Diagnostics;

namespace ModernFormsNext;

public sealed partial class SkiaControlSurface
{
    private PerformanceRenderScope BeginPerformanceRender(double scaling)
    {
        if (!PerformanceRecorder.IsEnabled) return default;
        // A borrowed canvas does not reveal backing allocation, stride or GPU support. An
        // enclosing native frame can supply those facts; shared-only rendering leaves them null.
        return PerformanceRecorder.BeginRender(Root, new PerformanceRenderInfo
        {
            Boundary = PerformanceFrameBoundary.SharedRender,
            Scale = scaling,
            LogicalWidth = LogicalSize.Width,
            LogicalHeight = LogicalSize.Height,
            Redraw = PerformanceRedraw.FullSurface
        });
    }
}
