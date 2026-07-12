using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownEditorKeyboardTests
{
    [Theory]
    [InlineData(Keys.B, "**text**")]
    [InlineData(Keys.I, "*text*")]
    public void InlineFormattingShortcutsUsePublicCommandSemantics(Keys key, string expected)
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.SelectAll();

        PressKey(editor, Keys.Control | key);

        Assert.Equal(expected, editor.Markdown);
        editor.Undo();
        Assert.Equal("text", editor.Markdown);
    }

    [Theory]
    [InlineData(Keys.D7, "1. text")]
    [InlineData(Keys.D8, "- text")]
    public void ListShortcutsTransformCurrentLine(Keys key, string expected)
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.Select(2, 0);

        PressKey(editor, Keys.Control | Keys.Shift | key);

        Assert.Equal(expected, editor.Markdown);
    }

    [Fact]
    public void InlineCodeAndStrikethroughShortcutsAreHandled()
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.SelectAll();
        PressKey(editor, Keys.Control | Keys.Oemtilde);
        Assert.Equal("`text`", editor.Markdown);

        editor.Markdown = "text";
        editor.SelectAll();
        PressKey(editor, Keys.Control | Keys.Shift | Keys.X);
        Assert.Equal("~~text~~", editor.Markdown);
    }

    [Theory]
    [InlineData(Keys.B)]
    [InlineData(Keys.I)]
    [InlineData(Keys.X)]
    [InlineData(Keys.D7)]
    [InlineData(Keys.D8)]
    [InlineData(Keys.Oemtilde)]
    public void AltGraphNeverRunsFormattingShortcuts(Keys key)
    {
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.SelectAll();

        PressKey(editor, Keys.Control | Keys.Alt | Keys.AltGraph | key);

        Assert.Equal("text", editor.Markdown);
    }

    private static void PressKey(MarkdownEditor editor, Keys keys)
        => editor.EditorSurface.RaiseKeyDown(new KeyEventArgs(keys));
}
