using System.Text;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection("Clipboard")]
public class MarkdownEditorInternationalInputTests
{
    private const Keys AltGraph = Keys.Control | Keys.Alt | Keys.AltGraph;

    [Fact]
    public void AltGraphAInsertsTextWithoutSelectingOrReplacingDocument()
    {
        using var editor = new MarkdownEditor { Markdown = "existing" };
        editor.Select(editor.Markdown.Length, 0);

        PressKey(editor, AltGraph | Keys.A);
        Assert.Equal(0, editor.SelectionLength);

        PressText(editor, "ą", AltGraph);

        Assert.Equal("existingą", editor.Markdown);
        Assert.Equal(editor.Markdown.Length, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public async Task AltGraphVDoesNotPasteAndCtrlVStillPastes()
    {
        var clipboard = ClipboardTestService.GetOrRegister();
        await clipboard.SetTextAsync("paste");
        using var editor = new MarkdownEditor { Markdown = "text" };
        editor.Select(editor.Markdown.Length, 0);

        PressKey(editor, AltGraph | Keys.V);
        Assert.Equal("text", editor.Markdown);

        PressKey(editor, Keys.Control | Keys.V);
        Assert.Equal("textpaste", editor.Markdown);
    }

    [Fact]
    public void AltGraphZDoesNotUndoAndFollowingTextDoesNotRestoreOldSnapshot()
    {
        using var editor = new MarkdownEditor { Markdown = "# MarkdownEditor\n" };
        editor.Select(editor.Markdown.Length, 0);

        PressKey(editor, AltGraph | Keys.Z);
        PressText(editor, "ż", AltGraph);
        PressText(editor, "y");
        PressKey(editor, AltGraph | Keys.C);
        PressText(editor, "ć", AltGraph);

        Assert.Equal("# MarkdownEditor\nżyć", editor.Markdown);
        Assert.Equal(editor.Markdown.Length, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Theory]
    [InlineData("żółć")]
    [InlineData("zażółć gęślą jaźń")]
    public void UnicodeTextInputPreservesCompleteText(string input)
    {
        using var editor = new MarkdownEditor();

        PressTextByRune(editor, input);

        Assert.Equal(input, editor.Markdown);
        Assert.Equal(input.Length, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void SurrogatePairInputRemainsIntact()
    {
        using var editor = new MarkdownEditor();

        // WM_CHAR can deliver a supplementary character as two UTF-16 code units.
        PressText(editor, "\ud83d");
        PressText(editor, "\ude00");

        Assert.Equal("😀", editor.Markdown);
        Assert.Equal(2, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void CtrlUndoAndRedoRoundTripInternationalTypingGroup()
    {
        using var editor = new MarkdownEditor();
        PressTextByRune(editor, "zażółć");

        PressKey(editor, Keys.Control | Keys.Z);
        Assert.Equal(string.Empty, editor.Markdown);

        PressKey(editor, Keys.Control | Keys.Y);
        Assert.Equal("zażółć", editor.Markdown);
    }

    [Fact]
    public void HighlightingAndSplitPreviewNeverWriteStaleSourceBackToEditor()
    {
        using var editor = new MarkdownEditor
        {
            Markdown = "# MarkdownEditor\n",
            ViewMode = MarkdownEditorViewMode.Split,
            PreviewUpdateDelayMilliseconds = 60_000
        };
        editor.Select(editor.Markdown.Length, 0);

        PressKey(editor, AltGraph | Keys.Z);
        PressText(editor, "ż", AltGraph);
        PressText(editor, "y");
        PressKey(editor, AltGraph | Keys.C);
        PressText(editor, "ć", AltGraph);
        editor.FlushPreviewUpdate();

        Assert.Equal("# MarkdownEditor\nżyć", editor.Markdown);
        Assert.Equal(editor.Markdown, editor.PreviewViewer.Markdown);
    }

    [Fact]
    public async Task AltGraphCopyAndCutDoNotRunButTextInputStillReplacesSelection()
    {
        var clipboard = ClipboardTestService.GetOrRegister();
        await clipboard.SetTextAsync("unchanged");
        using var editor = new MarkdownEditor { Markdown = "copy me" };
        editor.Select(0, 4);

        PressKey(editor, AltGraph | Keys.C);
        PressKey(editor, AltGraph | Keys.X);

        Assert.Equal("copy me", editor.Markdown);
        Assert.Equal("unchanged", await clipboard.GetTextAsync());

        PressText(editor, "ź", AltGraph);
        Assert.Equal("ź me", editor.Markdown);
    }

    private static void PressKey(MarkdownEditor editor, Keys keyData)
        => editor.EditorSurface.RaiseKeyDown(new KeyEventArgs(keyData));

    private static void PressText(MarkdownEditor editor, string text, Keys modifiers = Keys.None)
        => editor.EditorSurface.RaiseKeyPress(new KeyPressEventArgs(text, modifiers));

    private static void PressTextByRune(MarkdownEditor editor, string text)
    {
        foreach (var rune in text.EnumerateRunes())
            PressText(editor, rune.ToString());
    }
}
