using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Renderers;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Displays a rendered preview of a <see cref="PrintDocument"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The control renders document pages into Skia bitmaps by running the
    /// <see cref="PrintDocument"/> event pipeline. The rendered pages are cached until
    /// <see cref="InvalidatePreview"/> is called or <see cref="Document"/> changes.
    /// </para>
    /// <para>
    /// This is a platform-neutral managed preview surface. It does not send output to a
    /// physical printer and does not depend on native WinForms preview controls.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var preview = new PrintPreviewControl
    /// {
    ///     Dock = DockStyle.Fill,
    ///     Document = document,
    ///     AutoZoom = true
    /// };
    /// </code>
    /// </example>
    [DefaultProperty(nameof(Document))]
    public class PrintPreviewControl : Control
    {
        private const double DefaultZoom = 0.3d;
        private const int PageSpacing = 14;

        private List<PrintPreviewPage>? pages;
        private PrintDocument? document;
        private string? previewError;
        private int columns = 1;
        private int rows = 1;
        private int startPage;
        private bool autoZoom = true;
        private double zoom = DefaultZoom;
        private bool useAntiAlias;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrintPreviewControl"/> class.
        /// </summary>
        public PrintPreviewControl()
        {
            SetControlBehavior(ControlBehaviors.Selectable, false);
            SetControlBehavior(ControlBehaviors.ReceivesMouseEvents, false);
        }

        /// <summary>
        /// Gets or sets a value indicating whether resizing the control automatically adjusts <see cref="Zoom"/>.
        /// </summary>
        [DefaultValue(true)]
        public bool AutoZoom {
            get => autoZoom;
            set {
                if (autoZoom == value)
                    return;

                autoZoom = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the number of pages displayed horizontally.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than or equal to zero.</exception>
        [DefaultValue(1)]
        public int Columns {
            get => columns;
            set {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

                if (columns == value)
                    return;

                columns = value;
                ClampStartPage();
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the document rendered by the preview control.
        /// </summary>
        public PrintDocument? Document {
            get => document;
            set {
                if (document == value)
                    return;

                document = value;
                InvalidatePreview();
            }
        }

        /// <summary>
        /// Gets the number of rendered preview pages.
        /// </summary>
        /// <remarks>
        /// Accessing this property renders the document if the preview cache is stale.
        /// </remarks>
        [Browsable(false)]
        public int PageCount {
            get {
                EnsurePages();
                return pages?.Count ?? 0;
            }
        }

        /// <summary>
        /// Gets or sets the number of pages displayed vertically.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than or equal to zero.</exception>
        [DefaultValue(1)]
        public int Rows {
            get => rows;
            set {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

                if (rows == value)
                    return;

                rows = value;
                ClampStartPage();
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the zero-based page number displayed in the upper-left preview slot.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than zero.</exception>
        [DefaultValue(0)]
        public int StartPage {
            get {
                EnsurePages();
                return GetClampedStartPage(startPage);
            }
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                SetStartPage(value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether antialiasing is used when drawing preview pages.
        /// </summary>
        [DefaultValue(false)]
        public bool UseAntiAlias {
            get => useAntiAlias;
            set {
                if (useAntiAlias == value)
                    return;

                useAntiAlias = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the preview zoom factor.
        /// </summary>
        /// <remarks>
        /// Setting this property disables <see cref="AutoZoom"/>. A value of <c>1.0</c>
        /// maps one page unit to one preview pixel.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than or equal to zero.</exception>
        [DefaultValue(DefaultZoom)]
        public double Zoom {
            get => zoom;
            set {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Zoom must be greater than zero.");

                autoZoom = false;
                zoom = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Occurs when <see cref="StartPage"/> changes.
        /// </summary>
        public event EventHandler? StartPageChanged;

        /// <summary>
        /// Invalidates the cached preview pages and requests a repaint.
        /// </summary>
        /// <remarks>
        /// Call this method after changing document data that affects pagination or drawing.
        /// The next paint or <see cref="PageCount"/> access re-renders the document.
        /// </remarks>
        public void InvalidatePreview()
        {
            DisposePages();
            previewError = null;
            startPage = 0;
            Invalidate();
            OnStartPageChanged(EventArgs.Empty);
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size(400, 300);

        /// <summary>
        /// The default control style for all <see cref="PrintPreviewControl"/> instances.
        /// </summary>
        public new static ControlStyle DefaultStyle = new ControlStyle(Control.DefaultStyle,
            style => {
                style.BackgroundColor = Theme.ControlLowColor;
                style.Border.Width = 1;
                style.Border.Color = Theme.BorderLowColor;
            });

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle(DefaultStyle);

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposePages();

            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            EnsurePages();

            if (previewError is not null) {
                DrawCenteredText(e.Canvas, previewError);
                return;
            }

            if (pages is null || pages.Count == 0) {
                DrawCenteredText(e.Canvas, Document is null ? "No document selected." : "The document did not render any pages.");
                return;
            }

            var visiblePages = Math.Min(rows * columns, pages.Count - StartPage);

            if (visiblePages <= 0) {
                DrawCenteredText(e.Canvas, "No preview pages.");
                return;
            }

            var client = PaddedClientRectangle;
            var spacing = e.LogicalToDeviceUnits(PageSpacing);
            var effectiveZoom = GetEffectiveZoom(client, spacing, visiblePages);

            if (autoZoom)
                zoom = effectiveZoom;

            using var bitmapPaint = new SKPaint { IsAntialias = useAntiAlias };
            using var shadowPaint = new SKPaint { Color = new SKColor(0, 0, 0, 40), IsAntialias = true };
            using var pageBorderPaint = new SKPaint { Color = Theme.BorderHighColor, IsStroke = true, StrokeWidth = 1 };

            for (var slot = 0; slot < visiblePages; slot++) {
                var page = pages[StartPage + slot];
                var row = slot / columns;
                var column = slot % columns;
                var destination = GetPageDestination(client, spacing, effectiveZoom, page.Bitmap, row, column);
                var destinationRect = new SKRect(destination.Left, destination.Top, destination.Right, destination.Bottom);

                e.Canvas.DrawRect(destination.Left + 4, destination.Top + 4, destination.Width, destination.Height, shadowPaint);
                e.Canvas.DrawBitmap(page.Bitmap, destinationRect, bitmapPaint);
                e.Canvas.DrawRect(destinationRect, pageBorderPaint);
            }
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            if (autoZoom)
                Invalidate();
        }

        /// <summary>
        /// Raises the <see cref="StartPageChanged"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnStartPageChanged(EventArgs e) => StartPageChanged?.Invoke(this, e);

        private void EnsurePages()
        {
            if (pages is not null)
                return;

            if (document is null) {
                pages = new List<PrintPreviewPage>();
                return;
            }

            try {
                pages = document.RenderPreviewPages();
                ClampStartPage();
            } catch (Exception ex) {
                previewError = ex.Message;
                pages = new List<PrintPreviewPage>();
            }
        }

        private void DisposePages()
        {
            if (pages is null)
                return;

            foreach (var page in pages)
                page.Dispose();

            pages = null;
        }

        private double GetEffectiveZoom(Rectangle client, int spacing, int visiblePages)
        {
            if (!autoZoom)
                return zoom;

            var maxColumns = Math.Min(columns, visiblePages);
            var maxRows = (int)Math.Ceiling(visiblePages / (double)columns);
            var pageWidth = 1;
            var pageHeight = 1;

            for (var i = 0; i < visiblePages; i++) {
                var bitmap = pages![StartPage + i].Bitmap;
                pageWidth = Math.Max(pageWidth, bitmap.Width);
                pageHeight = Math.Max(pageHeight, bitmap.Height);
            }

            var availableWidth = Math.Max(1, client.Width - (spacing * (maxColumns + 1)));
            var availableHeight = Math.Max(1, client.Height - (spacing * (maxRows + 1)));
            var zoomX = availableWidth / (double)(pageWidth * maxColumns);
            var zoomY = availableHeight / (double)(pageHeight * maxRows);

            return Math.Max(0.01d, Math.Min(zoomX, zoomY));
        }

        private RectangleF GetPageDestination(Rectangle client, int spacing, double effectiveZoom, SKBitmap bitmap, int row, int column)
        {
            var pageWidth = Math.Max(1, (float)(bitmap.Width * effectiveZoom));
            var pageHeight = Math.Max(1, (float)(bitmap.Height * effectiveZoom));
            var totalWidth = (float)((columns * pageWidth) + ((columns + 1) * spacing));
            var visibleRows = Math.Min(rows, (int)Math.Ceiling((pages!.Count - StartPage) / (double)columns));
            var totalHeight = (float)((visibleRows * pageHeight) + ((visibleRows + 1) * spacing));
            var originX = client.Left + Math.Max(0, (client.Width - totalWidth) / 2f) + spacing;
            var originY = client.Top + Math.Max(0, (client.Height - totalHeight) / 2f) + spacing;

            return new RectangleF(
                originX + (column * (pageWidth + spacing)),
                originY + (row * (pageHeight + spacing)),
                pageWidth,
                pageHeight);
        }

        private int GetClampedStartPage(int value)
        {
            var pageCount = pages?.Count ?? 0;

            if (pageCount <= 0)
                return 0;

            return Math.Clamp(value, 0, Math.Max(0, pageCount - 1));
        }

        private void ClampStartPage()
        {
            var clamped = GetClampedStartPage(startPage);

            if (clamped != startPage) {
                startPage = clamped;
                OnStartPageChanged(EventArgs.Empty);
            }
        }

        private void SetStartPage(int value)
        {
            var clamped = GetClampedStartPage(value);

            if (startPage == clamped)
                return;

            startPage = clamped;
            Invalidate();
            OnStartPageChanged(EventArgs.Empty);
        }

        private void DrawCenteredText(SKCanvas canvas, string text)
        {
            using var paint = new SKPaint
            {
                Color = Theme.ForegroundDisabledColor,
                IsAntialias = true
            };
            using var font = new SKFont(Theme.UIFont, LogicalToDeviceUnits(Theme.FontSize));

            var bounds = ClientRectangle;
            var textWidth = font.MeasureText(text);
            var x = bounds.Left + ((bounds.Width - textWidth) / 2f);
            var y = bounds.Top + (bounds.Height / 2f);

            canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
        }
    }
}
