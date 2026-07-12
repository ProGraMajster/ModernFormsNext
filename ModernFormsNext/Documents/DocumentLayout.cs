using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;
using Topten.RichTextKit;

namespace ModernFormsNext.Documents;

internal sealed class DocumentLayout
{
    public DocumentLayout(
        IReadOnlyList<DocumentLayoutElement> elements,
        IReadOnlyList<DocumentLayoutLink> links,
        DocumentTextMap textMap,
        int height)
    {
        Elements = elements ?? throw new ArgumentNullException(nameof(elements));
        Links = links ?? throw new ArgumentNullException(nameof(links));
        TextMap = textMap ?? throw new ArgumentNullException(nameof(textMap));
        Height = height;
    }

    public IReadOnlyList<DocumentLayoutElement> Elements { get; }

    public int Height { get; }

    public IReadOnlyList<DocumentLayoutLink> Links { get; }

    public DocumentTextMap TextMap { get; }
}

internal abstract class DocumentLayoutElement
{
    protected DocumentLayoutElement(Rectangle bounds)
    {
        Bounds = bounds;
    }

    public Rectangle Bounds { get; }
}

internal class DocumentTextLayoutElement : DocumentLayoutElement
{
    public DocumentTextLayoutElement(
        Rectangle bounds,
        TextBlock textBlock,
        Point textOrigin,
        string text,
        int documentTextStart)
        : base(bounds)
    {
        TextBlock = textBlock ?? throw new ArgumentNullException(nameof(textBlock));
        TextOrigin = textOrigin;
        Text = text ?? throw new ArgumentNullException(nameof(text));
        DocumentTextStart = documentTextStart;
    }

    public int DocumentTextLength => Text.Length;

    public int DocumentTextStart { get; }

    public string Text { get; }

    public TextBlock TextBlock { get; }

    public Point TextOrigin { get; }
}

internal sealed class DocumentCodeBlockLayoutElement : DocumentTextLayoutElement
{
    public DocumentCodeBlockLayoutElement(
        Rectangle bounds,
        TextBlock textBlock,
        Point textOrigin,
        string text,
        int documentTextStart,
        SKColor backgroundColor,
        DocumentCodeBlockHeaderLayout? header)
        : base(bounds, textBlock, textOrigin, text, documentTextStart)
    {
        BackgroundColor = backgroundColor;
        Header = header;
    }

    public SKColor BackgroundColor { get; }

    public DocumentCodeBlockHeaderLayout? Header { get; }
}

internal sealed class DocumentCodeBlockHeaderLayout
{
    public DocumentCodeBlockHeaderLayout(TextBlock textBlock, Point textOrigin, Rectangle separatorBounds, SKColor separatorColor)
    {
        TextBlock = textBlock ?? throw new ArgumentNullException(nameof(textBlock));
        TextOrigin = textOrigin;
        SeparatorBounds = separatorBounds;
        SeparatorColor = separatorColor;
    }

    public Rectangle SeparatorBounds { get; }

    public SKColor SeparatorColor { get; }

    public TextBlock TextBlock { get; }

    public Point TextOrigin { get; }
}

internal sealed class DocumentLoadedImageLayoutElement : DocumentLayoutElement
{
    public DocumentLoadedImageLayoutElement(Rectangle bounds, SKBitmap bitmap)
        : base(bounds)
    {
        Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    }

    public SKBitmap Bitmap { get; }
}

internal sealed class DocumentImagePlaceholderLayoutElement : DocumentLayoutElement
{
    public DocumentImagePlaceholderLayoutElement(
        Rectangle bounds,
        TextBlock textBlock,
        Point textOrigin,
        string fallbackText,
        SKColor borderColor,
        bool failed)
        : base(bounds)
    {
        TextBlock = textBlock ?? throw new ArgumentNullException(nameof(textBlock));
        TextOrigin = textOrigin;
        FallbackText = fallbackText ?? throw new ArgumentNullException(nameof(fallbackText));
        BorderColor = borderColor;
        Failed = failed;
    }

    public SKColor BorderColor { get; }

    public bool Failed { get; }

    public string FallbackText { get; }

    public TextBlock TextBlock { get; }

    public Point TextOrigin { get; }
}

internal sealed class DocumentTaskCheckBoxLayoutElement : DocumentLayoutElement
{
    public DocumentTaskCheckBoxLayoutElement(Rectangle bounds, CheckState checkState)
        : base(bounds)
    {
        CheckState = checkState;
    }

    public CheckState CheckState { get; }
}

internal sealed class DocumentTableCellLayoutElement : DocumentLayoutElement
{
    public DocumentTableCellLayoutElement(
        Rectangle bounds,
        SKColor backgroundColor,
        SKColor borderColor,
        int borderThickness)
        : base(bounds)
    {
        if (borderThickness < 0)
            throw new ArgumentOutOfRangeException(nameof(borderThickness));

        BackgroundColor = backgroundColor;
        BorderColor = borderColor;
        BorderThickness = borderThickness;
    }

    public SKColor BackgroundColor { get; }

    public SKColor BorderColor { get; }

    public int BorderThickness { get; }
}

internal sealed class DocumentQuoteBorderLayoutElement : DocumentLayoutElement
{
    public DocumentQuoteBorderLayoutElement(Rectangle bounds, SKColor color)
        : base(bounds)
    {
        Color = color;
    }

    public SKColor Color { get; }
}

internal sealed class DocumentHorizontalRuleLayoutElement : DocumentLayoutElement
{
    public DocumentHorizontalRuleLayoutElement(Rectangle bounds, SKColor color)
        : base(bounds)
    {
        Color = color;
    }

    public SKColor Color { get; }
}

internal sealed class DocumentLayoutLink
{
    public DocumentLayoutLink(DocumentTextLayoutElement element, LinkInline link, int start, int end, string text)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Link = link ?? throw new ArgumentNullException(nameof(link));
        Start = start;
        End = end;
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public DocumentTextLayoutElement Element { get; }

    public int End { get; }

    public LinkInline Link { get; }

    public int Start { get; }

    public string Text { get; }
}
