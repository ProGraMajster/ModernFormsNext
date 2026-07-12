using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorToolbarTests
{
    [Fact]
    public void EveryCommandItemHasTooltipText()
    {
        using var editor = new MarkdownEditor();

        var commandItems = editor.CommandToolbar.Items.Where(item => item is not MenuSeparatorItem).ToArray();

        Assert.NotEmpty(commandItems);
        Assert.All(commandItems, item => Assert.False(string.IsNullOrWhiteSpace(item.ToolTipText)));
    }

    [Fact]
    public void ReadOnlyDisablesEditingUndoAndRedoItems()
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.SelectAll();
        editor.ToggleBold();
        editor.ReadOnly = true;

        Assert.All(
            editor.CommandToolbar.Items.Where(item => item is not MenuSeparatorItem),
            item => Assert.False(item.Enabled));
    }

    [Fact]
    public void UndoAndRedoEnabledStateTracksHistoryPosition()
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        var undo = Find(editor, "Undo");
        var redo = Find(editor, "Redo");
        Assert.False(undo.Enabled);
        Assert.False(redo.Enabled);

        editor.SelectAll();
        editor.ToggleBold();
        Assert.True(undo.Enabled);
        Assert.False(redo.Enabled);

        editor.Undo();
        Assert.False(undo.Enabled);
        Assert.True(redo.Enabled);
    }

    [Fact]
    public void ToolbarButtonUsesTheSamePublicFormattingCommand()
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.SelectAll();

        Find(editor, "Bold").OnClick(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, System.Drawing.Point.Empty));

        Assert.Equal("**text**", editor.Markdown);
        editor.Undo();
        Assert.Equal("text", editor.Markdown);
    }

    [Fact]
    public void CaretUpdatesConservativeActiveFormattingStates()
    {
        using var editor = new MarkdownEditor { Markdown = "**bold**\n- item" };
        editor.Select(4, 0);
        Assert.True(Find(editor, "Bold").Checked);

        editor.Select(editor.Markdown.Length, 0);
        Assert.False(Find(editor, "Bold").Checked);
        Assert.True(Find(editor, "Unordered list").Checked);
    }

    private static MenuItem Find(MarkdownEditor editor, string tooltipPrefix)
        => editor.CommandToolbar.Items.Single(item => item.ToolTipText.StartsWith(tooltipPrefix, StringComparison.Ordinal));
}
