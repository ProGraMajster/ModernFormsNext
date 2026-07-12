using System;
using System.Collections.Generic;
using System.Linq;
using Markdig;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ModernFormsNext.Documents;

/// <summary>
/// Converts Markdown source text into the ModernFormsNext <see cref="Document"/> model.
/// </summary>
/// <remarks>
/// <para>
/// This type isolates the Markdig dependency from the public ModernFormsNext document model. The
/// returned <see cref="Document"/> contains only ModernFormsNext document nodes; callers do not
/// need to reference or understand Markdig syntax-tree types.
/// </para>
/// <para>
    /// The default pipeline enables Markdig advanced extensions so fenced code blocks, strikethrough,
    /// auto links, task lists, tables, images, and footnotes can be represented when present.
    /// Embedded HTML is preserved as text and is not rendered or executed as HTML.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var parser = new MarkdownParser();
/// Document document = parser.Parse("# ModernFormsNext\n\nA **native** document.");
/// </code>
/// </example>
public sealed class MarkdownParser
{
    private readonly MarkdownPipeline pipeline;

    /// <summary>
    /// Initializes a new <see cref="MarkdownParser"/> instance using the default Markdown pipeline.
    /// </summary>
    public MarkdownParser()
        : this(CreateDefaultPipeline())
    {
    }

    private MarkdownParser(MarkdownPipeline pipeline)
    {
        this.pipeline = pipeline;
    }

    /// <summary>
    /// Parses Markdown source into a ModernFormsNext <see cref="Document"/>.
    /// </summary>
    /// <param name="markdown">The Markdown source text. <see langword="null"/> is treated as an empty document.</param>
    /// <returns>The converted document.</returns>
    public Document Parse(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return Document.Empty;

        var markdownDocument = Markdown.Parse(markdown, pipeline);
        return new Document(ConvertBlocks(markdownDocument));
    }

    private static MarkdownPipeline CreateDefaultPipeline()
        => new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

    private static IReadOnlyList<DocumentBlock> ConvertBlocks(ContainerBlock container)
    {
        var blocks = new List<DocumentBlock>();

        foreach (var block in container)
        {
            var converted = ConvertBlock(block);
            if (converted is not null)
                blocks.Add(converted);
        }

        return blocks;
    }

    private static DocumentBlock? ConvertBlock(Block block)
    {
        switch (block)
        {
            case Markdig.Syntax.ParagraphBlock paragraph:
                return ConvertParagraphBlock(paragraph);
            case Markdig.Syntax.HeadingBlock heading:
                return new HeadingBlock(heading.Level, ConvertInlines(heading.Inline));
            case FencedCodeBlock fenced:
                return ConvertCodeBlock(fenced);
            case Markdig.Syntax.CodeBlock code:
                return ConvertCodeBlock(code);
            case Markdig.Syntax.QuoteBlock quote:
                return new QuoteBlock(ConvertBlocks(quote));
            case Markdig.Syntax.ListBlock list:
                return ConvertListBlock(list);
            case Table table:
                return ConvertTableBlock(table);
            case FootnoteGroup footnotes:
                return ConvertFootnoteGroupBlock(footnotes);
            case ThematicBreakBlock:
                return new HorizontalRuleBlock();
            case HtmlBlock html:
                return new ParagraphBlock(new DocumentInline[] { new TextInline(html.Lines.ToString()) });
            case LinkReferenceDefinitionGroup:
                return null;
            case LeafBlock leaf when !string.IsNullOrWhiteSpace(leaf.Lines.ToString()):
                return new ParagraphBlock(new DocumentInline[] { new TextInline(leaf.Lines.ToString()) });
            case ContainerBlock nested:
                return ConvertContainerFallback(nested);
            default:
                return new ParagraphBlock(new DocumentInline[] { new TextInline(block.ToString()) });
        }
    }

    private static DocumentBlock ConvertParagraphBlock(Markdig.Syntax.ParagraphBlock paragraph)
    {
        var inlines = ConvertInlines(paragraph.Inline);

        if (TryGetStandaloneImage(inlines, out var image))
            return new ImageBlock(image.Source, image.AltText, image.Title);

        return new ParagraphBlock(inlines);
    }

