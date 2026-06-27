using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for the <see cref="PrintDocument.PrintPage"/> event.
    /// </summary>
    /// <remarks>
    /// <see cref="Canvas"/> uses the same logical page unit as the other printing types:
    /// one unit is one one-hundredth of an inch. Callers can draw directly with SkiaSharp.
    /// </remarks>
    public class PrintPageEventArgs : PrintEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PrintPageEventArgs"/> class.
        /// </summary>
        /// <param name="canvas">The Skia canvas for the page.</param>
        /// <param name="marginBounds">The printable margin rectangle in hundredths of an inch.</param>
        /// <param name="pageBounds">The full page rectangle in hundredths of an inch.</param>
        /// <param name="pageSettings">The settings used for this page.</param>
        /// <exception cref="ArgumentNullException"><paramref name="canvas"/> or <paramref name="pageSettings"/> is <see langword="null"/>.</exception>
        public PrintPageEventArgs(SKCanvas canvas, Rectangle marginBounds, Rectangle pageBounds, PageSettings pageSettings)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(pageSettings);

            Canvas = canvas;
            MarginBounds = marginBounds;
            PageBounds = pageBounds;
            PageSettings = pageSettings;
        }

        /// <summary>
        /// Gets the Skia canvas for the page.
        /// </summary>
        public SKCanvas Canvas { get; }

        /// <summary>
        /// Gets or sets a value indicating whether another page should be printed.
        /// </summary>
        public bool HasMorePages { get; set; }

        /// <summary>
        /// Gets the printable margin rectangle in hundredths of an inch.
        /// </summary>
        public Rectangle MarginBounds { get; }

        /// <summary>
        /// Gets the full page rectangle in hundredths of an inch.
        /// </summary>
        public Rectangle PageBounds { get; }

        /// <summary>
        /// Gets the settings used for this page.
        /// </summary>
        public PageSettings PageSettings { get; }
    }
}
