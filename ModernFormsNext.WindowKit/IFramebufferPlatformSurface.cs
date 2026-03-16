using ModernFormsNext.WindowKit.Metadata;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Controls.Platform.Surfaces
{
    [Unstable]
    public interface IFramebufferPlatformSurface
    {
        /// </summary>
        /// Provides a framebuffer descriptor for drawing.
        /// </summary>
        /// </remarks>
        /// Contents should be drawn on actual window after disposing
        /// </remarks>
        ILockedFramebuffer Lock();
    }
}
