using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.Rendering;

/// <summary>Application-lifetime renderer policy, separate from the native windowing backend.</summary>
internal interface IRenderingBackend
{
    RenderingBackend RequestedBackend { get; }
    RenderingBackend ActiveBackend { get; }
    string Renderer { get; }

    /// <summary>Creates a window-owned adapter without acquiring native resources or owning the window.</summary>
    IWindowRenderSurface CreateSurface(ITopLevelImpl window);
}
