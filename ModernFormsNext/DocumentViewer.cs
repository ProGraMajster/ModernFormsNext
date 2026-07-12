using System;
using System.Drawing;
using ModernFormsNext.Documents;
using ModernFormsNext.Renderers;
using MfnDocument = ModernFormsNext.Documents.Document;

namespace ModernFormsNext;

/// <summary>
/// Displays a read-only <see cref="Documents.Document"/> using the native ModernFormsNext
/// SkiaSharp rendering pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DocumentViewer"/> is the shared document rendering control used by
/// <see cref="MarkdownViewer"/>. It performs cached document layout, wraps text to the current
/// width, scrolls vertically, and exposes link activation without automatically opening URLs.
/// </para>
/// <para>
/// The viewer is read-only but supports text selection across document blocks, mouse or pointer
/// drag selection, word selection on double click, and plain-text copy through the platform
/// clipboard abstraction.
/// </para>
/// <para>
/// Layout is invalidated when the document, document style, size, padding, font, theme, DPI scale,
/// or link hover/pressed state changes. Rendering remains platform-neutral and does not use native
/// WinForms controls, WebView, HTML, or platform-specific link launching.
/// </para>
/// <para>
/// Block image nodes are loaded asynchronously through a per-viewer cache. While an image is
/// loading or if loading fails, the viewer renders fallback text instead of blocking layout or
/// painting. Mixed inline image nodes render fallback text because the current RichTextKit API
/// does not expose inline object measurement and drawing callbacks.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var document = new Document(new DocumentBlock[]
/// {
///     new ParagraphBlock(new DocumentInline[]
///     {
///         new TextInline("Visit "),
///         new LinkInline("https://example.com", new DocumentInline[] { new TextInline("docs") })
///     })
/// });
///
/// var viewer = new DocumentViewer
/// {
///     Dock = DockStyle.Fill,
///     Document = document
/// };
///
/// viewer.LinkClicked += (_, e) => Console.WriteLine(e.Destination);
/// </code>
/// </example>
public class DocumentViewer : ScrollControl
{
    private MfnDocument document = MfnDocument.Empty;
    private DocumentStyle documentStyle = new();
    private readonly DocumentImageLoadOptions imageLoadOptions = new();
    private readonly DocumentImageCache imageCache;
    private readonly DocumentSelection selection = new();
    private readonly DocumentLinkInteractionState linkInteraction = new();
    private DocumentLayout? cachedLayout;
    private Document? cachedDocument;
    private LinkInline? hoveredLink;
    private int scrollY;
    private int cachedWidth = -1;
    private int cachedStyleVersion = -1;
    private int cachedFontSize = -1;
    private double cachedScaling = -1;
    private string cachedFontFamily = string.Empty;
    private FontStyle cachedFontStyle;
    private bool disposed;
    private int selectionAnchor;
    private Cursor? cursorBeforeLink;
    private bool usingLinkCursor;

    /// <summary>
    /// Initializes a new <see cref="DocumentViewer"/> instance.
    /// </summary>
    public DocumentViewer()
    {
        imageCache = new DocumentImageCache(OnDocumentImageResourceChanged, GetImageLoadLimits);
        ScrollBars = ScrollBars.Vertical;
        VerticalScrollBar.Enabled = false;
        VerticalScrollBar.ValueChanged += (_, _) => SetScrollOffset(VerticalScrollBar.Value);
        documentStyle.Changed += DocumentStyle_Changed;
        imageLoadOptions.Changed += ImageLoadOptions_Changed;
        Cursor = Cursors.IBeam;
        SetControlBehavior(ControlBehaviors.Selectable, true);
        imageCache.SetDocument(document);
    }

    /// <inheritdoc/>
    protected override Padding DefaultPadding => new Padding(10);

    /// <inheritdoc/>
    protected override Size DefaultSize => new Size(320, 220);

