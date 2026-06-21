using System;

namespace ModernFormsNext.WindowKit.Platform
{
    /// <summary>
    /// Represents a locked framebuffer that can be written to by a renderer.
    /// </summary>
    /// <remarks>
    /// The framebuffer remains valid until it is disposed. Callers must respect
    /// <see cref="RowBytes"/>, <see cref="Format"/>, and <see cref="Dpi"/> when writing pixels.
    /// </remarks>
    public interface ILockedFramebuffer : IDisposable
    {
        /// <summary>
        /// Address of the first pixel
        /// </summary>
        IntPtr Address { get; }

        /// <summary>
        /// Gets the framebuffer size in device pixels.
        /// </summary>
        PixelSize Size{ get; }
        
        /// <summary>
        /// Number of bytes per row
        /// </summary>
        int RowBytes { get; }
        
        /// <summary>
        /// DPI of underling screen
        /// </summary>
        Vector Dpi { get; }
        
        /// <summary>
        /// Pixel format
        /// </summary>
        PixelFormat Format { get; }
    }
}
