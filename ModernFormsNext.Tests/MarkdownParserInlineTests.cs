using ModernFormsNext.Documents;
using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownParserInlineTests
{
    [Fact]
    public void AutoLinksCreateSemanticLinkInline()
    {
        var document = new MarkdownParser().Parse("Visit https://example.com.");
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var link = Assert.Single(DocumentTestHelpers.FlattenInlines<LinkInline>(paragraph.Inlines));

        Assert.Equal("https://example.com", link.Destination);
        Assert.Equal("https://example.com", new Document(new[] { new ParagraphBlock(link.Inlines) }).GetPlainText());
    }

    [Fact]
    public void EmailAutoLinksUseMailtoDestination()
    {
        var document = new MarkdownParser().Parse("Contact <user@example.com>.");
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var link = Assert.Single(DocumentTestHelpers.FlattenInlines<LinkInline>(paragraph.Inlines));

        Assert.Equal("mailto:user@example.com", link.Destination);
        Assert.Equal("user@example.com", new Document(new[] { new ParagraphBlock(link.Inlines) }).GetPlainText());
    }

    [Fact]
    public void InlineImageInsideTextRemainsInlineFallbackText()
    {
        var document = new MarkdownParser().Parse("Before ![Logo](Images/icon.png) after.");
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var image = Assert.Single(DocumentTestHelpers.FlattenInlines<ImageInline>(paragraph.Inlines));

        Assert.Equal("Images/icon.png", image.Source);
        Assert.Equal("Logo", image.AltText);
        Assert.Equal("Before Logo after.", document.GetPlainText());
    }

    [Fact]
    public void FootnotesCreateReferenceAndFootnoteGroup()
    {
        var document = new MarkdownParser().Parse("""
            Text with footnote[^1].

            [^1]: Footnote *content*.
            """);

        var paragraph = Assert.IsType<ParagraphBlock>(document.Blocks[0]);
        var reference = Assert.Single(DocumentTestHelpers.FlattenInlines<FootnoteReferenceInline>(paragraph.Inlines));
        var group = Assert.IsType<FootnoteGroupBlock>(document.Blocks.Last());

        Assert.Equal(1, reference.Order);
        Assert.Single(group.Footnotes);
        Assert.Equal(1, group.Footnotes[0].Order);
        Assert.Contains("Footnote content.", new Document(group.Footnotes[0].Blocks).GetPlainText());
    }

    [Fact]
    public void EscapesEntitiesAndSoftLineBreaksProduceReadableText()
    {
        var document = new MarkdownParser().Parse("Escaped \\*star\\*, entity &amp;, soft\nbreak.");

        Assert.Equal("Escaped *star*, entity &, soft break.", document.GetPlainText());
    }
}
