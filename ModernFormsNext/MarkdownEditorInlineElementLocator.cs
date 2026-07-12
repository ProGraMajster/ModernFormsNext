using System;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ModernFormsNext;

internal sealed class MarkdownEditorInlineElementLocator
{
    private const int MaximumSourceLength = 4096;
    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public MarkdownEditorInlineElement? Find(string source, int selectionStart, int selectionLength, bool image)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (selectionStart < 0 || selectionLength < 0 || selectionStart + selectionLength > source.Length)
            return null;

        var lineStart = selectionStart > 0 ? source.LastIndexOf('\n', selectionStart - 1) + 1 : 0;
        var lineEnd = source.IndexOf('\n', selectionStart);
        if (lineEnd < 0)
            lineEnd = source.Length;
        if (lineEnd > lineStart && source[lineEnd - 1] == '\r')
            lineEnd--;

        if (lineEnd - lineStart > MaximumSourceLength)
            return null;

        var fragment = source.Substring(lineStart, lineEnd - lineStart);
        var document = Markdown.Parse(fragment, pipeline);
        return FindInBlocks(document, lineStart, selectionStart, selectionLength, image);
    }

    private static MarkdownEditorInlineElement? FindInBlocks(
        ContainerBlock blocks,
        int sourceOffset,
        int selectionStart,
        int selectionLength,
        bool image)
    {
        foreach (var block in blocks)
        {
            if (block is LeafBlock leaf && leaf.Inline is not null)
            {
                var match = FindInInlines(leaf.Inline, sourceOffset, selectionStart, selectionLength, image);
                if (match is not null)
                    return match;
            }

            if (block is ContainerBlock nested)
            {
                var match = FindInBlocks(nested, sourceOffset, selectionStart, selectionLength, image);
                if (match is not null)
                    return match;
            }
        }

        return null;
    }

    private static MarkdownEditorInlineElement? FindInInlines(
        ContainerInline container,
        int sourceOffset,
        int selectionStart,
        int selectionLength,
        bool image)
    {
        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            if (inline is Markdig.Syntax.Inlines.LinkInline link && link.IsImage == image)
            {
                var start = sourceOffset + link.Span.Start;
                var end = sourceOffset + link.Span.End + 1;
                var selectionEnd = selectionStart + selectionLength;
                var contains = selectionLength == 0
                    ? selectionStart >= start && selectionStart <= end
                    : selectionStart >= start && selectionEnd <= end;
                if (contains)
                {
                    return new MarkdownEditorInlineElement(
                        start,
                        Math.Max(0, end - start),
                        GetInlineText(link),
                        link.Url ?? string.Empty,
                        link.Title,
                        image);
                }
            }

            if (inline is ContainerInline nested)
            {
                var match = FindInInlines(nested, sourceOffset, selectionStart, selectionLength, image);
                if (match is not null)
                    return match;
            }
        }

        return null;
    }

    private static string GetInlineText(ContainerInline container)
    {
        var builder = new StringBuilder();
        AppendInlineText(container, builder);
        return builder.ToString();
    }

    private static void AppendInlineText(ContainerInline container, StringBuilder builder)
    {
        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case Markdig.Syntax.Inlines.CodeInline code:
                    builder.Append(code.Content);
                    break;
                case LineBreakInline:
                    builder.AppendLine();
                    break;
                case ContainerInline nested:
                    AppendInlineText(nested, builder);
                    break;
            }
        }
    }
}

internal sealed record MarkdownEditorInlineElement(
    int Start,
    int Length,
    string Text,
    string Destination,
    string? Title,
    bool IsImage);
