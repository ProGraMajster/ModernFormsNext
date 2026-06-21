using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Metadata;

#nullable enable

namespace ModernFormsNext.WindowKit.Platform
{
    /// <summary>
    /// Creates platform cursor implementations for standard and bitmap-backed cursors.
    /// </summary>
    /// <remarks>
    /// This is a backend-facing service. Application code should normally use the higher-level
    /// cursor abstractions exposed by controls instead of implementing this interface directly.
    /// </remarks>
    [PrivateApi]
    public interface ICursorFactory
    {
        /// <summary>
        /// Gets a platform cursor implementation for a standard cursor shape.
        /// </summary>
        /// <param name="cursorType">The standard cursor shape to request.</param>
        /// <returns>The platform cursor implementation.</returns>
        ICursorImpl GetCursor(StandardCursorType cursorType);

        /// <summary>
        /// Creates a custom cursor from a bitmap and hot spot.
        /// </summary>
        /// <param name="cursor">The bitmap used as the cursor image.</param>
        /// <param name="hotSpot">The cursor hot spot in bitmap pixels.</param>
        /// <returns>The platform cursor implementation.</returns>
        ICursorImpl CreateCursor(IBitmapImpl cursor, PixelPoint hotSpot);
    }
}
