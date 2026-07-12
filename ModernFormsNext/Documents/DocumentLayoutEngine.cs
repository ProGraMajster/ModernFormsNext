using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using SkiaSharp;
using Topten.RichTextKit;

namespace ModernFormsNext.Documents;

internal static class DocumentLayoutEngine
{
    private static readonly string[] UnorderedListMarkers = { "\u2022", "\u25e6", "\u25aa" };

    public static DocumentLayout Layout(
        DocumentViewer viewer,
        Document document,
        DocumentStyle style,
        Rectangle contentBounds,
        LinkInline? hoveredLink,
        LinkInline? pressedLink)
    {
        var context = new LayoutContext(viewer, style, contentBounds, hoveredLink, pressedLink);

        foreach (var block in document.Blocks)
            LayoutBlock(context, block, BlockLayoutOptions.Default);

        var height = Math.Max(0, context.Y - contentBounds.Top);
        return new DocumentLayout(context.Elements, context.Links, context.TextMap.Build(), height);
    }

    private static void LayoutBlock(LayoutContext context, DocumentBlock block, BlockLayoutOptions options)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                LayoutTextBlock(context, paragraph.Inlines, TextBlockRole.Paragraph, options);
                break;
            case HeadingBlock heading:
                LayoutTextBlock(context, heading.Inlines, TextBlockRole.Heading, options with { HeadingLevel = heading.Level });
                break;
            case CodeBlock code:
                LayoutCodeBlock(context, code, options);
                break;
            case ImageBlock image:
                LayoutImageBlock(context, image, options);
                break;
            case TableBlock table:
                LayoutTableBlock(context, table, options);
                break;
            case FootnoteGroupBlock footnotes:
                LayoutFootnoteGroupBlock(context, footnotes, options);
                break;
            case QuoteBlock quote:
                LayoutQuoteBlock(context, quote, options);
                break;
            case ListBlock list:
                LayoutListBlock(context, list, options);
                break;
            case HorizontalRuleBlock:
                LayoutHorizontalRule(context);
                break;
        }
    }

    private static void LayoutCodeBlock(LayoutContext context, CodeBlock code, BlockLayoutOptions options)
    {
        AddSpacingBeforeBlock(context, context.Scale(context.Style.ParagraphSpacing), options);
        AddTextSeparationBeforeBlock(context, options);

        var padding = context.Scale(context.Style.CodePadding);
        var textWidth = context.Style.CodeBlockWrap
            ? Math.Max(1, context.AvailableWidth - (padding * 2))
            : (int?)null;
        var block = CreateTextBlock(textWidth);
        var runState = InlineRunState.CodeBlock;
        var displayText = code.Text.TrimEnd('\r', '\n');

        AppendHighlightedCode(block, displayText, code.Language, context, runState);

        var textHeight = GetTextBlockHeight(block, context.BaseFontSize);
        var textOrigin = new Point(context.X + padding, context.Y + padding);
        DocumentCodeBlockHeaderLayout? header = null;

        if (context.Style.ShowCodeBlockLanguage && !string.IsNullOrWhiteSpace(code.Language))
        {
            var headerBlock = CreateTextBlock(Math.Max(1, context.AvailableWidth - (padding * 2)));
            headerBlock.AddText(
                DocumentSyntaxHighlighterRegistry.GetDisplayName(code.Language),
                CreateCodeLanguageStyle(context));

            var headerHeight = GetTextBlockHeight(headerBlock, Math.Max(1, context.BaseFontSize - context.Scale(2)));
            var gap = Math.Max(2, padding / 2);
            var separatorThickness = Math.Max(1, context.Scale(1));
            var separatorBounds = new Rectangle(
                context.X + padding,
                textOrigin.Y + headerHeight + (gap / 2),
                Math.Max(1, context.AvailableWidth - (padding * 2)),
                separatorThickness);

            header = new DocumentCodeBlockHeaderLayout(
                headerBlock,
                textOrigin,
                separatorBounds,
                Theme.BorderLowColor);
            textOrigin = new Point(textOrigin.X, separatorBounds.Bottom + gap);
        }

        var bounds = new Rectangle(
            context.X,
            context.Y,
            context.AvailableWidth,
            Math.Max(1, textOrigin.Y + textHeight + padding - context.Y));
        var documentTextStart = context.TextMap.Append(displayText);
        var element = new DocumentCodeBlockLayoutElement(
            bounds,
            block,
            textOrigin,
            displayText,
            documentTextStart,
            context.Style.ResolveCodeBackgroundColor(context.Viewer),
            header);

        context.Elements.Add(element);
        context.TextMap.AddElement(element);
        context.Y = bounds.Bottom + context.Scale(context.Style.ParagraphSpacing);
    }

    private static void LayoutHorizontalRule(LayoutContext context)
    {
        var spacing = context.Scale(context.Style.HorizontalRuleSpacing);
        var thickness = Math.Max(1, context.Scale(context.Style.HorizontalRuleThickness));

        AddSpacingBeforeBlock(context, spacing, BlockLayoutOptions.Default);
        context.TextMap.EnsureBlockSeparation();

        var y = context.Y + spacing / 2;
        var bounds = new Rectangle(context.X, y, context.AvailableWidth, thickness);
        context.Elements.Add(new DocumentHorizontalRuleLayoutElement(
            bounds,
            context.Style.ResolveHorizontalRuleColor(context.Viewer)));

        context.Y = bounds.Bottom + spacing;
    }

    private static void LayoutImageBlock(LayoutContext context, ImageBlock image, BlockLayoutOptions options)
    {
        LayoutImage(
            context,
            image.Source,
            image.AltText,
            options);
    }

    private static void LayoutListBlock(LayoutContext context, ListBlock list, BlockLayoutOptions options)
    {
        if (options.ListDepth > 0)
            context.TextMap.EnsureLineBreak();
        else
            AddTextSeparationBeforeBlock(context, options);

        var index = list.StartNumber;
        var itemSpacing = context.Scale(context.Style.ListItemSpacing);
        var markerColumnWidth = MeasureListMarkerColumn(context, list, options.ListDepth);
        var firstItem = true;

        foreach (var item in list.Items)
        {
            if (!firstItem)
                context.TextMap.EnsureLineBreak();

            if (options.ListDepth > 0)
                context.TextMap.Append(new string(' ', options.ListDepth * 2));

            var marker = GetListMarker(list, item, index, options.ListDepth);
            LayoutListItem(context, item, marker, markerColumnWidth, options);
            context.Y += itemSpacing;
            firstItem = false;

            if (list.Ordered)
                index++;
        }
    }

    private static void LayoutListItem(
        LayoutContext context,
        DocumentListItem item,
        string marker,
        int markerColumnWidth,
        BlockLayoutOptions options)
    {
        var originalX = context.X;
        var originalWidth = context.AvailableWidth;
        var indent = Math.Max(1, context.Scale(context.Style.ListIndent));
        var markerSpacing = context.Scale(6);
        var compactOptions = options with { Compact = true };
        var markerX = originalX + indent;
        var contentX = markerX + markerColumnWidth + markerSpacing;
        var contentWidth = Math.Max(1, originalWidth - indent - markerColumnWidth - markerSpacing);
        var itemStartY = context.Y;

        AddListMarkerElement(context, item, marker, markerX, markerColumnWidth, itemStartY, options);

        if (item.Blocks.Count == 0)
        {
            context.Y = Math.Max(context.Y, itemStartY + context.BaseFontSize + 2);
            return;
        }

        var first = true;
        context.X = contentX;
        context.AvailableWidth = contentWidth;

        foreach (var block in item.Blocks)
        {
            if (first && block is ParagraphBlock paragraph)
            {
                LayoutTextBlock(context, paragraph.Inlines, TextBlockRole.Paragraph, compactOptions);
            }
            else if (first && block is HeadingBlock heading)
            {
                LayoutTextBlock(context, heading.Inlines, TextBlockRole.Heading, compactOptions with { HeadingLevel = heading.Level });
            }
            else
            {
                var nestedOptions = block is ListBlock
                    ? options with { Compact = first, ListDepth = options.ListDepth + 1 }
                    : options with { Compact = first };

                LayoutBlock(context, block, nestedOptions);
            }

            first = false;
        }

        context.X = originalX;
        context.AvailableWidth = originalWidth;
    }

    private static void AddListMarkerElement(
        LayoutContext context,
        DocumentListItem item,
        string marker,
        int markerX,
        int markerColumnWidth,
        int y,
        BlockLayoutOptions options)
    {
        if (item.IsChecked is not null)
        {
            context.TextMap.Append(item.IsChecked.Value ? "[x] " : "[ ] ");
            var size = Math.Min(markerColumnWidth, Math.Max(1, context.BaseFontSize));
            var checkBounds = new Rectangle(
                markerX + markerColumnWidth - size,
                y + Math.Max(0, (context.BaseFontSize - size) / 2),
                size,
                size);

            context.Elements.Add(new DocumentTaskCheckBoxLayoutElement(
                checkBounds,
                item.IsChecked.Value ? CheckState.Checked : CheckState.Unchecked));
            return;
        }

        var block = CreateTextBlock(markerColumnWidth, TextAlignment.Right);
        var state = options.Quoted ? InlineRunState.Normal with { Quoted = true } : InlineRunState.Normal;
        AppendText(block, marker, context, state, null);

        var height = GetTextBlockHeight(block, context.BaseFontSize);
        var markerBounds = new Rectangle(
            markerX,
            y,
            markerColumnWidth,
            height);

        var documentTextStart = context.TextMap.Append(marker);
        var element = new DocumentTextLayoutElement(
            markerBounds,
            block,
            markerBounds.Location,
            marker,
            documentTextStart);
        context.Elements.Add(element);
        context.TextMap.AddElement(element);
        context.TextMap.Append(" ");
    }

    private static int MeasureListMarkerColumn(LayoutContext context, ListBlock list, int listDepth)
    {
        var width = Math.Max(1, context.BaseFontSize);
        var index = list.StartNumber;

        foreach (var item in list.Items)
        {
            if (item.IsChecked is not null)
            {
                width = Math.Max(width, context.BaseFontSize);
            }
            else
            {
                var marker = list.Ordered ? index.ToString() + "." : GetUnorderedListMarker(listDepth);
                width = Math.Max(width, MeasureInlineTextWidth(context, marker, InlineRunState.Normal));
            }

            if (list.Ordered)
                index++;
        }

        return width;
    }

    private static void LayoutQuoteBlock(LayoutContext context, QuoteBlock quote, BlockLayoutOptions options)
    {
        var originalX = context.X;
        var originalWidth = context.AvailableWidth;
        var quoteIndent = context.Scale(context.Style.QuoteIndent);
        var borderWidth = Math.Max(1, context.Scale(context.Style.QuoteBorderWidth));
        var startY = context.Y;
        var insertionIndex = context.Elements.Count;
        var firstBlock = true;

        AddTextSeparationBeforeBlock(context, options);

        context.X += quoteIndent + borderWidth;
        context.AvailableWidth = Math.Max(1, originalWidth - quoteIndent - borderWidth);

        foreach (var block in quote.Blocks)
        {
            if (!firstBlock)
                context.TextMap.EnsureBlockSeparation();

            LayoutBlock(context, block, options with { Compact = true, Quoted = true });
            firstBlock = false;
        }

        context.X = originalX;
        context.AvailableWidth = originalWidth;

        if (context.Y <= startY)
            return;

        var borderBounds = new Rectangle(originalX + quoteIndent / 2, startY, borderWidth, context.Y - startY);
        context.Elements.Insert(
            insertionIndex,
            new DocumentQuoteBorderLayoutElement(
                borderBounds,
                context.Style.ResolveQuoteBorderColor(context.Viewer)));

        context.Y += context.Scale(context.Style.ParagraphSpacing);
    }

    private static void LayoutTextBlock(
        LayoutContext context,
        IEnumerable<DocumentInline> inlines,
        TextBlockRole role,
        BlockLayoutOptions options)
    {
        var inlineList = inlines as IReadOnlyList<DocumentInline> ?? inlines.ToArray();
        LayoutRichTextBlock(context, inlineList, role, options);
    }

    private static void LayoutRichTextBlock(
        LayoutContext context,
        IReadOnlyList<DocumentInline> inlines,
        TextBlockRole role,
        BlockLayoutOptions options)
    {
        var spacingBefore = role == TextBlockRole.Heading
            ? context.Scale(context.Style.HeadingTopSpacing)
            : context.Scale(context.Style.ParagraphSpacing);

        AddSpacingBeforeBlock(context, spacingBefore, options);
        AddTextSeparationBeforeBlock(context, options);

        var block = CreateTextBlock(context.AvailableWidth);
        var pendingLinks = new List<PendingLink>();
        var state = InlineRunState.Normal;

        if (role == TextBlockRole.Heading)
        {
            state = state with
            {
                Bold = true,
                HeadingLevel = Math.Clamp(options.HeadingLevel, 1, 6)
            };
        }

        if (options.Quoted)
            state = state with { Quoted = true };

        AppendInlines(block, inlines, context, state, pendingLinks);

        if (block.Length == 0)
            return;

        var height = GetTextBlockHeight(block, context.BaseFontSize);
        var bounds = new Rectangle(context.X, context.Y, context.AvailableWidth, height);
        var text = GetPlainText(inlines);
        var documentTextStart = context.TextMap.Append(text);
        var element = new DocumentTextLayoutElement(
            bounds,
            block,
            bounds.Location,
            text,
            documentTextStart);

        context.Elements.Add(element);
        context.TextMap.AddElement(element);

        foreach (var pending in pendingLinks)
            context.Links.Add(new DocumentLayoutLink(element, pending.Link, pending.Start, pending.End, pending.Text));

        context.Y = bounds.Bottom + (role == TextBlockRole.Heading
            ? context.Scale(context.Style.HeadingBottomSpacing)
            : context.Scale(context.Style.ParagraphSpacing));
    }

    private static void AddLinksForTextElement(
        LayoutContext context,
        DocumentTextLayoutElement element,
        IEnumerable<PendingLink> pendingLinks)
    {
        foreach (var pending in pendingLinks)
            context.Links.Add(new DocumentLayoutLink(element, pending.Link, pending.Start, pending.End, pending.Text));
    }

    private static int[] CalculateTableColumnWidths(
        LayoutContext context,
        TableBlock table,
        int columnCount,
        int padding,
        int border)
    {
        var chromeWidth = AddWidths(padding, border);
        chromeWidth = AddWidths(chromeWidth, chromeWidth);
        var minimumWidths = Enumerable.Repeat(AddWidths(context.BaseFontSize, chromeWidth), columnCount).ToArray();
        var preferredWidths = (int[])minimumWidths.Clone();

        foreach (var row in table.Rows)
        {
            for (var column = 0; column < Math.Min(columnCount, row.Cells.Count); column++)
            {
                var cell = row.Cells[column];
                var alignment = column < table.Columns.Count
                    ? table.Columns[column].Alignment
                    : DocumentTextAlignment.Left;
                var unwrapped = CreateTableCellTextBlock(context, cell, null, alignment, row.IsHeader);
                var preferredContentWidth = GetMeasuredWidth(unwrapped.TextBlock);
                var minimumContentWidth = MeasureLongestTableCellToken(context, cell, row.IsHeader);

                minimumWidths[column] = Math.Max(minimumWidths[column], AddWidths(minimumContentWidth, chromeWidth));
                preferredWidths[column] = Math.Max(preferredWidths[column], AddWidths(preferredContentWidth, chromeWidth));
            }
        }

        return DocumentTableLayoutCalculator.Calculate(context.AvailableWidth, minimumWidths, preferredWidths);
    }

    private static TableCellLayout CreateTableCellTextBlock(
        LayoutContext context,
        DocumentTableCell cell,
        int? contentWidth,
        DocumentTextAlignment alignment,
        bool header)
    {
        var block = CreateTextBlock(contentWidth, MapTextAlignment(alignment));
        var pendingLinks = new List<PendingLink>();
        var text = new StringBuilder();
        var state = InlineRunState.Normal with { Bold = header };
        var first = true;

        if (cell.Blocks.Count == 0)
            return new TableCellLayout(block, string.Empty, pendingLinks);

        foreach (var child in cell.Blocks)
        {
            if (!first)
            {
                AppendText(block, "\n", context, state, pendingLinks);
                text.Append('\n');
            }

            switch (child)
            {
                case ParagraphBlock paragraph:
                    AppendInlines(block, paragraph.Inlines, context, state, pendingLinks);
                    text.Append(GetPlainText(paragraph.Inlines));
                    break;
                case HeadingBlock heading:
                    AppendInlines(block, heading.Inlines, context, state with { Bold = true }, pendingLinks);
                    text.Append(GetPlainText(heading.Inlines));
                    break;
                case CodeBlock code:
                    var codeText = code.Text.TrimEnd('\r', '\n');
                    AppendText(block, codeText, context, state with { Code = true }, pendingLinks);
                    text.Append(codeText);
                    break;
                case ImageBlock image:
                    var imageText = GetImageFallbackText(image);
                    AppendText(block, imageText, context, state, pendingLinks);
                    text.Append(imageText);
                    break;
                default:
                    var fallbackText = GetPlainText(new[] { child });
                    AppendText(block, fallbackText, context, state, pendingLinks);
                    text.Append(fallbackText);
                    break;
            }

            first = false;
        }

        return new TableCellLayout(block, text.ToString(), pendingLinks);
    }

    private static void LayoutImage(
        LayoutContext context,
        string source,
        string altText,
        BlockLayoutOptions options)
    {
        var spacing = context.Scale(context.Style.ImageSpacing);
        AddSpacingBeforeBlock(context, spacing, options);

        var resource = context.Viewer.GetImageResource(source);
        AddTextSeparationBeforeBlock(context, options);

        if (resource is { State: DocumentImageResourceState.Loaded, Bitmap: { } bitmap })
        {
            var size = GetConstrainedImageSize(bitmap, context.AvailableWidth);
            var bounds = new Rectangle(context.X, context.Y, size.Width, size.Height);
            context.Elements.Add(new DocumentLoadedImageLayoutElement(bounds, bitmap));

            context.Y = bounds.Bottom + spacing;
            return;
        }

        var padding = context.Scale(context.Style.CodePadding);
        var fallbackText = GetImageFallbackText(source, altText);
        var placeholderText = CreateTextBlock(context.AvailableWidth - (padding * 2));
        AppendText(placeholderText, fallbackText, context, InlineRunState.Normal, null);

        var textHeight = GetTextBlockHeight(placeholderText, context.BaseFontSize);
        var placeholderHeight = Math.Max(context.BaseFontSize * 3, textHeight + (padding * 2));
        var placeholderBounds = new Rectangle(context.X, context.Y, context.AvailableWidth, placeholderHeight);
        var textOrigin = new Point(placeholderBounds.X + padding, placeholderBounds.Y + padding);

        context.Elements.Add(new DocumentImagePlaceholderLayoutElement(
            placeholderBounds,
            placeholderText,
            textOrigin,
            fallbackText,
            context.Style.ResolveImagePlaceholderColor(context.Viewer),
            resource?.State == DocumentImageResourceState.Failed));

        context.Y = placeholderBounds.Bottom + spacing;
    }

    private static void LayoutTableBlock(LayoutContext context, TableBlock table, BlockLayoutOptions options)
    {
        if (table.Rows.Count == 0)
            return;

        AddSpacingBeforeBlock(context, context.Scale(context.Style.ParagraphSpacing), options);
        AddTextSeparationBeforeBlock(context, options);

        var columnCount = Math.Max(
            table.Columns.Count,
            table.Rows.Select(row => row.Cells.Count).DefaultIfEmpty(0).Max());

        if (columnCount == 0)
            return;

        var border = Math.Max(1, context.Scale(context.Style.TableBorderThickness));
        var padding = context.Scale(context.Style.TableCellPadding);
        var columnWidths = CalculateTableColumnWidths(context, table, columnCount, padding, border);
        var tableX = context.X;
        var y = context.Y;

        var firstRow = true;
        foreach (var row in table.Rows)
        {
            if (!firstRow)
                context.TextMap.EnsureLineBreak();

            var cellLayouts = new List<TableCellLayout>(columnCount);
            var rowHeight = border;

            for (var column = 0; column < columnCount; column++)
            {
                var cell = column < row.Cells.Count
                    ? row.Cells[column]
                    : new DocumentTableCell(Array.Empty<DocumentBlock>());

                var contentWidth = Math.Max(1, columnWidths[column] - (padding * 2) - (border * 2));
                var alignment = column < table.Columns.Count ? table.Columns[column].Alignment : DocumentTextAlignment.Left;
                var cellLayout = CreateTableCellTextBlock(context, cell, contentWidth, alignment, row.IsHeader);
                var textHeight = GetTextBlockHeight(cellLayout.TextBlock, context.BaseFontSize);

                rowHeight = Math.Max(rowHeight, textHeight + (padding * 2) + (border * 2));
                cellLayouts.Add(cellLayout);
            }

            var x = tableX;

            for (var column = 0; column < columnCount; column++)
            {
                var width = columnWidths[column];
                var cellBounds = new Rectangle(x, y, width, rowHeight);
                var fillColor = row.IsHeader
                    ? context.Style.ResolveTableHeaderBackgroundColor(context.Viewer)
                    : context.Style.ResolveTableCellBackgroundColor(context.Viewer);
                var borderColor = context.Style.ResolveTableBorderColor(context.Viewer);

                context.Elements.Add(new DocumentTableCellLayoutElement(
                    cellBounds,
                    fillColor,
                    borderColor,
                    border));

                var textOrigin = new Point(cellBounds.X + padding + border, cellBounds.Y + padding + border);
                var textBounds = new Rectangle(textOrigin.X, textOrigin.Y, Math.Max(1, width - (padding * 2) - (border * 2)), Math.Max(1, rowHeight - (padding * 2) - (border * 2)));
                if (column > 0)
                    context.TextMap.Append("\t");

                var text = cellLayouts[column].Text;
                var documentTextStart = context.TextMap.Append(text);
                var textElement = new DocumentTextLayoutElement(
                    textBounds,
                    cellLayouts[column].TextBlock,
                    textOrigin,
                    text,
                    documentTextStart);

                if (text.Length > 0)
                {
                    context.Elements.Add(textElement);
                    context.TextMap.AddElement(textElement);
                    AddLinksForTextElement(context, textElement, cellLayouts[column].TextBlockLinks);
                }
                x += width;
            }

            y += rowHeight - border;
            firstRow = false;
        }

        context.Y = y + context.Scale(context.Style.ParagraphSpacing);
    }

    private static void LayoutFootnoteGroupBlock(LayoutContext context, FootnoteGroupBlock footnotes, BlockLayoutOptions options)
    {
        if (footnotes.Footnotes.Count == 0)
            return;

        AddSpacingBeforeBlock(context, context.Scale(context.Style.ParagraphSpacing), options);
        LayoutHorizontalRule(context);

        var first = true;
        foreach (var footnote in footnotes.Footnotes.OrderBy(footnote => footnote.Order))
        {
            if (!first)
                context.TextMap.EnsureLineBreak();

            var item = new DocumentListItem(footnote.Blocks);
            var marker = "[" + footnote.Order + "]";
            var markerWidth = Math.Max(context.BaseFontSize, MeasureInlineTextWidth(context, marker, InlineRunState.Normal));
            LayoutListItem(context, item, marker, markerWidth, options with { Compact = true });
            context.Y += context.Scale(context.Style.ListItemSpacing);
            first = false;
        }
    }

    private static void AddTextSeparationBeforeBlock(LayoutContext context, BlockLayoutOptions options)
    {
        if (!options.Compact)
            context.TextMap.EnsureBlockSeparation();
    }

    private static void AddSpacingBeforeBlock(LayoutContext context, int spacing, BlockLayoutOptions options)
    {
        if (options.Compact || context.Elements.Count == 0)
            return;

        context.Y += spacing;
    }

    private static void AppendInlines(
        TextBlock block,
        IEnumerable<DocumentInline> inlines,
        LayoutContext context,
        InlineRunState state,
        List<PendingLink> pendingLinks)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    AppendText(block, text.Text, context, state, pendingLinks);
                    break;
                case CodeInline code:
                    AppendText(block, code.Text, context, state with { Code = true }, pendingLinks);
                    break;
                case LineBreakInline lineBreak:
                    AppendText(block, lineBreak.Hard ? "\n" : " ", context, state, pendingLinks);
                    break;
                case StrongInline strong:
                    AppendInlines(block, strong.Inlines, context, state with { Bold = true }, pendingLinks);
                    break;
                case EmphasisInline emphasis:
                    AppendInlines(block, emphasis.Inlines, context, state with { Italic = true }, pendingLinks);
                    break;
                case StrikethroughInline strike:
                    AppendInlines(block, strike.Inlines, context, state with { Strike = true }, pendingLinks);
                    break;
                case LinkInline link:
                    AppendInlines(block, link.Inlines, context, state with { Link = link }, pendingLinks);
                    break;
                case ImageInline image:
                    AppendText(block, GetImageFallbackText(image), context, state, pendingLinks);
                    break;
                case FootnoteReferenceInline footnote:
                    AppendText(block, "[" + footnote.Order + "]", context, state, pendingLinks);
                    break;
            }
        }
    }

    private static void AppendText(
        TextBlock block,
        string text,
        LayoutContext context,
        InlineRunState state,
        List<PendingLink>? pendingLinks)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var start = block.Length;
        block.AddText(text, CreateRichTextStyle(context, state));
        var end = block.Length;

        if (state.Link is not null && pendingLinks is not null && end > start)
            pendingLinks.Add(new PendingLink(state.Link, start, end, GetPlainText(state.Link.Inlines)));
    }

    private static void AppendHighlightedCode(
        TextBlock block,
        string text,
        string? language,
        LayoutContext context,
        InlineRunState state)
    {
        var spans = DocumentSyntaxHighlighterRegistry.Highlight(language, text);
        var cursor = 0;

        foreach (var span in spans)
        {
            if (span.Start < cursor || span.Start < 0 || span.End > text.Length)
                continue;

            if (span.Start > cursor)
                AppendText(block, text[cursor..span.Start], context, state, null);

            AppendText(block, text[span.Start..span.End], context, state with { SyntaxToken = span.Kind }, null);
            cursor = span.End;
        }

        if (cursor < text.Length)
            AppendText(block, text[cursor..], context, state, null);
    }

    private static TextBlock CreateTextBlock(int? maxWidth)
        => CreateTextBlock(maxWidth, TextAlignment.Left);

    private static TextBlock CreateTextBlock(int? maxWidth, TextAlignment alignment)
        => new TextBlock
        {
            MaxWidth = maxWidth.HasValue ? Math.Max(1, maxWidth.Value) : null,
            MaxHeight = null,
            Alignment = alignment,
            EllipsisEnabled = false
        };

    private static TextAlignment MapTextAlignment(DocumentTextAlignment alignment)
        => alignment switch
        {
            DocumentTextAlignment.Center => TextAlignment.Center,
            DocumentTextAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left
        };

    private static int MeasureInlineTextWidth(LayoutContext context, string text, InlineRunState state)
    {
        var block = CreateTextBlock(null);
        AppendText(block, text, context, state, null);
        return GetMeasuredWidth(block);
    }

    private static int MeasureLongestTableCellToken(LayoutContext context, DocumentTableCell cell, bool header)
    {
        var text = GetPlainText(cell.Blocks);
        var longest = Math.Max(1, context.BaseFontSize);
        var state = InlineRunState.Normal with { Bold = header };

        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            longest = Math.Max(longest, MeasureInlineTextWidth(context, token, state));

        return longest;
    }

    private static int GetMeasuredWidth(TextBlock block)
    {
        var measured = Math.Ceiling(block.MeasuredWidth);
        return measured >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)measured);
    }

    private static int AddWidths(int first, int second)
    {
        var total = (long)first + second;
        return total >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)total);
    }

    private static Style CreateRichTextStyle(LayoutContext context, InlineRunState state)
    {
        var controlStyle = context.Viewer.CurrentStyle;
        var baseTypeface = controlStyle.GetFont();
        var fontSize = context.BaseFontSize;
        var textColor = state.Quoted
            ? context.Style.ResolveQuoteForegroundColor(context.Viewer)
            : context.Style.ResolveForegroundColor(context.Viewer);

        if (state.HeadingLevel > 0)
        {
            fontSize = Math.Max(1, (int)Math.Round(fontSize * context.Style.GetHeadingScale(state.HeadingLevel)));
            textColor = context.Style.ResolveHeadingColor(context.Viewer);
        }

        if (state.Code)
            textColor = context.Style.ResolveCodeForegroundColor(context.Viewer);

        if (state.SyntaxToken is DocumentSyntaxTokenKind tokenKind)
            textColor = context.Style.CodeStyle.Resolve(tokenKind, textColor, context.Viewer.Enabled);

        if (state.Link is not null)
            textColor = context.Style.ResolveLinkColor(context.Viewer, state.Link == context.HoveredLink, state.Link == context.PressedLink);

        return new Style
        {
            FontFamily = state.Code ? context.Style.CodeFontFamily : baseTypeface.FamilyName,
            FontSize = fontSize,
            TextColor = textColor,
            BackgroundColor = state.Code ? context.Style.ResolveCodeBackgroundColor(context.Viewer) : SKColor.Empty,
            FontWeight = state.Bold ? (int)SKFontStyleWeight.Bold : baseTypeface.FontWeight,
            FontItalic = state.Italic || baseTypeface.FontSlant is SKFontStyleSlant.Italic or SKFontStyleSlant.Oblique,
            Underline = state.Link is not null ? UnderlineStyle.Solid : UnderlineStyle.None,
            StrikeThrough = state.Strike ? StrikeThroughStyle.Solid : StrikeThroughStyle.None
        };
    }

    private static Style CreateCodeLanguageStyle(LayoutContext context)
    {
        var foreground = context.Style.ResolveCodeForegroundColor(context.Viewer);
        return new Style
        {
            FontFamily = context.Style.CodeFontFamily,
            FontSize = Math.Max(1, context.BaseFontSize - context.Scale(2)),
            TextColor = foreground,
            FontWeight = (int)SKFontStyleWeight.Bold
        };
    }

    private static int GetTextBlockHeight(TextBlock block, int fallbackFontSize)
    {
        var measured = (int)Math.Ceiling(block.MeasuredHeight);
        return measured > 0 ? measured : fallbackFontSize + 2;
    }

    private static Size GetConstrainedImageSize(SKBitmap bitmap, int availableWidth)
    {
        var naturalWidth = Math.Max(1, bitmap.Width);
        var naturalHeight = Math.Max(1, bitmap.Height);
        var width = Math.Max(1, Math.Min(naturalWidth, Math.Max(1, availableWidth)));
        var height = Math.Max(1, (int)Math.Round(naturalHeight * (width / (double)naturalWidth)));

        return new Size(width, height);
    }

    private static string GetImageFallbackText(ImageInline image)
        => GetImageFallbackText(image.Source, image.AltText);

    private static string GetImageFallbackText(ImageBlock image)
        => GetImageFallbackText(image.Source, image.AltText);

    private static string GetImageFallbackText(string source, string altText)
    {
        if (!string.IsNullOrWhiteSpace(altText))
            return altText;

        if (!string.IsNullOrWhiteSpace(source))
            return source;

        return "Image";
    }

    private static string GetListMarker(ListBlock list, DocumentListItem item, int index, int listDepth)
    {
        if (item.IsChecked is true)
            return string.Empty;

        if (item.IsChecked is false)
            return string.Empty;

        if (list.Ordered)
            return index.ToString() + ".";

        return GetUnorderedListMarker(listDepth);
    }

    private static string GetUnorderedListMarker(int listDepth)
    {
        var index = ((listDepth % UnorderedListMarkers.Length) + UnorderedListMarkers.Length) % UnorderedListMarkers.Length;
        return UnorderedListMarkers[index];
    }

    private static string GetPlainText(IEnumerable<DocumentInline> inlines)
    {
        var parts = new List<string>();
        CollectPlainText(inlines, parts);
        return string.Concat(parts);
    }

    private static string GetPlainText(IEnumerable<DocumentBlock> blocks)
    {
        var document = new Document(blocks);
        return document.GetPlainText();
    }

    private static void CollectPlainText(IEnumerable<DocumentInline> inlines, List<string> parts)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    parts.Add(text.Text);
                    break;
                case CodeInline code:
                    parts.Add(code.Text);
                    break;
                case LineBreakInline lineBreak:
                    parts.Add(lineBreak.Hard ? "\n" : " ");
                    break;
                case StrongInline strong:
                    CollectPlainText(strong.Inlines, parts);
                    break;
                case EmphasisInline emphasis:
                    CollectPlainText(emphasis.Inlines, parts);
                    break;
                case StrikethroughInline strike:
                    CollectPlainText(strike.Inlines, parts);
                    break;
                case LinkInline link:
                    CollectPlainText(link.Inlines, parts);
                    break;
                case ImageInline image:
                    parts.Add(GetImageFallbackText(image));
                    break;
                case FootnoteReferenceInline footnote:
                    parts.Add("[");
                    parts.Add(footnote.Order.ToString());
                    parts.Add("]");
                    break;
            }
        }
    }

    private sealed class LayoutContext
    {
        public LayoutContext(
            DocumentViewer viewer,
            DocumentStyle style,
            Rectangle contentBounds,
            LinkInline? hoveredLink,
            LinkInline? pressedLink)
        {
            Viewer = viewer;
            Style = style;
            X = contentBounds.X;
            Y = contentBounds.Y;
            AvailableWidth = Math.Max(1, contentBounds.Width);
            HoveredLink = hoveredLink;
            PressedLink = pressedLink;
            BaseFontSize = viewer.LogicalToDeviceUnits(viewer.CurrentStyle.GetFontSize());
        }

        public int AvailableWidth { get; set; }

        public int BaseFontSize { get; }

        public List<DocumentLayoutElement> Elements { get; } = new();

        public LinkInline? HoveredLink { get; }

        public List<DocumentLayoutLink> Links { get; } = new();

        public LinkInline? PressedLink { get; }

        public DocumentStyle Style { get; }

        public DocumentTextMapBuilder TextMap { get; } = new();

        public DocumentViewer Viewer { get; }

        public int X { get; set; }

        public int Y { get; set; }

        public int Scale(int logicalPixels) => Viewer.LogicalToDeviceUnits(logicalPixels);
    }

    private readonly record struct BlockLayoutOptions(bool Compact, bool Quoted, int HeadingLevel, int ListDepth)
    {
        public static BlockLayoutOptions Default { get; } = new(false, false, 0, 0);

        public static BlockLayoutOptions CompactOptions { get; } = new(true, false, 0, 0);
    }

    private readonly record struct InlineRunState(
        bool Bold,
        bool Italic,
        bool Strike,
        bool Code,
        bool Quoted,
        int HeadingLevel,
        LinkInline? Link,
        DocumentSyntaxTokenKind? SyntaxToken)
    {
        public static InlineRunState Normal { get; } = new(false, false, false, false, false, 0, null, null);

        public static InlineRunState CodeBlock { get; } = new(false, false, false, true, false, 0, null, null);
    }

    private readonly record struct PendingLink(LinkInline Link, int Start, int End, string Text);

    private sealed record TableCellLayout(
        TextBlock TextBlock,
        string Text,
        IReadOnlyList<PendingLink> TextBlockLinks);

    private enum TextBlockRole
    {
        Paragraph,
        Heading
    }
}
