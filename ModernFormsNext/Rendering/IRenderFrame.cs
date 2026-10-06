using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit;
using SkiaSharp;

namespace ModernFormsNext.Rendering;

/// <summary>A scoped drawing lease. Canvas and metadata are valid only until Complete or Dispose.</summary>
/// <remarks>
/// Do not retain a frame or canvas beyond its using scope. A disposed lease must never become
/// usable again, even when the implementation pools its internal resource holders.
/// Complete seals successful drawing; Dispose releases and presents the frame after inner paint
/// and profiler scopes unwind. Dispose also releases an incomplete frame after failure.
/// Presentation policy belongs to the implementation: software unlock preserves legacy presentation
/// even on paint failure, since ILockedFramebuffer has no separate abort operation.
/// </remarks>
internal interface IRenderFrame : IDisposable
{
    SKCanvas Canvas { get; }
    SKImageInfo ImageInfo { get; }
    Size LogicalSize { get; }
    double Scale { get; }
    Rect Damage { get; }
    PerformanceRenderInfo RenderInfo { get; }
    void Complete();
}
