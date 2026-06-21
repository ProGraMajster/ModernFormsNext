using System;

namespace ModernFormsNext.WindowKit.Platform
{
    /// <summary>
    /// Provides a basic immutable implementation of <see cref="ILockedFramebuffer"/>.
    /// </summary>
    public class LockedFramebuffer : ILockedFramebuffer
    {
        private readonly Action? _onDispose;

        /// <summary>
        /// Initializes a new instance of the <see cref="LockedFramebuffer"/> class.
        /// </summary>
        /// <param name="address">The address of the first pixel.</param>
        /// <param name="size">The framebuffer size in device pixels.</param>
        /// <param name="rowBytes">The number of bytes between the start of adjacent rows.</param>
        /// <param name="dpi">The DPI of the screen or surface backing the framebuffer.</param>
        /// <param name="format">The pixel format used by the framebuffer.</param>
        /// <param name="onDispose">An optional callback invoked when the framebuffer is disposed.</param>
        public LockedFramebuffer(IntPtr address, PixelSize size, int rowBytes, Vector dpi, PixelFormat format,
            Action? onDispose)
        {
            _onDispose = onDispose;
            Address = address;
            Size = size;
            RowBytes = rowBytes;
            Dpi = dpi;
            Format = format;
        }

        /// <inheritdoc />
        public IntPtr Address { get; }

        /// <inheritdoc />
        public PixelSize Size { get; }

        /// <inheritdoc />
        public int RowBytes { get; }

        /// <inheritdoc />
        public Vector Dpi { get; }

        /// <inheritdoc />
        public PixelFormat Format { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            _onDispose?.Invoke();
        }
    }
}