    private static DocumentBlock? ConvertContainerFallback(ContainerBlock container)
    {
        var blocks = ConvertBlocks(container);

        if (blocks.Count == 0)
            return null;

        if (blocks.Count == 1)
            return blocks[0];

        return new QuoteBlock(blocks);
    }

    private static CodeBlock ConvertCodeBlock(Markdig.Syntax.CodeBlock block)
    {
        var language = block is FencedCodeBlock fenced ? NormalizeCodeBlockLanguage(fenced.Info) : null;
        return new CodeBlock(block.Lines.ToString(), language);
    }

    private static ListBlock ConvertListBlock(Markdig.Syntax.ListBlock block)
    {
        var items = new List<DocumentListItem>();

        foreach (var child in block)
        {
            if (child is not ListItemBlock item)
                continue;

            var isChecked = TryGetTaskListState(item);
            var itemBlocks = ConvertBlocks(item);
            items.Add(new DocumentListItem(
                isChecked is null ? itemBlocks : TrimTaskListLeadingWhitespace(itemBlocks),
                isChecked));
        }

        var start = 1;
        if (!string.IsNullOrWhiteSpace(block.OrderedStart) && int.TryParse(block.OrderedStart, out var parsed))
            start = parsed;

        return new ListBlock(block.IsOrdered, items, start);
    }

    private static TableBlock ConvertTableBlock(Table table)
    {
        var columns = table.ColumnDefinitions
            .Select(column => new DocumentTableColumn(ConvertAlignment(column.Alignment)))
            .ToList();
        var rows = new List<DocumentTableRow>();

        foreach (var child in table)
        {
            if (child is not TableRow row)
                continue;

            var cells = new List<DocumentTableCell>();

            foreach (var rowChild in row)
            {
                if (rowChild is TableCell cell)
                    cells.Add(new DocumentTableCell(ConvertBlocks(cell)));
            }

            rows.Add(new DocumentTableRow(cells, row.IsHeader));
        }

        return new TableBlock(columns, rows);
    }

    private static FootnoteGroupBlock ConvertFootnoteGroupBlock(FootnoteGroup group)
    {
        var footnotes = new List<DocumentFootnote>();

        foreach (var child in group)
        {
            if (child is not Footnote footnote)
                continue;

            footnotes.Add(new DocumentFootnote(
                footnote.Order <= 0 ? footnotes.Count + 1 : footnote.Order,
                footnote.Label,
                ConvertBlocks(footnote)));
        }

        return new FootnoteGroupBlock(footnotes.OrderBy(footnote => footnote.Order));
    }

    private static DocumentTextAlignment ConvertAlignment(TableColumnAlign? alignment)
        => alignment switch
        {
            TableColumnAlign.Center => DocumentTextAlignment.Center,
            TableColumnAlign.Right => DocumentTextAlignment.Right,
            _ => DocumentTextAlignment.Left
        };

    private static IReadOnlyList<DocumentBlock> TrimTaskListLeadingWhitespace(IReadOnlyList<DocumentBlock> blocks)
    {
        if (blocks.Count == 0 || blocks[0] is not ParagraphBlock paragraph || paragraph.Inlines.Count == 0)
            return blocks;

        var inlines = paragraph.Inlines.ToList();

        if (inlines[0] is not TextInline text)
            return blocks;

        var trimmed = text.Text.TrimStart();

        if (trimmed.Length == text.Text.Length)
            return blocks;

        if (trimmed.Length == 0)
            inlines.RemoveAt(0);
        else
            inlines[0] = new TextInline(trimmed);

        var replacedBlocks = blocks.ToList();
        replacedBlocks[0] = new ParagraphBlock(inlines);
        return replacedBlocks;
    }

    private static IReadOnlyList<DocumentInline> ConvertInlines(ContainerInline? container)
    {
        var inlines = new List<DocumentInline>();
        var child = container?.FirstChild;

        while (child is not null)
        {
            AddInline(child, inlines);
            child = child.NextSibling;
        }

        return inlines;
    }

