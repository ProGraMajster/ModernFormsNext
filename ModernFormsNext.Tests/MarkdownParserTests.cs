using ModernFormsNext.Documents;
using System.Drawing;
using System.Threading;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownParserTests
{
    [Fact]
    public void NullAndEmptyMarkdownReturnEmptyDocument()
    {
        var parser = new MarkdownParser();

        Assert.Empty(parser.Parse(null).Blocks);
        Assert.Empty(parser.Parse(string.Empty).Blocks);
    }

    [Fact]
    public void ParsesHeadingsParagraphFormattingAndLinks()
    {
        var document = new MarkdownParser().Parse("""
            # Heading 1

            Normal paragraph with **bold**, *italic*, ***bold italic***, ~~strike~~, `code`, and [a link](https://example.com).
            """);

        var heading = Assert.IsType<HeadingBlock>(document.Blocks[0]);
        Assert.Equal(1, heading.Level);
        Assert.Equal("Heading 1", Assert.IsType<TextInline>(heading.Inlines[0]).Text);

        var paragraph = Assert.IsType<ParagraphBlock>(document.Blocks[1]);

        Assert.Contains(paragraph.Inlines, inline => ContainsInline<StrongInline>(inline));
        Assert.Contains(paragraph.Inlines, inline => ContainsInline<EmphasisInline>(inline));
        Assert.Contains(paragraph.Inlines, inline => ContainsInline<StrikethroughInline>(inline));
        Assert.Contains(paragraph.Inlines, inline => ContainsInline<CodeInline>(inline));

        var link = FindInline<LinkInline>(paragraph.Inlines);
        Assert.NotNull(link);
        Assert.Equal("https://example.com", link!.Destination);
        Assert.Equal("a link", Assert.IsType<TextInline>(link.Inlines[0]).Text);
    }

    [Fact]
    public void ParsesQuotesHorizontalRulesAndFencedCodeBlocks()
    {
        var document = new MarkdownParser().Parse("""
            > Quoted text

            ---

            ```csharp
            var button = new Button();
            ```
            """);

        var quote = Assert.IsType<QuoteBlock>(document.Blocks[0]);
        Assert.IsType<ParagraphBlock>(quote.Blocks[0]);

        Assert.IsType<HorizontalRuleBlock>(document.Blocks[1]);

        var code = Assert.IsType<CodeBlock>(document.Blocks[2]);
        Assert.Equal("csharp", code.Language);
        Assert.Contains("new Button", code.Text);
    }

    [Fact]
    public void ParsesOrderedUnorderedNestedAndTaskLists()
    {
        var document = new MarkdownParser().Parse("""
            - [x] completed
            - [ ] pending
              - nested

            3. third
            4. fourth
            """);

        var unordered = Assert.IsType<ListBlock>(document.Blocks[0]);
        Assert.False(unordered.Ordered);
        Assert.True(unordered.Items[0].IsChecked);
        Assert.False(unordered.Items[1].IsChecked);

        var nested = Assert.IsType<ListBlock>(unordered.Items[1].Blocks[1]);
        Assert.False(nested.Ordered);

        var ordered = Assert.IsType<ListBlock>(document.Blocks[1]);
        Assert.True(ordered.Ordered);
        Assert.Equal(3, ordered.StartNumber);
        Assert.Equal(2, ordered.Items.Count);
    }

    [Fact]
    public void UnorderedListMarkersUseTypographicBulletsByDepth()
    {
        var document = new MarkdownParser().Parse("""
            - first level
              - second level
                - third level
                  - fourth level
            """);

        var layout = LayoutDocument(document, width: 400);
        var textElements = layout.Elements
            .OfType<DocumentTextLayoutElement>()
            .Select(element => element.Text)
            .ToArray();

        Assert.Contains("\u2022", textElements);
        Assert.Contains("first level", textElements);
        Assert.Contains("\u25e6", textElements);
        Assert.Contains("second level", textElements);
        Assert.Contains("\u25aa", textElements);
        Assert.Contains("third level", textElements);
    }

    [Fact]
    public void OrderedStartNumberAndNestedListCombinationsArePreservedInLayout()
    {
        var document = new MarkdownParser().Parse("""
            3. third
            4. fourth
               - nested unordered
                 1. nested ordered
            """);

        var ordered = Assert.IsType<ListBlock>(document.Blocks[0]);
        Assert.True(ordered.Ordered);
        Assert.Equal(3, ordered.StartNumber);

        var textElements = LayoutDocument(document, width: 400).Elements
            .OfType<DocumentTextLayoutElement>()
            .Select(element => element.Text)
            .ToArray();

        Assert.Contains("3.", textElements);
        Assert.Contains("third", textElements);
        Assert.Contains("4.", textElements);
        Assert.Contains("fourth", textElements);
        Assert.Contains("\u25e6", textElements);
        Assert.Contains("nested unordered", textElements);
        Assert.Contains("1.", textElements);
        Assert.Contains("nested ordered", textElements);
    }

    [Fact]
    public void TaskListMarkersAreNotReplacedByBullets()
    {
        var document = new MarkdownParser().Parse("""
            - [x] Native Markdown rendering
            - [ ] Markdown editor
            """);

        var layout = LayoutDocument(document, width: 400);
        var layoutText = GetLayoutText(layout);

        Assert.Equal(2, layout.Elements.OfType<DocumentTaskCheckBoxLayoutElement>().Count());
        Assert.Contains("Native Markdown rendering", layoutText);
        Assert.Contains("Markdown editor", layoutText);
        Assert.DoesNotContain("\u2022 [x]", layoutText);
        Assert.DoesNotContain("\u2022 [ ]", layoutText);
    }

    [Theory]
    [InlineData("csharp", "csharp")]
    [InlineData("cs", "cs")]
    [InlineData("json", "json")]
    public void FencedCodeBlockLanguageIsPreserved(string language, string expected)
    {
        var markdown = "```" + language + "\nvalue\n```";
        var document = new MarkdownParser().Parse(markdown);

        var code = Assert.IsType<CodeBlock>(Assert.Single(document.Blocks));
        Assert.Equal(expected, code.Language);
    }

    [Fact]
    public void FencedCodeBlockWithoutLanguageHasNullLanguage()
    {
        var document = new MarkdownParser().Parse("""
            ```
            value
            ```
            """);

        var code = Assert.IsType<CodeBlock>(Assert.Single(document.Blocks));
        Assert.Null(code.Language);
    }

    [Fact]
    public void FencedCodeBlockLanguageIsNormalizedFromInfoWhitespace()
    {
        var document = new MarkdownParser().Parse("""
            ```   csharp   metadata
            value
            ```
            """);

        var code = Assert.IsType<CodeBlock>(Assert.Single(document.Blocks));
        Assert.Equal("csharp", code.Language);
    }

    [Fact]
    public void StandaloneMarkdownImageCreatesSemanticImageBlock()
    {
        var document = new MarkdownParser().Parse("""
            ![ModernFormsNext](https://example.com/logo.png "Logo")
            """);

        var image = Assert.IsType<ImageBlock>(Assert.Single(document.Blocks));

        Assert.Equal("https://example.com/logo.png", image.Source);
        Assert.Equal("ModernFormsNext", image.AltText);
        Assert.Equal("Logo", image.Title);
    }

    [Fact]
    public void StandaloneMarkdownImageWithoutAltTextUsesEmptyAltText()
    {
        var document = new MarkdownParser().Parse("""
            ![](https://example.com/logo.png)
            """);

        var image = Assert.IsType<ImageBlock>(Assert.Single(document.Blocks));

        Assert.Equal("https://example.com/logo.png", image.Source);
        Assert.Equal(string.Empty, image.AltText);
        Assert.Null(image.Title);
    }

    [Fact]
    public async Task DuplicateImageSourceStartsOneLoadRequest()
    {
        var loads = 0;
        using var cache = new DocumentImageCache(
            (source, cancellationToken) =>
            {
                loads++;
                return Task.FromResult<SKBitmap?>(new SKBitmap(4, 4));
            },
            action => action(),
            () => { });

        cache.SetDocument(new Document(new DocumentBlock[]
        {
            new ParagraphBlock(new DocumentInline[]
            {
                new ImageInline("same-source.png", "first"),
                new ImageInline("same-source.png", "second")
            })
        }));

        await WaitForAsync(() => cache.GetResource("same-source.png")?.State == DocumentImageResourceState.Loaded);

        Assert.Equal(1, loads);
    }

    [Fact]
    public void FailedImageLoadProducesPlaceholderLayout()
    {
        using var viewer = new DocumentViewer();
        viewer.ImageCache.SetFailedForTesting("missing.png");

        var document = new Document(new DocumentBlock[]
        {
            new ImageBlock("missing.png", "Missing image")
        });

        var imageElement = Assert.Single(LayoutDocument(document, viewer, width: 180).Elements.OfType<DocumentImagePlaceholderLayoutElement>());

        Assert.True(imageElement.Failed);
        Assert.Equal("Missing image", imageElement.FallbackText);
        Assert.True(imageElement.Bounds.Width > 0);
        Assert.True(imageElement.Bounds.Height > 0);
    }

    [Fact]
    public void ImageWiderThanAvailableWidthIsScaledWithAspectRatio()
    {
        using var viewer = new DocumentViewer();
        viewer.ImageCache.SetLoadedForTesting("wide.png", new SKBitmap(200, 100));

        var document = new Document(new DocumentBlock[]
        {
            new ImageBlock("wide.png", "Wide")
        });

        var imageElement = Assert.Single(LayoutDocument(document, viewer, width: 50).Elements.OfType<DocumentLoadedImageLayoutElement>());

        Assert.Equal(50, imageElement.Bounds.Width);
        Assert.Equal(25, imageElement.Bounds.Height);
    }

    [Fact]
    public async Task DisposingImageCacheDuringPendingLoadSuppressesCallback()
    {
        var callbacks = 0;
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource<SKBitmap?>();

        using var cache = new DocumentImageCache(
            async (source, cancellationToken) =>
            {
                started.SetResult();
                return await release.Task;
            },
            action => action(),
            () => callbacks++);

        cache.SetDocument(new Document(new DocumentBlock[]
        {
            new ParagraphBlock(new DocumentInline[] { new ImageInline("pending.png", "Pending") })
        }));

        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cache.Dispose();
        release.SetResult(new SKBitmap(2, 2));
        await Task.Delay(50);

        Assert.Equal(0, callbacks);
    }

    [Fact]
    public void HtmlInlineIsPreservedAsTextAndNotInterpreted()
    {
        var document = new MarkdownParser().Parse("Text <strong>not bold</strong>.");
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var text = GetPlainText(paragraph.Inlines);

        Assert.Contains("<strong>", text);
        Assert.Contains("</strong>", text);
        Assert.DoesNotContain(paragraph.Inlines, inline => ContainsInline<StrongInline>(inline));
    }

    [Fact]
    public void HtmlBlockIsPreservedAsTextAndNotExecuted()
    {
        var document = new MarkdownParser().Parse("""
            <script>alert('x')</script>
            """);

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var text = GetPlainText(paragraph.Inlines);

        Assert.Contains("<script>", text);
        Assert.Contains("alert", text);
    }

    [Fact]
    public void MarkdownViewerTreatsNullMarkdownAsEmptyDocument()
    {
        var viewer = new MarkdownViewer
        {
            Markdown = "# Title"
        };

        viewer.Markdown = null!;

        Assert.Equal(string.Empty, viewer.Markdown);
        Assert.Empty(viewer.Document.Blocks);
    }

    private static string GetLayoutText(DocumentLayout layout)
        => string.Join(Environment.NewLine, layout.Elements.Select(element => element switch
        {
            DocumentTextLayoutElement text => text.Text,
            DocumentImagePlaceholderLayoutElement image => image.FallbackText,
            _ => string.Empty
        }));

    private static string GetPlainText(IEnumerable<DocumentInline> inlines)
    {
        var parts = new List<string>();
        CollectPlainText(inlines, parts);
        return string.Concat(parts);
    }

    private static DocumentLayout LayoutDocument(Document document, int width)
    {
        using var viewer = new DocumentViewer();
        return LayoutDocument(document, viewer, width);
    }

    private static DocumentLayout LayoutDocument(Document document, DocumentViewer viewer, int width)
        => DocumentLayoutEngine.Layout(
            viewer,
            document,
            viewer.DocumentStyle,
            new Rectangle(0, 0, width, 1000),
            null,
            null);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }
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
            }
        }
    }

    private static bool ContainsInline<T>(DocumentInline inline)
        where T : DocumentInline
    {
        if (inline is T)
            return true;

        return inline switch
        {
            StrongInline strong => strong.Inlines.Any(ContainsInline<T>),
            EmphasisInline emphasis => emphasis.Inlines.Any(ContainsInline<T>),
            StrikethroughInline strike => strike.Inlines.Any(ContainsInline<T>),
            LinkInline link => link.Inlines.Any(ContainsInline<T>),
            ImageInline => typeof(T) == typeof(ImageInline),
            _ => false
        };
    }

    private static T? FindInline<T>(IEnumerable<DocumentInline> inlines)
        where T : DocumentInline
    {
        foreach (var inline in inlines)
        {
            if (inline is T match)
                return match;

            var nested = inline switch
            {
                StrongInline strong => FindInline<T>(strong.Inlines),
                EmphasisInline emphasis => FindInline<T>(emphasis.Inlines),
                StrikethroughInline strike => FindInline<T>(strike.Inlines),
                LinkInline link => FindInline<T>(link.Inlines),
                ImageInline image when image is T imageMatch => imageMatch,
                _ => null
            };

            if (nested is not null)
                return nested;
        }

        return null;
    }
}
