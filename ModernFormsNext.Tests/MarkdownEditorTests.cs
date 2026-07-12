using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection("Clipboard")]
public class MarkdownEditorTests
{
    [Fact]
    public void MarkdownAndTextStaySynchronizedAndRaiseOneChange()
    {
        using var editor = new MarkdownEditor();
        var markdownChanges = 0;
        var textChanges = 0;
        editor.MarkdownChanged += (_, _) => markdownChanges++;
        editor.TextChanged += (_, _) => textChanges++;

        editor.Markdown = "# One";

        Assert.Equal("# One", editor.Text);
        Assert.Equal(1, markdownChanges);
        Assert.Equal(1, textChanges);

        editor.Text = "# Two";
        Assert.Equal("# Two", editor.Markdown);
        Assert.Equal(2, markdownChanges);
        Assert.Equal(2, textChanges);
    }

    [Fact]
    public void ProgrammaticMarkdownResetsModifiedAndUndo()
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.Select(0, 4);
        editor.ToggleBold();
        Assert.True(editor.Modified);
        Assert.True(editor.CanUndo);

        editor.Markdown = "saved";

        Assert.False(editor.Modified);
        Assert.False(editor.CanUndo);
        Assert.Equal("saved", editor.Markdown);
    }

    [Fact]
    public void ModifiedCanBeResetAtCurrentHistoryPosition()
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        var changes = 0;
        editor.ModifiedChanged += (_, _) => changes++;
        editor.Select(0, 4);
        editor.ToggleBold();
        editor.Modified = false;
        editor.Modified = false;

        Assert.False(editor.Modified);
        Assert.Equal(2, changes);

        editor.Undo();
        Assert.True(editor.Modified);
        editor.Redo();
        Assert.False(editor.Modified);
    }

    [Fact]
    public void ReadOnlyBlocksEditingCommandsButAllowsSelection()
    {
        using var editor = new MarkdownEditor { Markdown = "text", ReadOnly = true };
        editor.Select(0, 4);

        editor.ToggleBold();
        editor.SelectedText = "other";
        editor.Cut();

        Assert.Equal("text", editor.Markdown);
        Assert.Equal("text", editor.SelectedText);
        Assert.False(editor.Modified);
    }

    [Fact]
    public void SelectionAndSelectedTextUseUtf16Indexes()
    {
        using var editor = new MarkdownEditor { Markdown = "A😀B" };

        editor.Select(1, 2);

        Assert.Equal(1, editor.SelectionStart);
        Assert.Equal(2, editor.SelectionLength);
        Assert.Equal("😀", editor.SelectedText);
    }

    [Fact]
    public void SelectedTextReplacementIsOneUndoOperation()
    {
        using var editor = new MarkdownEditor { Markdown = "one two" };
        editor.Select(4, 3);

        editor.SelectedText = "three";

        Assert.Equal("one three", editor.Markdown);
        editor.Undo();
        Assert.Equal("one two", editor.Markdown);
        Assert.Equal("two", editor.SelectedText);
        editor.Redo();
        Assert.Equal("one three", editor.Markdown);
    }

    [Fact]
    public void MaxLengthIsEnforcedBySharedEditingCore()
    {
        using var editor = new MarkdownEditor { MaxLength = 5 };

        editor.SelectedText = "123456789";

        Assert.Equal("12345", editor.Markdown);
    }

    [Fact]
    public void ContinuousTypingIsGroupedIntoOneUndoRecord()
    {
        using var editor = new MarkdownEditor();

        editor.EditorSurface.SelectedText = "a";
        editor.EditorSurface.SelectedText = "b";
        editor.EditorSurface.SelectedText = "c";

        Assert.Equal("abc", editor.Markdown);
        editor.Undo();
        Assert.Equal(string.Empty, editor.Markdown);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void EditHistoryHandlesTenThousandSmallDeltaRecords()
    {
        var history = new MarkdownEditorHistory();

        for (var index = 0; index < 10_000; index++)
        {
            history.Push(new MarkdownEditRecord(
                index,
                string.Empty,
                "x",
                new MarkdownSelection(index, 0),
                new MarkdownSelection(index + 1, 0),
                MarkdownEditKind.Typing),
                allowTypingMerge: false);
        }

        Assert.Equal(10_000, history.Position);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(9_999, history.TakeUndo().Start);
    }

    [Fact]
    public async Task CopyCutAndPasteUseSharedClipboard()
    {
        var clipboard = ClipboardTestService.GetOrRegister();

        await clipboard.ClearAsync();
        using var editor = new MarkdownEditor { Markdown = "copy me" };
        editor.Select(0, 4);

        editor.Copy();
        Assert.Equal("copy", await clipboard.GetTextAsync());

        editor.Cut();
        Assert.Equal(" me", editor.Markdown);
        editor.Select(editor.Markdown.Length, 0);
        var pasteText = await clipboard.GetTextAsync();
        editor.Paste();
        Assert.Equal(" me" + pasteText, editor.Markdown);
    }

}
