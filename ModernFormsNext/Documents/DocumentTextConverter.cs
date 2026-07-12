using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModernFormsNext.Documents;

/// <summary>
/// Converts ModernFormsNext documents to plain text.
/// </summary>
/// <remarks>
/// This converter is intentionally independent of Markdown. It provides a stable copy/export
/// representation for complete-document export. <see cref="DocumentViewer"/> maintains a related
/// layout text map for selecting and copying only part of a rendered document.
/// </remarks>
public static class DocumentTextConverter
{
    private static readonly string[] UnorderedListMarkers = { "\u2022", "\u25e6", "\u25aa" };

    /// <summary>
    /// Converts a document to plain text.
    /// </summary>
    /// <param name="document">The document to convert.</param>
    /// <returns>A plain-text representation of the document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>.</exception>
    public static string ToPlainText(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();

        foreach (var block in document.Blocks)
            AppendBlock(builder, block, new TextExportContext());

        return builder.ToString().TrimEnd();
    }

    private static void AppendBlock(StringBuilder builder, DocumentBlock block, TextExportContext context)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                AppendInlines(builder, paragraph.Inlines);
                AppendBlankLine(builder);
                break;
            case HeadingBlock heading:
                AppendInlines(builder, heading.Inlines);
                AppendBlankLine(builder);
                break;
            case CodeBlock code:
                builder.AppendLine(code.Text.TrimEnd('\r', '\n'));
                AppendBlankLine(builder);
                break;
            case QuoteBlock quote:
                AppendQuotedBlock(builder, quote, context);
                break;
            case ListBlock list:
                AppendList(builder, list, context);
                break;
            case HorizontalRuleBlock:
                builder.AppendLine(new string('-', 3));
                AppendBlankLine(builder);
                break;
            case ImageBlock image:
                builder.AppendLine(GetImageFallbackText(image));
                AppendBlankLine(builder);
                break;
            case TableBlock table:
                AppendTable(builder, table);
                AppendBlankLine(builder);
                break;
            case FootnoteGroupBlock footnotes:
                AppendFootnotes(builder, footnotes, context);
                break;
        }
    }

    private static void AppendFootnotes(StringBuilder builder, FootnoteGroupBlock footnotes, TextExportContext context)
    {
        foreach (var footnote in footnotes.Footnotes.OrderBy(footnote => footnote.Order))
        {
            builder.Append('[').Append(footnote.Order).Append("] ");
            AppendBlocksInline(builder, footnote.Blocks, context);
            builder.AppendLine();
        }

        AppendBlankLine(builder);
    }

    private static void AppendList(StringBuilder builder, ListBlock list, TextExportContext context)
    {
        var number = list.StartNumber;

        foreach (var item in list.Items)
        {
            var marker = item.IsChecked switch
            {
                true => "[x]",
                false => "[ ]",
                _ when list.Ordered => number.ToString() + ".",
                _ => GetUnorderedListMarker(context.ListDepth)
            };

            builder.Append(new string(' ', context.ListDepth * 2));
            builder.Append(marker).Append(' ');
            AppendBlocksInline(builder, item.Blocks, context with { ListDepth = context.ListDepth + 1 });
            builder.AppendLine();

            if (list.Ordered)
                number++;
        }

        AppendBlankLine(builder);
    }

    private static void AppendQuotedBlock(StringBuilder builder, QuoteBlock quote, TextExportContext context)
    {
        var text = new StringBuilder();

        foreach (var block in quote.Blocks)
            AppendBlock(text, block, context);

        foreach (var line in text.ToString().TrimEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            builder.Append("> ").AppendLine(line);

        AppendBlankLine(builder);
    }

    private static void AppendTable(StringBuilder builder, TableBlock table)
    {
        foreach (var row in table.Rows)
        {
            for (var i = 0; i < row.Cells.Count; i++)
            {
                if (i > 0)
                    builder.Append('\t');

                AppendBlocksInline(builder, row.Cells[i].Blocks, new TextExportContext());
            }

            builder.AppendLine();
        }
    }

    private static void AppendBlocksInline(StringBuilder builder, IReadOnlyList<DocumentBlock> blocks, TextExportContext context)
    {
        var first = true;

        foreach (var block in blocks)
        {
            if (!first)
                builder.Append(' ');

            switch (block)
            {
                case ParagraphBlock paragraph:
                    AppendInlines(builder, paragraph.Inlines);
                    break;
                case HeadingBlock heading:
                    AppendInlines(builder, heading.Inlines);
                    break;
                case CodeBlock code:
                    builder.Append(code.Text.TrimEnd('\r', '\n'));
                    break;
                case ImageBlock image:
                    builder.Append(GetImageFallbackText(image));
                    break;
                default:
                    var nested = new StringBuilder();
                    AppendBlock(nested, block, context);
                    builder.Append(nested.ToString().Trim());
                    break;
            }

            first = false;
        }
    }

    private static void AppendInlines(StringBuilder builder, IEnumerable<DocumentInline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    builder.Append(text.Text);
                    break;
                case CodeInline code:
                    builder.Append(code.Text);
                    break;
                case LineBreakInline:
                    builder.AppendLine();
                    break;
                case StrongInline strong:
                    AppendInlines(builder, strong.Inlines);
                    break;
                case EmphasisInline emphasis:
                    AppendInlines(builder, emphasis.Inlines);
                    break;
                case StrikethroughInline strike:
                    AppendInlines(builder, strike.Inlines);
                    break;
                case LinkInline link:
                    AppendInlines(builder, link.Inlines);
                    break;
                case ImageInline image:
                    builder.Append(GetImageFallbackText(image));
                    break;
                case FootnoteReferenceInline footnote:
                    builder.Append('[').Append(footnote.Order).Append(']');
                    break;
            }
        }
    }

    private static void AppendBlankLine(StringBuilder builder)
    {
        if (builder.Length == 0)
            return;

        if (builder[^1] != '\n')
            builder.AppendLine();

        builder.AppendLine();
    }

    private static string GetImageFallbackText(ImageBlock image)
    {
        if (!string.IsNullOrWhiteSpace(image.AltText))
            return image.AltText;

        if (!string.IsNullOrWhiteSpace(image.Source))
            return image.Source;

        return "Image";
    }

    private static string GetImageFallbackText(ImageInline image)
    {
        if (!string.IsNullOrWhiteSpace(image.AltText))
            return image.AltText;

        if (!string.IsNullOrWhiteSpace(image.Source))
            return image.Source;

        return "Image";
    }

    private static string GetUnorderedListMarker(int listDepth)
    {
        var index = ((listDepth % UnorderedListMarkers.Length) + UnorderedListMarkers.Length) % UnorderedListMarkers.Length;
        return UnorderedListMarkers[index];
    }

    private readonly record struct TextExportContext(int ListDepth)
    {
        public TextExportContext()
            : this(0)
        {
        }
    }
}
