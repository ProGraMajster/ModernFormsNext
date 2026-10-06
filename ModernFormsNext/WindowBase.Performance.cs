using ModernFormsNext.Diagnostics;
using ModernFormsNext.Rendering;

namespace ModernFormsNext;

public abstract partial class WindowBase
{
    private PerformanceRenderScope BeginPerformanceRender(IRenderFrame frame)
    {
        if (!PerformanceRecorder.IsEnabled) return default;
        // Frame metadata describes the renderer; the enclosing native boundary still owns
        // host identity, presentation timing and host/backing generations.
        return PerformanceRecorder.BeginRender(adapter, frame.RenderInfo);
    }
}