    /// <summary>
    /// Gets or sets the document displayed by the viewer.
    /// </summary>
    /// <remarks>
    /// Assigning this property invalidates cached layout and repainting. The viewer treats
    /// <see langword="null"/> as <see cref="Documents.Document.Empty"/> so empty document states
    /// are safe and do not throw.
    /// </remarks>
    public virtual MfnDocument Document
    {
        get => document;
        set
        {
            value ??= MfnDocument.Empty;

            if (ReferenceEquals(document, value))
                return;

            document = value;
            ClearLinkInteraction(clearHover: true);
            selection.Clear();
            imageCache.SetDocument(document);
            InvalidateDocumentLayout(refreshImageSources: false, preserveLinkInteraction: true);
        }
    }

    /// <summary>
    /// Gets or sets the style used for document layout and rendering.
    /// </summary>
    /// <remarks>
    /// Changing the style instance or mutating its properties invalidates cached layout because
    /// font metrics, spacing, and colors may change. Values are resolved against the viewer's
    /// current <see cref="ControlStyle"/> and <see cref="Theme"/>.
    /// </remarks>
    public DocumentStyle DocumentStyle
    {
        get => documentStyle;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(documentStyle, value))
                return;

            documentStyle.Changed -= DocumentStyle_Changed;
            documentStyle = value;
            documentStyle.Changed += DocumentStyle_Changed;
            InvalidateDocumentLayout(refreshImageSources: false);
        }
    }

    /// <summary>
    /// Occurs when a document link is clicked or tapped.
    /// </summary>
    /// <remarks>
    /// The event is raised once after a primary pointer is pressed and released over the same
    /// semantic link without crossing the DPI-scaled text-selection drag threshold. ModernFormsNext
    /// does not open link destinations automatically. Handle this event to route links through an
    /// application-specific navigation or URI-opening service.
    /// </remarks>
    public event EventHandler<DocumentLinkClickedEventArgs>? LinkClicked;

    /// <summary>
    /// Gets the image loading options used by this viewer.
    /// </summary>
    /// <remarks>
    /// The returned instance is owned by the viewer and remains stable for its lifetime. Changing
    /// one of its properties keeps successfully loaded images and restarts pending or failed
    /// resources with the new limits. Mutate options associated with a live viewer on the UI
    /// thread because changes invalidate control state.
    /// </remarks>
    public DocumentImageLoadOptions ImageLoadOptions => imageLoadOptions;

    /// <summary>
    /// Gets or sets the maximum number of encoded bytes allowed for a single document image.
    /// </summary>
    /// <remarks>
    /// The limit applies to HTTP, HTTPS, file, relative file, and data URI image sources. Images
    /// exceeding the limit fail gracefully and render their fallback text. Changing the value
    /// restarts pending image loads for the current document.
    /// </remarks>
    public int MaxImageDownloadBytes
    {
        get => ImageLoadOptions.MaxDownloadBytes;
        set => ImageLoadOptions.MaxDownloadBytes = value;
    }

    /// <summary>
    /// Gets or sets the maximum decoded pixel count allowed for a single document image.
    /// </summary>
    /// <remarks>
    /// This protects the renderer from very large decoded images. Images exceeding the limit fail
    /// gracefully and render their fallback text. Changing the value restarts pending image loads
    /// for the current document.
    /// </remarks>
    public long MaxImagePixelCount
    {
        get => ImageLoadOptions.MaxDecodedPixels;
        set => ImageLoadOptions.MaxDecodedPixels = value;
    }

    /// <summary>
    /// Gets or sets the timeout used while loading a single HTTP or HTTPS document image.
    /// </summary>
    /// <remarks>
    /// A value equal to <see cref="TimeSpan.Zero"/> disables the per-image timeout.
    /// Timed-out images fail gracefully and render their fallback text.
    /// </remarks>
    public TimeSpan ImageRequestTimeout
    {
        get => ImageLoadOptions.RequestTimeout;
        set => ImageLoadOptions.RequestTimeout = value;
    }

    /// <summary>
    /// Gets or sets the zero-based UTF-16 offset at which the current selection starts.
    /// </summary>
    /// <remarks>Setting this property preserves the current selection length where possible.</remarks>
    public int SelectionStart
    {
        get => selection.Start;
        set => Select(value, SelectionLength);
    }

    /// <summary>
    /// Gets or sets the number of UTF-16 code units in the current selection.
    /// </summary>
    public int SelectionLength
    {
        get => selection.Length;
        set => Select(SelectionStart, value);
    }

    /// <summary>
    /// Gets the plain text represented by the current selection.
    /// </summary>
    public string SelectedText
    {
        get
        {
            var layout = GetDocumentLayout();
            return layout.TextMap.GetText(SelectionStart, SelectionLength);
        }
    }

    internal LinkInline? HoveredLink => hoveredLink;

    internal LinkInline? PressedLink => linkInteraction.PressedLink;

    internal int ScrollOffset => scrollY;

    internal DocumentImageCache ImageCache => imageCache;

    internal DocumentLayout GetDocumentLayout()
    {
        EnsureDocumentLayout();
        return cachedLayout ?? new DocumentLayout(
            Array.Empty<DocumentLayoutElement>(),
            Array.Empty<DocumentLayoutLink>(),
            DocumentTextMap.Empty,
            0);
    }

    internal TextSelection GetTextSelection(DocumentTextLayoutElement element)
        => (cachedLayout?.TextMap ?? DocumentTextMap.Empty).GetSelection(
            element,
            SelectionStart,
            SelectionLength,
            documentStyle.ResolveSelectionBackgroundColor(this));

    internal DocumentImageResource? GetImageResource(string source)
        => imageCache.GetResource(source);

    /// <summary>
    /// Invalidates the cached document layout and schedules a repaint.
    /// </summary>
    /// <remarks>
    /// Call this method after mutating an already assigned <see cref="Document"/> instance. The
    /// preferred pattern is still to assign a new immutable document object when content changes.
    /// </remarks>
    public void InvalidateDocumentLayout()
        => InvalidateDocumentLayout(refreshImageSources: true);

    /// <summary>
    /// Cancels current image loads, clears the per-viewer image cache, and reloads image sources in
    /// the assigned document.
    /// </summary>
    /// <remarks>
    /// Use this method after replacing a local file whose source string did not change. The method
    /// does not reparse Markdown, mutate the document, or affect selection. Call it on the UI thread.
    /// </remarks>
    public void ReloadDocumentImages()
    {
        if (disposed)
            return;

        imageCache.SetDocument(document);
        InvalidateDocumentLayout(refreshImageSources: false);
    }

    /// <summary>
    /// Selects a range in the viewer's logical plain-text representation.
    /// </summary>
    /// <param name="start">The zero-based UTF-16 offset at which selection starts.</param>
    /// <param name="length">The number of UTF-16 code units to select.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> or <paramref name="length"/> is negative.</exception>
    public void Select(int start, int length)
    {
        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        var textLength = GetDocumentLayout().TextMap.Length;
        if (selection.Select(start, length, textLength))
            Invalidate();
    }

    /// <summary>
    /// Selects all selectable text in the current document.
    /// </summary>
    public void SelectAll()
    {
        var textLength = GetDocumentLayout().TextMap.Length;
        if (selection.Select(0, textLength, textLength))
            Invalidate();
    }

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        if (selection.Clear())
            Invalidate();
    }

    /// <summary>
    /// Copies <see cref="SelectedText"/> to the platform clipboard.
    /// </summary>
    /// <remarks>
    /// This method has no effect when no text is selected. Call it on the UI thread because the
    /// platform clipboard service and control selection both have UI-thread affinity.
    /// </remarks>
    public void Copy()
    {
        if (SelectionLength == 0)
            return;

        var text = SelectedText;
        if (text.Length > 0)
            AsyncHelper.RunSync(() => Clipboard.SetTextAsync(text));
    }

    private void InvalidateDocumentLayout(bool refreshImageSources, bool preserveLinkInteraction = false)
    {
        if (!preserveLinkInteraction)
            ClearLinkInteraction(clearHover: true);

        if (refreshImageSources)
            imageCache.SetDocument(document);

        cachedLayout = null;
        cachedDocument = null;
        Invalidate();
    }

    /// <summary>
    /// Raises the <see cref="LinkClicked"/> event.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnLinkClicked(DocumentLinkClickedEventArgs e)
        => LinkClicked?.Invoke(this, e);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposed = true;
            ClearLinkInteraction(clearHover: true);
            documentStyle.Changed -= DocumentStyle_Changed;
            imageLoadOptions.Changed -= ImageLoadOptions_Changed;
            imageCache.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        InvalidateDocumentLayout(refreshImageSources: false);
    }

    /// <inheritdoc/>
    protected override void OnDoubleClick(MouseEventArgs e)
    {
        base.OnDoubleClick(e);

        if (Enabled && e.Button.HasFlag(MouseButtons.Left))
        {
            var pressedStateChanged = linkInteraction.Cancel();
            SelectWordAt(e.Location);
            if (pressedStateChanged)
                InvalidateDocumentLayout(refreshImageSources: false, preserveLinkInteraction: true);
        }
    }

    /// <inheritdoc/>
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        InvalidateDocumentLayout(refreshImageSources: false);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.IsShortcutControlPressed && e.KeyCode == Keys.A)
        {
            SelectAll();
            e.Handled = true;
        }
        else if (e.IsShortcutControlPressed && e.KeyCode == Keys.C && SelectionLength > 0)
        {
            Copy();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape && SelectionLength > 0)
        {
            ClearSelection();
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!Enabled || !e.Button.HasFlag(MouseButtons.Left))
            return;

        if (e.Clicks > 1)
        {
            linkInteraction.Cancel();
            SelectWordAt(e.Location);
            return;
        }

        var link = HitTestLink(e.Location);
        var hoverStateChanged = !ReferenceEquals(hoveredLink, link?.Link);
        hoveredLink = link?.Link;
        var pressedStateChanged = linkInteraction.Begin(link?.Link, e.Location);
        selectionAnchor = HitTestTextPosition(e.Location);

        if (selection.SelectFromAnchor(selectionAnchor, selectionAnchor, GetDocumentLayout().TextMap.Length))
            Invalidate();

        UpdateLinkCursor(link?.Link);
        if (pressedStateChanged || hoverStateChanged)
            InvalidateDocumentLayout(refreshImageSources: false, preserveLinkInteraction: true);
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (hoveredLink is null && linkInteraction.PressedLink is null)
            return;

        var hoverStateChanged = hoveredLink is not null;
        hoveredLink = null;
        var pressedStateChanged = linkInteraction.IsPointerDown
            ? linkInteraction.LeaveLink()
            : linkInteraction.Cancel();
        UpdateLinkCursor(null);
        if (pressedStateChanged || hoverStateChanged)
            InvalidateDocumentLayout(refreshImageSources: false, preserveLinkInteraction: true);
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!Enabled)
            return;

        var link = HitTestLink(e.Location)?.Link;
        if (linkInteraction.IsPointerDown)
        {
            var moveResult = linkInteraction.Move(link, e.Location, GetDragThreshold());
            if (linkInteraction.DragStarted)
            {
                AutoScrollSelection(e.Location);
                var active = HitTestTextPosition(e.Location);
                if (selection.SelectFromAnchor(selectionAnchor, active, GetDocumentLayout().TextMap.Length))
                    Invalidate();
            }

            UpdateLinkCursor(linkInteraction.DragStarted ? null : link);
            if (moveResult.VisualStateChanged)
                InvalidateDocumentLayout(refreshImageSources: false, preserveLinkInteraction: true);

            return;
        }

        if (ReferenceEquals(hoveredLink, link))
        {
            UpdateLinkCursor(link);
            return;
        }

        hoveredLink = link;
        UpdateLinkCursor(link);
        InvalidateDocumentLayout(refreshImageSources: false, preserveLinkInteraction: true);
    }

    /// <inheritdoc/>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!e.Button.HasFlag(MouseButtons.Left))
            return;

        if (!Enabled)
        {
            ClearLinkInteraction(clearHover: true);
            return;
        }

        var released = HitTestLink(e.Location);
        var pointerWasDown = linkInteraction.IsPointerDown;
        var selectionDragged = linkInteraction.DragStarted;

        if (pointerWasDown && selectionDragged)
        {
            var active = HitTestTextPosition(e.Location);
            if (selection.SelectFromAnchor(selectionAnchor, active, GetDocumentLayout().TextMap.Length))
                Invalidate();
        }

        var activatedLink = linkInteraction.Complete(released?.Link);
        hoveredLink = released?.Link;
        UpdateLinkCursor(hoveredLink);

        if (activatedLink is not null && released is not null)
        {
            OnLinkClicked(new DocumentLinkClickedEventArgs(
                activatedLink.Destination,
                released.Text,
                activatedLink.Title,
                e.Button));
        }

        if (pointerWasDown && !disposed)
            InvalidateDocumentLayout(refreshImageSources: false, preserveLinkInteraction: true);
    }

    /// <inheritdoc/>
    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        InvalidateDocumentLayout(refreshImageSources: false);
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        RenderManager.Render(this, e);
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        InvalidateDocumentLayout(refreshImageSources: false);
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateDocumentLayout(refreshImageSources: false);
    }

    /// <inheritdoc/>
    protected internal override void OnThemeChanged(EventArgs e)
    {
        base.OnThemeChanged(e);
        InvalidateDocumentLayout(refreshImageSources: false);
    }

    private void DocumentStyle_Changed(object? sender, EventArgs e)
        => InvalidateDocumentLayout(refreshImageSources: false);

    private DocumentImageLoadLimits GetImageLoadLimits()
        => imageLoadOptions.ToLimits();

    private void ImageLoadOptions_Changed(object? sender, EventArgs e)
    {
        imageCache.ReloadPendingAndFailed(document);
        InvalidateDocumentLayout(refreshImageSources: false);
    }

    private void OnDocumentImageResourceChanged()
    {
        if (disposed)
            return;

        InvalidateDocumentLayout(refreshImageSources: false);
    }

    private void EnsureDocumentLayout()
    {
        var contentBounds = PaddedClientRectangle;
        var font = CurrentStyle.GetFont();
        var fontSize = CurrentStyle.GetFontSize();
        var fontStyle = CurrentStyle.GetFontStyle();

        if (cachedLayout is not null
            && ReferenceEquals(cachedDocument, document)
            && cachedWidth == contentBounds.Width
            && cachedStyleVersion == documentStyle.Version
            && cachedFontSize == fontSize
            && cachedFontStyle == fontStyle
            && cachedFontFamily == font.FamilyName
            && Math.Abs(cachedScaling - Scaling) < double.Epsilon)
        {
            UpdateScrollBars(cachedLayout);
            return;
        }

        cachedLayout = DocumentLayoutEngine.Layout(this, document, documentStyle, contentBounds, hoveredLink, linkInteraction.PressedLink);
        cachedDocument = document;
        cachedWidth = contentBounds.Width;
        cachedStyleVersion = documentStyle.Version;
        cachedFontSize = fontSize;
        cachedFontStyle = fontStyle;
        cachedFontFamily = font.FamilyName;
        cachedScaling = Scaling;

        UpdateScrollBars(cachedLayout);
    }

    private DocumentLayoutLink? HitTestLink(Point location)
    {
        if (!PaddedClientRectangle.Contains(location))
            return null;

        var layout = GetDocumentLayout();
        var documentPoint = new Point(location.X, location.Y + scrollY);

        foreach (var link in layout.Links)
        {
            if (!link.Element.Bounds.Contains(documentPoint))
                continue;

            var x = documentPoint.X - link.Element.TextOrigin.X;
            var y = documentPoint.Y - link.Element.TextOrigin.Y;
            var hit = link.Element.TextBlock.HitTest(x, y);

            if (hit.IsNone)
                continue;

            // Link activation needs the glyph actually under the pointer. ClosestCodePointIndex is
            // a caret position and can advance to the next run in the right half of a glyph.
            var index = hit.OverCodePointIndex;
            if (index >= link.Start && index < link.End)
                return link;
        }

        return null;
    }

    private int HitTestTextPosition(Point location)
    {
        var documentPoint = new Point(location.X, location.Y + scrollY);
        return GetDocumentLayout().TextMap.HitTest(documentPoint);
    }

    private void SelectWordAt(Point location)
    {
        var map = GetDocumentLayout().TextMap;
        if (map.Length == 0)
        {
            ClearSelection();
            return;
        }

        var index = Math.Min(HitTestTextPosition(location), map.Length - 1);
        if (!IsWordCharacter(map.Text[index]))
        {
            Select(index, 1);
            return;
        }

        var start = index;
        var end = index + 1;

        while (start > 0 && IsWordCharacter(map.Text[start - 1]))
            start--;
        while (end < map.Length && IsWordCharacter(map.Text[end]))
            end++;

        Select(start, end - start);
    }

    private static bool IsWordCharacter(char value)
        => char.IsLetterOrDigit(value) || value == '_';

    private void ClearLinkInteraction(bool clearHover)
    {
        linkInteraction.Cancel();
        if (clearHover)
            hoveredLink = null;
        UpdateLinkCursor(null);
    }

    private int GetDragThreshold()
        => Math.Max(1, LogicalToDeviceUnits(DocumentLinkInteractionState.DragThresholdLogicalPixels));

    private void UpdateLinkCursor(LinkInline? link)
    {
        if (Enabled && link is not null)
        {
            if (!usingLinkCursor)
            {
                cursorBeforeLink = Cursor;
                usingLinkCursor = true;
            }

            if (Cursor != Cursors.Hand)
                Cursor = Cursors.Hand;
            return;
        }

        if (!usingLinkCursor)
            return;

        var cursor = cursorBeforeLink ?? Cursors.IBeam;
        cursorBeforeLink = null;
        usingLinkCursor = false;
        if (Cursor != cursor)
            Cursor = cursor;
    }

    private void AutoScrollSelection(Point location)
    {
        if (!VerticalScrollBar.Enabled)
            return;

        var viewport = PaddedClientRectangle;
        var value = VerticalScrollBar.Value;

        if (location.Y < viewport.Top)
            value -= VerticalScrollBar.SmallChange;
        else if (location.Y > viewport.Bottom)
            value += VerticalScrollBar.SmallChange;
        else
            return;

        VerticalScrollBar.Value = Math.Clamp(value, VerticalScrollBar.Minimum, VerticalScrollBar.Maximum);
    }

    private void SetScrollOffset(int value)
    {
        if (scrollY == value)
            return;

        scrollY = value;
        Invalidate();
    }

    private void UpdateScrollBars(DocumentLayout layout)
    {
        var viewportHeight = Math.Max(0, PaddedClientRectangle.Height);
        var maximum = Math.Max(0, layout.Height - viewportHeight);

        VerticalScrollBar.Maximum = maximum;
        VerticalScrollBar.LargeChange = Math.Max(1, viewportHeight);
        VerticalScrollBar.SmallChange = Math.Max(1, LogicalToDeviceUnits(CurrentStyle.GetFontSize() * 3));
        VerticalScrollBar.Enabled = maximum > 0;

        var value = Math.Min(scrollY, maximum);
        if (VerticalScrollBar.Value != value)
            VerticalScrollBar.Value = value;

        scrollY = value;
    }
}
