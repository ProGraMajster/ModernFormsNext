using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorCommandTests
{
    [Theory]
    [InlineData("**", "**")]
    [InlineData("*", "*")]
    [InlineData("~~", "~~")]
    [InlineData("`", "`")]
    public void InlineCommandsWrapAndUnwrapSelection(string opening, string closing)
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.Select(0, 4);

        RunInlineCommand(editor, opening);
        Assert.Equal(opening + "text" + closing, editor.Markdown);
        Assert.Equal("text", editor.SelectedText);

        RunInlineCommand(editor, opening);
        Assert.Equal("text", editor.Markdown);
    }

    [Fact]
    public void EmptyBoldSelectionPlacesCaretBetweenMarkers()
    {
        using var editor = new MarkdownEditor();

        editor.ToggleBold();

        Assert.Equal("****", editor.Markdown);
        Assert.Equal(2, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void HeadingQuoteAndListsTransformCompleteSelectedLines()
    {
        using var editor = new MarkdownEditor { Markdown = "one\ntwo" };
        editor.Select(1, 5);
        editor.InsertHeading(2);
        Assert.Equal("## one\n## two", editor.Markdown);

        editor.SelectAll();
        editor.ToggleBlockQuote();
        Assert.Equal("> ## one\n> ## two", editor.Markdown);

        editor.Markdown = "one\ntwo";
        editor.SelectAll();
        editor.ToggleUnorderedList();
        Assert.Equal("- one\n- two", editor.Markdown);

        editor.SelectAll();
        editor.ToggleUnorderedList();
        Assert.Equal("one\ntwo", editor.Markdown);
    }

    [Fact]
    public void OrderedAndTaskListsReplaceExistingListMarkersWithoutStacking()
    {
        using var editor = new MarkdownEditor { Markdown = "- one\n* two" };
        editor.SelectAll();

        editor.ToggleOrderedList();
        Assert.Equal("1. one\n2. two", editor.Markdown);

        editor.SelectAll();
        editor.ToggleTaskList();
        Assert.Equal("- [ ] one\n- [ ] two", editor.Markdown);

        editor.SelectAll();
        editor.ToggleTaskList();
        Assert.Equal("one\ntwo", editor.Markdown);
    }

    [Fact]
    public void LineCommandsPreserveCrLf()
    {
        using var editor = new MarkdownEditor { Markdown = "one\r\ntwo\r\nthree" };
        editor.Select(1, 6);

        editor.ToggleUnorderedList();

        Assert.Equal("- one\r\n- two\r\nthree", editor.Markdown);
        Assert.DoesNotContain("\n- two\n", editor.Markdown);
    }

    [Fact]
    public void IndentAndOutdentRoundTripMultipleLines()
    {
        using var editor = new MarkdownEditor { Markdown = "one\n  two" };
        editor.SelectAll();

        editor.Indent();
        Assert.Equal("\tone\n\t  two", editor.Markdown);
        editor.Outdent();
        Assert.Equal("one\n  two", editor.Markdown);
    }

    [Fact]
    public void CodeBlockRoundTripsAndIsOneUndoOperation()
    {
        using var editor = new MarkdownEditor { Markdown = "line 1\nline 2" };
        editor.SelectAll();

        editor.ToggleCodeBlock();
        Assert.Equal("```\nline 1\nline 2\n```", editor.Markdown);
        editor.Undo();
        Assert.Equal("line 1\nline 2", editor.Markdown);
        editor.Redo();
        Assert.Equal("```\nline 1\nline 2\n```", editor.Markdown);

        editor.ToggleCodeBlock();
        Assert.Equal("line 1\nline 2", editor.Markdown);
    }

    [Fact]
    public void LinkImageAndHorizontalRuleInsertPredictably()
    {
        using var editor = new MarkdownEditor { Markdown = "label" };
        editor.SelectAll();
        editor.InsertLink("https://example.com");
        Assert.Equal("[label](https://example.com)", editor.Markdown);
        Assert.Equal("label", editor.SelectedText);

        editor.Markdown = "alt";
        editor.SelectAll();
        editor.InsertImage("image.png");
        Assert.Equal("![alt](image.png)", editor.Markdown);

        editor.Markdown = "beforeafter";
        editor.Select(6, 0);
        editor.InsertHorizontalRule();
        Assert.Equal("before\n---\nafter", editor.Markdown);
    }

    [Fact]
    public void EmptyDocumentAndUnicodeCommandsRemainValidUtf16()
    {
        using var editor = new MarkdownEditor();
        editor.InsertHeading(1);
        Assert.Equal("# ", editor.Markdown);

        editor.Markdown = "😀";
        editor.Select(0, 2);
        editor.ToggleBold();
        Assert.Equal("**😀**", editor.Markdown);
        Assert.Equal("😀", editor.SelectedText);
    }

    private static void RunInlineCommand(MarkdownEditor editor, string marker)
    {
        switch (marker)
        {
            case "**": editor.ToggleBold(); break;
            case "*": editor.ToggleItalic(); break;
            case "~~": editor.ToggleStrikethrough(); break;
            case "`": editor.ToggleInlineCode(); break;
        }
    }
}
