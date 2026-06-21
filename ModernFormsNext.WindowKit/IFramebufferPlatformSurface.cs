using ModernFormsNext.WindowKit.Metadata;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Controls.Platform.Surfaces
{
    /// <summary>
    /// Represents a platform surface that exposes a lockable framebuffer for custom rendering.
    /// </summary>
    /// <remarks>
    /// ModernFormsNext uses framebuffer surfaces to draw SkiaSharp content into a native window.
    /// The returned framebuffer must be disposed after drawing so the backend can present the
    /// updated pixels to the actual window surface.
    /// </remarks>
    [Unstable]
    public interface IFramebufferPlatformSurface
    {
        /// <summary>
        /// Locks the framebuffer and returns drawing information for the current frame.
        /// </summary>
        /// <returns>
        /// A locked framebuffer. Dispose the returned object after drawing to release the frame.
        /// </returns>
        ILockedFramebuffer Lock();
    }
}
