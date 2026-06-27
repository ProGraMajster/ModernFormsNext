using System;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a rendered print-preview page.
    /// </summary>
    /// <remarks>
    /// Instances own their <see cref="Bitmap"/> and must be disposed when a preview is refreshed
    /// or closed. The type is public so advanced callers can render a <see cref="PrintDocument"/>
    /// into pages for custom preview surfaces.
    /// </remarks>
    public sealed class PrintPreviewPage : IDisposable
    {
        private bool disposed;

        internal PrintPreviewPage(SKBitmap bitmap, PageSettings pageSettings)
        {
            Bitmap = bitmap;
            PageSettings = pageSettings;
        }

        /// <summary>
        /// Gets the rendered page bitmap.
        /// </summary>
        public SKBitmap Bitmap { get; }

        /// <summary>
        /// Gets the page settings used for the rendered page.
        /// </summary>
        public PageSettings PageSettings { get; }

        /// <summary>
        /// Releases the bitmap owned by this page.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            Bitmap.Dispose();
            disposed = true;
        }
    }
}
