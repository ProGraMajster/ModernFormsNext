using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Defines a reusable document that can render pages for printing or print preview.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ModernFormsNext uses SkiaSharp for printing surfaces. Handle <see cref="PrintPage"/> and
    /// draw to <see cref="PrintPageEventArgs.Canvas"/> using page units of one one-hundredth of an inch.
    /// Set <see cref="PrintPageEventArgs.HasMorePages"/> to <see langword="true"/> when another page
    /// should be requested.
    /// </para>
    /// <para>
    /// The current implementation is platform-neutral and renders pages in memory. Physical printer
    /// spooling is intentionally left for a future WindowKit backend service; <see cref="Print"/> still
    /// runs the normal event pipeline so migrated code can validate pagination and preview behavior.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var document = new PrintDocument();
    /// document.PrintPage += (_, e) =>
    /// {
    ///     using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
    ///     using var font = new SKFont(SKTypeface.Default, 24);
    ///     e.Canvas.DrawText("Invoice #1001", e.MarginBounds.Left, e.MarginBounds.Top + 40, font, paint);
    /// };
    ///
    /// var preview = new PrintPreviewDialog { Document = document };
    /// await preview.ShowDialog(this);
    /// </code>
    /// </example>
    [DefaultEvent(nameof(PrintPage))]
    [DefaultProperty(nameof(DocumentName))]
    public class PrintDocument : Component
    {
        private const int MaximumRenderedPages = 1000;

        private PageSettings defaultPageSettings = new ();
        private PrinterSettings printerSettings = new ();
        private string documentName = "document";

        /// <summary>
        /// Occurs before the first page is printed or rendered.
        /// </summary>
        public event PrintEventHandler? BeginPrint;

        /// <summary>
        /// Occurs after printing or rendering completes.
        /// </summary>
        public event PrintEventHandler? EndPrint;

        /// <summary>
        /// Occurs before each page is rendered, allowing callers to adjust page settings per page.
        /// </summary>
        public event QueryPageSettingsEventHandler? QueryPageSettings;

        /// <summary>
        /// Occurs when a page should be rendered.
        /// </summary>
        public event PrintPageEventHandler? PrintPage;

        /// <summary>
        /// Gets or sets the default page settings used when rendering pages.
        /// </summary>
        /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
        public PageSettings DefaultPageSettings {
            get => defaultPageSettings;
            set {
                ArgumentNullException.ThrowIfNull(value);
                defaultPageSettings = value;
            }
        }

        /// <summary>
        /// Gets or sets the document name shown by dialogs and diagnostic output.
        /// </summary>
        public string DocumentName {
            get => documentName;
            set => documentName = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets a value indicating whether drawing should be logically offset to the page margins.
        /// </summary>
        /// <remarks>
        /// When this value is <see langword="true"/>, ModernFormsNext translates the print canvas by
        /// <see cref="PageSettings.Margins"/> before raising <see cref="PrintPage"/>. The event still
        /// receives absolute <see cref="PrintPageEventArgs.MarginBounds"/> and <see cref="PrintPageEventArgs.PageBounds"/>
        /// values for compatibility.
        /// </remarks>
        public bool OriginAtMargins { get; set; }

        /// <summary>
        /// Gets or sets the printer settings used by this document.
        /// </summary>
        /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
        public PrinterSettings PrinterSettings {
            get => printerSettings;
            set {
                ArgumentNullException.ThrowIfNull(value);
                printerSettings = value;
            }
        }

        /// <summary>
        /// Runs the print event pipeline.
        /// </summary>
        /// <remarks>
        /// This method currently renders pages in memory and disposes them immediately. It does not
        /// send output to a physical printer until a platform print backend is added. Use
        /// <see cref="PrintPreviewDialog"/> or <see cref="RenderPreviewPages"/> to inspect rendered output.
        /// </remarks>
        public void Print()
        {
            using var pages = new RenderedPageCollection(RenderPreviewPages());
        }

        /// <summary>
        /// Renders the document into preview pages.
        /// </summary>
        /// <returns>A list of rendered pages. The caller owns and must dispose every page.</returns>
        /// <remarks>
        /// This method is useful for custom preview surfaces. It runs the same print lifecycle events
        /// as <see cref="Print"/> and stops when <see cref="PrintPageEventArgs.HasMorePages"/> is
        /// <see langword="false"/>, when an event cancels printing, or when a safety limit is reached.
        /// </remarks>
        public List<PrintPreviewPage> RenderPreviewPages()
        {
            var pages = new List<PrintPreviewPage>();
            var canceled = false;
            var beginArgs = new PrintEventArgs();

            OnBeginPrint(beginArgs);

            if (beginArgs.Cancel) {
                OnEndPrint(new PrintEventArgs(true));
                return pages;
            }

            try {
                var hasMorePages = true;
                var pageIndex = 0;

                while (hasMorePages) {
                    if (pageIndex >= MaximumRenderedPages)
                        throw new InvalidOperationException($"PrintDocument rendered more than {MaximumRenderedPages} pages. Ensure PrintPage eventually sets HasMorePages to false.");

                    var pageSettings = (PageSettings)DefaultPageSettings.Clone();
                    pageSettings.PrinterSettings = PrinterSettings;

                    var queryArgs = new QueryPageSettingsEventArgs(pageSettings);
                    OnQueryPageSettings(queryArgs);

                    if (queryArgs.Cancel) {
                        canceled = true;
                        break;
                    }

                    pageSettings = queryArgs.PageSettings;
                    var pageBounds = pageSettings.Bounds;
                    var marginBounds = GetMarginBounds(pageBounds, pageSettings.Margins);
                    var bitmap = new SKBitmap(pageBounds.Width, pageBounds.Height);

                    try {
                        using var canvas = new SKCanvas(bitmap);
                        canvas.Clear(SKColors.White);

                        if (OriginAtMargins)
                            canvas.Translate(pageSettings.Margins.Left, pageSettings.Margins.Top);

                        var printPageArgs = new PrintPageEventArgs(canvas, marginBounds, pageBounds, pageSettings);
                        OnPrintPage(printPageArgs);

                        canceled = printPageArgs.Cancel;
                        hasMorePages = printPageArgs.HasMorePages && !printPageArgs.Cancel;
                        pages.Add(new PrintPreviewPage(bitmap, pageSettings));
                        bitmap = null;
                    } finally {
                        bitmap?.Dispose();
                    }

                    pageIndex++;
                }
            } catch {
                foreach (var page in pages)
                    page.Dispose();

                pages.Clear();
                throw;
            } finally {
                OnEndPrint(new PrintEventArgs(canceled));
            }

            return pages;
        }

        /// <summary>
        /// Raises the <see cref="BeginPrint"/> event.
        /// </summary>
        /// <param name="e">The print event data.</param>
        protected virtual void OnBeginPrint(PrintEventArgs e) => BeginPrint?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="EndPrint"/> event.
        /// </summary>
        /// <param name="e">The print event data.</param>
        protected virtual void OnEndPrint(PrintEventArgs e) => EndPrint?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="PrintPage"/> event.
        /// </summary>
        /// <param name="e">The print page event data.</param>
        protected virtual void OnPrintPage(PrintPageEventArgs e) => PrintPage?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="QueryPageSettings"/> event.
        /// </summary>
        /// <param name="e">The query page settings event data.</param>
        protected virtual void OnQueryPageSettings(QueryPageSettingsEventArgs e) => QueryPageSettings?.Invoke(this, e);

        private static Rectangle GetMarginBounds(Rectangle pageBounds, Margins margins)
        {
            var left = Math.Min(margins.Left, pageBounds.Width);
            var top = Math.Min(margins.Top, pageBounds.Height);
            var right = Math.Min(margins.Right, Math.Max(0, pageBounds.Width - left));
            var bottom = Math.Min(margins.Bottom, Math.Max(0, pageBounds.Height - top));

            return new Rectangle(
                pageBounds.Left + left,
                pageBounds.Top + top,
                Math.Max(0, pageBounds.Width - left - right),
                Math.Max(0, pageBounds.Height - top - bottom));
        }

        private sealed class RenderedPageCollection : IDisposable
        {
            private readonly IReadOnlyList<PrintPreviewPage> pages;

            public RenderedPageCollection(IReadOnlyList<PrintPreviewPage> pages)
            {
                this.pages = pages;
            }

            public void Dispose()
            {
                foreach (var page in pages)
                    page.Dispose();
            }
        }
    }
}
