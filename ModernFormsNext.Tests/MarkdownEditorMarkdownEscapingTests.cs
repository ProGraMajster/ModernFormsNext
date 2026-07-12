using ModernFormsNext.Documents;
using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorMarkdownEscapingTests
{
    [Theory]
    [InlineData("label [with] brackets", "https://example.com/a(b)")]
    [InlineData("back\\slash", "relative path/file(1).png")]
    [InlineData("quotes \"and\" brackets", "https://example.com/[part]\\path")]
    [InlineData("Zażółć 😀", "https://example.com/zażółć?q=hello world")]
    public void GeneratedLinkRoundTripsThroughDocumentParser(string label, string url)
    {
        using var editor = new MarkdownEditor();
        editor.InsertLink(url, label);

        var document = new MarkdownParser().Parse(editor.Markdown);
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var link = Assert.IsType<LinkInline>(Assert.Single(paragraph.Inlines));
        Assert.Equal(label, Flatten(link.Inlines));
        Assert.Equal(url, link.Destination);
    }

    [Theory]
    [InlineData("Alt [text] \\", "Images/image (1).png", "Quoted \"title\"")]
    [InlineData("Unicode żółć 😀", "data:image/png;base64,AA==", null)]
    [InlineData("", "../obrazy/zażółć (1).png", "Tytuł's \"quoted\"")]
    [InlineData("Query 😀", "https://example.com/image.png?q=zażółć (1)#fragment", null)]
    public void GeneratedImageRoundTripsThroughDocumentParser(string alt, string source, string? title)
    {
        using var editor = new MarkdownEditor();
        editor.InsertImage(source, alt, title);

        var document = new MarkdownParser().Parse(editor.Markdown);
        var image = Assert.IsType<ImageBlock>(Assert.Single(document.Blocks));
        Assert.Equal(alt, image.AltText);
        Assert.Equal(source, image.Source);
        Assert.Equal(title, image.Title);
    }

    private static string Flatten(IEnumerable<DocumentInline> inlines)
        => string.Concat(inlines.Select(inline => inline switch
        {
            TextInline text => text.Text,
            CodeInline code => code.Text,
            StrongInline strong => Flatten(strong.Inlines),
            EmphasisInline emphasis => Flatten(emphasis.Inlines),
            StrikethroughInline strike => Flatten(strike.Inlines),
            LinkInline link => Flatten(link.Inlines),
            _ => string.Empty
        }));
}
