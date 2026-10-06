using ModernFormsNext.WindowKit;

namespace ModernFormsNext.Rendering;

/// <summary>UI-thread-owned rendering target for an existing window/presentation lifetime.</summary>
/// <remarks>
/// Acquire reads current backing/scale/size, including after resize or host recreation. Disposing
/// prevents new acquisitions; in-flight frames retain their lease until the paint stack unwinds.
/// The platform window owns native presentation and must outlive its active paint callback.
/// </remarks>
internal interface IWindowRenderSurface : IDisposable
{
    /// <summary>Acquires current backing for logical window damage. The caller must dispose the frame.</summary>
    IRenderFrame AcquireFrame(Rect damage);
}