    private static void AddInline(Inline inline, List<DocumentInline> output)
    {
        if (TryIsTaskListInline(inline, out _))
            return;

        switch (inline)
        {
            case LiteralInline literal:
                AddText(output, literal.Content.ToString());
                break;
            case Markdig.Syntax.Inlines.CodeInline code:
                output.Add(new CodeInline(code.Content));
                break;
            case Markdig.Syntax.Inlines.LineBreakInline lineBreak:
                output.Add(lineBreak.IsHard ? new LineBreakInline(true) : new TextInline(" "));
                break;
            case Markdig.Syntax.Inlines.LinkInline link when link.IsImage:
                AddImageInline(link, output);
                break;
            case Markdig.Syntax.Inlines.LinkInline link:
                output.Add(new LinkInline(link.Url, ConvertInlines(link), link.Title));
                break;
            case AutolinkInline autolink:
                AddAutolinkInline(autolink, output);
                break;
            case FootnoteLink footnote when footnote.IsBackLink:
                break;
            case FootnoteLink footnote:
                output.Add(new FootnoteReferenceInline(footnote.Index, footnote.Footnote?.Label));
                break;
            case Markdig.Syntax.Inlines.EmphasisInline emphasis:
                AddEmphasisInline(emphasis, output);
                break;
            case HtmlInline html:
                AddText(output, html.Tag);
                break;
            case HtmlEntityInline entity:
                AddText(output, entity.Transcoded.ToString());
                break;
            case ContainerInline container:
                output.AddRange(ConvertInlines(container));
                break;
            default:
                AddText(output, inline.ToString() ?? string.Empty);
                break;
        }
    }

    private static void AddEmphasisInline(Markdig.Syntax.Inlines.EmphasisInline emphasis, List<DocumentInline> output)
    {
        var children = ConvertInlines(emphasis);

        if (emphasis.DelimiterChar == '~' && emphasis.DelimiterCount >= 2)
        {
            output.Add(new StrikethroughInline(children));
            return;
        }

        if (emphasis.DelimiterCount >= 3)
        {
            output.Add(new StrongInline(new DocumentInline[] { new EmphasisInline(children) }));
            return;
        }

        if (emphasis.DelimiterCount >= 2)
            output.Add(new StrongInline(children));
        else
            output.Add(new EmphasisInline(children));
    }

    private static void AddImageInline(Markdig.Syntax.Inlines.LinkInline image, List<DocumentInline> output)
        => output.Add(new ImageInline(image.Url, GetPlainText(ConvertInlines(image)), image.Title));

    private static void AddAutolinkInline(AutolinkInline autolink, List<DocumentInline> output)
    {
        var destination = autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url;
        output.Add(new LinkInline(destination, new DocumentInline[] { new TextInline(autolink.Url) }));
    }

    private static void AddText(List<DocumentInline> output, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        output.Add(new TextInline(text));
    }

    private static string? NormalizeCodeBlockLanguage(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
            return null;

        var firstToken = info
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(firstToken) ? null : firstToken;
    }

    private static bool TryGetStandaloneImage(IReadOnlyList<DocumentInline> inlines, out ImageInline image)
    {
        image = null!;

        var meaningful = inlines
            .Where(inline => inline is not TextInline text || !string.IsNullOrWhiteSpace(text.Text))
            .ToArray();

        if (meaningful.Length != 1 || meaningful[0] is not ImageInline single)
            return false;

        image = single;
        return true;
    }

    private static string GetPlainText(IEnumerable<DocumentInline> inlines)
    {
        var parts = new List<string>();
        CollectPlainText(inlines, parts);
        return string.Concat(parts);
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
                case LineBreakInline:
                    parts.Add(Environment.NewLine);
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
                    parts.Add(image.AltText);
                    break;
                case FootnoteReferenceInline footnote:
                    parts.Add("[");
                    parts.Add(footnote.Order.ToString());
                    parts.Add("]");
                    break;
            }
        }
    }

    private static bool? TryGetTaskListState(ListItemBlock item)
    {
        var firstInline = item
            .OfType<Markdig.Syntax.ParagraphBlock>()
            .FirstOrDefault()
            ?.Inline
            ?.FirstChild;

        if (firstInline is null)
            return null;

        return TryIsTaskListInline(firstInline, out var isChecked) ? isChecked : null;
    }

    private static bool TryIsTaskListInline(Inline inline, out bool isChecked)
    {
        isChecked = false;

        if (inline.GetType().FullName != "Markdig.Extensions.TaskLists.TaskList")
            return false;

        var property = inline.GetType().GetProperty("Checked");
        if (property?.GetValue(inline) is bool value)
            isChecked = value;

        return true;
    }
}
