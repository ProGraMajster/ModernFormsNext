using System.Drawing;
using ModernFormsNext.Documents;
using Topten.RichTextKit;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentTextMappingTests
{
    [Fact]
    public void EveryTextElementMapsToItsLogicalTextRange()
    {
        var document = new MarkdownParser().Parse("# Heading\n\nParagraph with `code`.\n\n- item");
        var layout = DocumentTestHelpers.LayoutDocument(document, 260);

        foreach (var element in layout.Elements.OfType<DocumentTextLayoutElement>())
        {
            var mapped = layout.TextMap.Text.Substring(element.DocumentTextStart, element.DocumentTextLength);
            Assert.Equal(element.Text, mapped);
        }
    }

    [Fact]
    public void HardBreakUsesTheSameSingleNewlineInLayoutAndTextMap()
    {
        var document = new MarkdownParser().Parse("first  \nsecond");
        var layout = DocumentTestHelpers.LayoutDocument(document, 240);
        var element = Assert.Single(layout.Elements.OfType<DocumentTextLayoutElement>());

        Assert.Equal("first\nsecond", element.Text);
        Assert.Equal(element.Text, layout.TextMap.Text);
        Assert.Equal(element.Text.Length, element.DocumentTextLength);
    }

    [Fact]
    public void ImagesAndRulesDoNotCreateSelectableRanges()
    {
        var document = new Document(new DocumentBlock[]
        {
            new ParagraphBlock(new[] { new TextInline("Before") }),
            new ImageBlock("missing.png", "Alt text"),
            new HorizontalRuleBlock(),
            new ParagraphBlock(new[] { new TextInline("After") })
        });
        var layout = DocumentTestHelpers.LayoutDocument(document, 240);

        Assert.Equal("Before\n\nAfter", layout.TextMap.Text);
        Assert.DoesNotContain(layout.TextMap.Elements, element => element.Text == "Alt text");
    }

    [Fact]
    public void Utf16AndCodePointConversionsPreserveSurrogatePairs()
    {
        const string text = "A\U0001F600B";

        Assert.Equal(1, DocumentTextMap.CodePointToUtf16Index(text, 1));
        Assert.Equal(3, DocumentTextMap.CodePointToUtf16Index(text, 2));
        Assert.Equal(2, DocumentTextMap.Utf16ToCodePointIndex(text, 3));
    }

    [Fact]
    public void HitTestMapsWrappedTextPointToGlobalPosition()
    {
        var document = new MarkdownParser().Parse("A long paragraph that wraps across several lines in a narrow viewer.");
        var layout = DocumentTestHelpers.LayoutDocument(document, 90);
        var element = Assert.Single(layout.Elements.OfType<DocumentTextLayoutElement>());
        const int codePoint = 24;
        var caret = element.TextBlock.GetCaretInfo(new CaretPosition(codePoint)).CaretRectangle;
        var point = new Point(
            element.TextOrigin.X + (int)caret.Left,
            element.TextOrigin.Y + (int)(caret.Top + Math.Max(1, caret.Height / 2)));

        var position = layout.TextMap.HitTest(point);

        Assert.InRange(position, codePoint - 1, codePoint + 1);
    }

    [Fact]
    public void SelectionGeometryIsCalculatedPerTextElement()
    {
        var document = new MarkdownParser().Parse("First paragraph.\n\nSecond paragraph.");
        var layout = DocumentTestHelpers.LayoutDocument(document, 180);
        var elements = layout.Elements.OfType<DocumentTextLayoutElement>().ToArray();
        var start = layout.TextMap.Text.IndexOf("paragraph", StringComparison.Ordinal);
        var end = layout.TextMap.Text.LastIndexOf("paragraph", StringComparison.Ordinal) + "paragraph".Length;

        var firstSelection = layout.TextMap.GetSelection(elements[0], start, end - start, SkiaSharp.SKColors.Blue);
        var secondSelection = layout.TextMap.GetSelection(elements[1], start, end - start, SkiaSharp.SKColors.Blue);

        Assert.False(firstSelection.IsEmpty());
        Assert.False(secondSelection.IsEmpty());
    }
}
