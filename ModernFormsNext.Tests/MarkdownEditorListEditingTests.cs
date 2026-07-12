using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorListEditingTests
{
    [Theory]
    [InlineData("- item", "- item\n- ")]
    [InlineData("7. item", "7. item\n8. ")]
    [InlineData("- [x] item", "- [x] item\n- [ ] ")]
    [InlineData("> item", "> item\n> ")]
    public void EnterContinuesMarkdownLinePrefix(string source, string expected)
    {
        using var editor = new MarkdownEditor { Markdown = source };
        editor.Select(source.Length, 0);

        PressKey(editor, Keys.Enter);

        Assert.Equal(expected, editor.Markdown);
        Assert.Equal(expected.Length, editor.SelectionStart);
        editor.Undo();
        Assert.Equal(source, editor.Markdown);
        editor.Redo();
        Assert.Equal(expected, editor.Markdown);
    }

    [Theory]
    [InlineData("- ")]
    [InlineData("1. ")]
    [InlineData("- [ ] ")]
    [InlineData("> ")]
    public void EnterOnEmptyMarkerExitsTheConstruct(string source)
    {
        using var editor = new MarkdownEditor { Markdown = source };
        editor.Select(source.Length, 0);

        PressKey(editor, Keys.Enter);

        Assert.Equal(string.Empty, editor.Markdown);
        Assert.Equal(0, editor.SelectionStart);
        editor.Undo();
        Assert.Equal(source, editor.Markdown);
    }

    [Fact]
    public void EnterPreservesCrLfAndSplitsAtCaret()
    {
        using var editor = new MarkdownEditor { Markdown = "- first\r\n- second" };
        editor.Select("- first\r\n- sec".Length, 0);

        PressKey(editor, Keys.Enter);

        Assert.Equal("- first\r\n- sec\r\n- ond", editor.Markdown);
        Assert.DoesNotContain("\n- sec\n", editor.Markdown);
    }

    [Fact]
    public void EnterReplacesSelectionWithOneContinuation()
    {
        using var editor = new MarkdownEditor { Markdown = "- selected" };
        editor.Select(2, 8);

        PressKey(editor, Keys.Enter);

        Assert.Equal("- \n- ", editor.Markdown);
        editor.Undo();
        Assert.Equal("- selected", editor.Markdown);
        Assert.Equal("selected", editor.SelectedText);
    }

    [Fact]
    public void TabAndShiftTabIndentAListAsSingleUndoOperations()
    {
        using var editor = new MarkdownEditor { Markdown = "- item" };
        editor.Select(2, 0);

        PressKey(editor, Keys.Tab);
        Assert.Equal("\t- item", editor.Markdown);
        Assert.Equal(3, editor.SelectionStart);

        PressKey(editor, Keys.Shift | Keys.Tab);
        Assert.Equal("- item", editor.Markdown);
        Assert.Equal(2, editor.SelectionStart);

        editor.Undo();
        Assert.Equal("\t- item", editor.Markdown);
        editor.Undo();
        Assert.Equal("- item", editor.Markdown);
    }

    [Fact]
    public void TabPreservesMultilineListSelection()
    {
        using var editor = new MarkdownEditor { Markdown = "- one\n- two" };
        editor.SelectAll();

        PressKey(editor, Keys.Tab);

        Assert.Equal("\t- one\n\t- two", editor.Markdown);
        Assert.Equal(editor.Markdown, editor.SelectedText);
        PressKey(editor, Keys.Shift | Keys.Tab);
        Assert.Equal("- one\n- two", editor.Markdown);
        Assert.Equal(editor.Markdown, editor.SelectedText);
    }

    [Fact]
    public void TabOutsideAListKeepsRichTextBoxAcceptsTabBehavior()
    {
        using var editor = new MarkdownEditor { Markdown = "plain" };
        editor.Select(2, 0);

        PressKey(editor, Keys.Tab);

        Assert.Equal("pl\tain", editor.Markdown);
    }

    [Theory]
    [InlineData("- item", 2, "item")]
    [InlineData("12. item", 4, "item")]
    [InlineData("- [x] item", 6, "item")]
    [InlineData("> item", 2, "item")]
    [InlineData("### item", 4, "item")]
    public void BackspaceAtContentBoundaryRemovesOnlyMarker(string source, int caret, string expected)
    {
        using var editor = new MarkdownEditor { Markdown = source };
        editor.Select(caret, 0);

        PressKey(editor, Keys.Back);

        Assert.Equal(expected, editor.Markdown);
        Assert.Equal(0, editor.SelectionStart);
        editor.Undo();
        Assert.Equal(source, editor.Markdown);
    }

    [Fact]
    public void BackspaceOutsideMarkerBoundaryUsesNormalDeletion()
    {
        using var editor = new MarkdownEditor { Markdown = "- item" };
        editor.Select(3, 0);

        PressKey(editor, Keys.Back);

        Assert.Equal("- tem", editor.Markdown);
    }

    [Fact]
    public void ReadOnlyBlocksMarkdownAwareEditing()
    {
        using var editor = new MarkdownEditor { Markdown = "- item", ReadOnly = true };
        editor.Select(editor.Markdown.Length, 0);

        PressKey(editor, Keys.Enter);
        PressKey(editor, Keys.Tab);
        editor.Select(2, 0);
        PressKey(editor, Keys.Back);

        Assert.Equal("- item", editor.Markdown);
        Assert.False(editor.CanUndo);
    }

    private static void PressKey(MarkdownEditor editor, Keys keys)
        => editor.EditorSurface.RaiseKeyDown(new KeyEventArgs(keys));
}
