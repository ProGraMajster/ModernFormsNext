using ModernFormsNext.Documents;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentSyntaxHighlightingTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("cs")]
    public void CSharpHighlightsKeywordStringCommentNumberAndType(string language)
    {
        const string source = "public Button Build() { var value = 42; return \"ok\"; } // note";

        var spans = DocumentSyntaxHighlighterRegistry.Highlight(language, source);

        AssertSpan(source, spans, "public", DocumentSyntaxTokenKind.Keyword);
        AssertSpan(source, spans, "Button", DocumentSyntaxTokenKind.Type);
        AssertSpan(source, spans, "42", DocumentSyntaxTokenKind.Number);
        AssertSpan(source, spans, "\"ok\"", DocumentSyntaxTokenKind.String);
        AssertSpan(source, spans, "// note", DocumentSyntaxTokenKind.Comment);
        AssertValidSpans(source, spans);
    }

    [Fact]
    public void JsonHighlightsPropertiesStringsNumbersAndKeywords()
    {
        const string source = "{ \"name\": \"MFN\", \"count\": 2, \"ready\": true }";

        var spans = DocumentSyntaxHighlighterRegistry.Highlight("json", source);

        AssertSpan(source, spans, "\"name\"", DocumentSyntaxTokenKind.Property);
        AssertSpan(source, spans, "\"MFN\"", DocumentSyntaxTokenKind.String);
        AssertSpan(source, spans, "2", DocumentSyntaxTokenKind.Number);
        AssertSpan(source, spans, "true", DocumentSyntaxTokenKind.Keyword);
        AssertValidSpans(source, spans);
    }

    [Fact]
    public void XmlHighlightsTagAttributeStringAndComment()
    {
        const string source = "<!-- note --><Button Text=\"Save\" />";

        var spans = DocumentSyntaxHighlighterRegistry.Highlight("xml", source);

        AssertSpan(source, spans, "<!-- note -->", DocumentSyntaxTokenKind.Comment);
        AssertSpan(source, spans, "Button", DocumentSyntaxTokenKind.Type);
        AssertSpan(source, spans, "Text", DocumentSyntaxTokenKind.Property);
        AssertSpan(source, spans, "\"Save\"", DocumentSyntaxTokenKind.String);
        AssertValidSpans(source, spans);
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("shell")]
    [InlineData("powershell")]
    public void ShellFamiliesHighlightKeywordVariableStringAndComment(string language)
    {
        const string source = "if ($name) { return \"ok\" } # note";

        var spans = DocumentSyntaxHighlighterRegistry.Highlight(language, source);

        AssertSpan(source, spans, "if", DocumentSyntaxTokenKind.Keyword);
        AssertSpan(source, spans, "$name", DocumentSyntaxTokenKind.Property);
        AssertSpan(source, spans, "\"ok\"", DocumentSyntaxTokenKind.String);
        AssertSpan(source, spans, "# note", DocumentSyntaxTokenKind.Comment);
        AssertValidSpans(source, spans);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-language")]
    public void MissingOrUnknownLanguageFallsBackToPlainCode(string? language)
    {
        var spans = DocumentSyntaxHighlighterRegistry.Highlight(language, "keyword \"string\" 42");

        Assert.Empty(spans);
    }

    [Fact]
    public void UnicodeContentKeepsUtf16SpanBoundariesValid()
    {
        const string source = "var emoji = \"😀\"; // zażółć";

        var spans = DocumentSyntaxHighlighterRegistry.Highlight("csharp", source);

        AssertSpan(source, spans, "\"😀\"", DocumentSyntaxTokenKind.String);
        AssertSpan(source, spans, "// zażółć", DocumentSyntaxTokenKind.Comment);
        AssertValidSpans(source, spans);
    }

    [Fact]
    public void HighlightedLayoutAndSelectionPreserveOriginalSource()
    {
        const string source = "var emoji = \"😀\"; // note";
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse($"```csharp\n{source}\n```")
        };

        var code = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentCodeBlockLayoutElement>());
        viewer.SelectAll();

        Assert.Equal(source, code.Text);
        Assert.Equal(source, viewer.SelectedText);
    }

    [Fact]
    public void LayoutTranslatesSemanticTokensIntoRichTextStyles()
    {
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse("```csharp\npublic string Value = \"ok\";\n```")
        };
        viewer.DocumentStyle.CodeStyle.KeywordColor = SKColors.Red;
        viewer.DocumentStyle.CodeStyle.TypeColor = SKColors.Green;
        viewer.DocumentStyle.CodeStyle.StringColor = SKColors.Blue;

        var code = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentCodeBlockLayoutElement>());
        var colors = code.TextBlock.StyleRuns.Select(run => run.Style.TextColor).ToArray();

        Assert.Contains(SKColors.Red, colors);
        Assert.Contains(SKColors.Green, colors);
        Assert.Contains(SKColors.Blue, colors);
    }

    [Fact]
    public void LanguageHeaderIsOptionalAndDoesNotEnterSelectableText()
    {
        const string source = "var value = 1;";
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse($"```csharp\n{source}\n```")
        };

        var withoutHeader = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentCodeBlockLayoutElement>());
        Assert.Null(withoutHeader.Header);

        viewer.DocumentStyle.ShowCodeBlockLanguage = true;
        var withHeader = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentCodeBlockLayoutElement>());
        viewer.SelectAll();

        Assert.NotNull(withHeader.Header);
        Assert.Equal(2, withHeader.Header.TextBlock.Length);
        Assert.Equal(source, viewer.SelectedText);
    }

    [Fact]
    public void LanguageHeaderIsNotCreatedWithoutLanguageMetadata()
    {
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse("```\nplain code\n```")
        };
        viewer.DocumentStyle.ShowCodeBlockLanguage = true;

        var code = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentCodeBlockLayoutElement>());

        Assert.Null(code.Header);
    }

    [Fact]
    public void CodeStyleChangesBubbleToDocumentStyleVersion()
    {
        var style = new DocumentStyle();
        var version = style.Version;
        var changed = 0;
        style.Changed += (_, _) => changed++;

        style.CodeStyle.KeywordColor = SKColors.Red;

        Assert.Equal(version + 1, style.Version);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void DisabledViewerUsesDisabledCodeForegroundForAllTokens()
    {
        using var viewer = new DocumentViewer
        {
            Document = new MarkdownParser().Parse("```csharp\npublic string Value = \"ok\";\n```")
        };
        viewer.Enabled = false;

        var code = Assert.Single(viewer.GetDocumentLayout().Elements.OfType<DocumentCodeBlockLayoutElement>());

        Assert.All(code.TextBlock.StyleRuns, run => Assert.Equal(Theme.ForegroundDisabledColor, run.Style.TextColor));
    }

    private static void AssertSpan(
        string source,
        IReadOnlyList<DocumentSyntaxSpan> spans,
        string expectedText,
        DocumentSyntaxTokenKind expectedKind)
        => Assert.Contains(spans, span => span.Kind == expectedKind && source.Substring(span.Start, span.Length) == expectedText);

    private static void AssertValidSpans(string source, IReadOnlyList<DocumentSyntaxSpan> spans)
    {
        var previousEnd = 0;
        foreach (var span in spans)
        {
            Assert.InRange(span.Start, previousEnd, source.Length);
            Assert.InRange(span.Length, 1, source.Length - span.Start);
            Assert.InRange(span.End, span.Start + 1, source.Length);
            previousEnd = span.End;
        }
    }
}
