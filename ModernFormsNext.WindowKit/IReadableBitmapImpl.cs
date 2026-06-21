namespace ModernFormsNext.WindowKit.Platform;

/// <summary>
/// Represents a bitmap implementation whose pixel data can be locked for reading.
/// </summary>
public interface IReadableBitmapImpl
{
    /// <summary>
    /// Gets the bitmap pixel format, or <see langword="null"/> when the backend cannot expose it.
    /// </summary>
    PixelFormat? Format { get; }

    /// <summary>
    /// Locks the bitmap and returns a framebuffer view over its pixels.
    /// </summary>
    /// <returns>A locked framebuffer. Dispose the returned object to release the lock.</returns>
    ILockedFramebuffer Lock();
}
