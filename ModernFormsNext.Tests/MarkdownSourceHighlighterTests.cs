using System.Diagnostics;
using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownSourceHighlighterTests
{
    [Fact]
    public void HighlightsSupportedSourceConstructsWithoutChangingSource()
    {
        const string source = "# Heading\n**bold** *italic* ~~strike~~ `code`\n> quote\n- [x] task\n[link](target) ![alt](image.png)\n---";
        var highlighter = new MarkdownSourceHighlighter();

        var spans = highlighter.Highlight(source);

        Assert.Contains(spans, span => span.Kind == MarkdownSourceSpanKind.HeadingMarker);
        Assert.Contains(spans, span => span.Kind == MarkdownSourceSpanKind.EmphasisMarker);
        Assert.Contains(spans, span => span.Kind == MarkdownSourceSpanKind.CodeMarker);
        Assert.Contains(spans, span => span.Kind == MarkdownSourceSpanKind.QuoteMarker);
        Assert.Contains(spans, span => span.Kind == MarkdownSourceSpanKind.ListMarker);
        Assert.Contains(spans, span => span.Kind == MarkdownSourceSpanKind.LinkText);
        Assert.Contains(spans, span => span.Kind == MarkdownSourceSpanKind.LinkTarget);
        Assert.Contains(spans, span => span.Kind == MarkdownSourceSpanKind.ImageMarker);
        Assert.Equal("# Heading\n**bold** *italic* ~~strike~~ `code`\n> quote\n- [x] task\n[link](target) ![alt](image.png)\n---", source);
    }

    [Fact]
    public void FencedCodeContentIsNotMisclassifiedAsMarkdown()
    {
        const string source = "```csharp\n# not a heading\n[not](a-link)\n```";
        var spans = new MarkdownSourceHighlighter().Highlight(source);

        Assert.Equal(2, spans.Count(span => span.Kind == MarkdownSourceSpanKind.CodeMarker));
        Assert.DoesNotContain(spans, span => span.Kind == MarkdownSourceSpanKind.HeadingMarker);
        Assert.DoesNotContain(spans, span => span.Kind == MarkdownSourceSpanKind.LinkText);
    }

    [Theory]
    [InlineData("**missing close")]
    [InlineData("[broken](")]
    [InlineData("![alt]")]
    [InlineData("😀 **ważne**")]
    public void MalformedAndUnicodeInputNeverProducesOutOfRangeSpans(string source)
    {
        var spans = new MarkdownSourceHighlighter().Highlight(source);

        Assert.All(spans, span =>
        {
            Assert.InRange(span.Start, 0, Math.Max(0, source.Length - 1));
            Assert.True(span.Length > 0);
            Assert.True(span.End <= source.Length);
        });
    }

    [Fact]
    public void TenThousandLinesHighlightWithoutObviousTimeExplosion()
    {
        var source = string.Concat(Enumerable.Repeat("- [ ] item with **bold** and [link](https://example.com)\n", 10_000));
        var highlighter = new MarkdownSourceHighlighter();
        var stopwatch = Stopwatch.StartNew();

        var spans = highlighter.Highlight(source);

        stopwatch.Stop();
        Assert.NotEmpty(spans);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Highlighting took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void CrLfAndSupplementaryCharactersProduceOrderedInRangeSpans()
    {
        const string source = "# emoji 😀\r\n- **ważne**\r\n`kod`";

        var spans = new MarkdownSourceHighlighter().Highlight(source)
            .OrderBy(span => span.Start)
            .ThenByDescending(span => span.Length)
            .ToArray();

        Assert.NotEmpty(spans);
        Assert.All(spans, span =>
        {
            Assert.InRange(span.Start, 0, source.Length - 1);
            Assert.InRange(span.End, span.Start + 1, source.Length);
        });
    }
}
