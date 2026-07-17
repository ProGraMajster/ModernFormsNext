using System.Text;
using Topten.RichTextKit;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class TextBoxUnicodeEditingTests
{
    [Fact]
    public void LayoutIndexesRoundTripToUtf16AfterSupplementaryText()
    {
        const string text = "emoji 👋🏽 and composition: 你妇";
        var textBox = new TextBox { Width = 600, Height = 40, Text = text };
        var utf16Index = text.IndexOf('你');
        var expectedCodePointIndex = text[..utf16Index].EnumerateRunes().Count();

        var codePointIndex = textBox.document.GetLayoutCodePointIndex(utf16Index);

        Assert.Equal(expectedCodePointIndex, codePointIndex);
        Assert.Equal(utf16Index, textBox.document.GetUtf16IndexFromLayoutCodePointIndex(codePointIndex));
    }

    [Fact]
    public void HitTestingAfterEmojiReturnsUtf16Index()
    {
        const string text = "emoji 👋🏽 and composition: 你妇";
        var textBox = new TextBox { Width = 600, Height = 40, Text = text };
        var expectedUtf16Index = text.IndexOf('你');
        var codePointIndex = textBox.document.GetLayoutCodePointIndex(expectedUtf16Index);
        var block = textBox.document.GetTextBlock();
        var caret = block.GetCaretInfo(new CaretPosition(codePointIndex));

        var actualUtf16Index = textBox.document.GetUtf16IndexFromPosition(
            (int)MathF.Round(caret.CaretXCoord),
            (int)MathF.Round(caret.CaretRectangle.MidY));

        Assert.Equal(expectedUtf16Index, actualUtf16Index);
    }

    [Fact]
    public void SelectionAfterEmojiPaintsAndDeletesOnlyChineseCharacter()
    {
        const string text = "👋🏽你妇";
        var textBox = new TextBox { Width = 300, Height = 40, Text = text };
        var selectionStart = text.IndexOf('你');
        textBox.document.SetImeSelection(selectionStart, selectionStart + 1);

        var layoutSelection = textBox.document.GetTextSelection();

        Assert.Equal(2, layoutSelection.Start);
        Assert.Equal(3, layoutSelection.End);
        Assert.True(textBox.document.DeleteSelection());
        Assert.Equal("👋🏽妇", textBox.Text);
    }

    [Fact]
    public void ReverseSelectionAfterEmojiIsNormalizedForPaintingAndDeletion()
    {
        const string text = "👋🏽你妇";
        var textBox = new TextBox { Width = 300, Height = 40, Text = text };
        var selectionStart = text.IndexOf('你');
        textBox.document.SetImeSelection(text.Length, selectionStart);

        var layoutSelection = textBox.document.GetTextSelection();

        Assert.Equal(2, layoutSelection.Start);
        Assert.Equal(4, layoutSelection.End);
        Assert.True(textBox.document.DeleteSelection());
        Assert.Equal("👋🏽", textBox.Text);
    }

    [Fact]
    public void ArrowNavigationUsesCaretBoundariesInsteadOfUtf16AsListIndex()
    {
        const string text = "A👋🏽你妇";
        var textBox = new TextBox { Width = 300, Height = 40, Text = text };
        textBox.document.SetImeSelection(text.Length, text.Length);

        Assert.True(textBox.document.MoveCursor(ArrowDirection.Left, select: false, wholeWord: false, end: false));
        Assert.Equal(text.IndexOf('妇'), textBox.document.CursorIndex);

        Assert.True(textBox.document.DeleteText(forward: false, wholeWord: false));
        Assert.Equal("A👋🏽妇", textBox.Text);
    }

    [Fact]
    public void ControlEndConvertsTheLastLayoutIndexBackToUtf16()
    {
        const string text = "A👋🏽你妇";
        var textBox = new TextBox { Width = 300, Height = 40, Text = text };

        Assert.True(textBox.document.MoveCursor(
            ArrowDirection.Right,
            select: false,
            wholeWord: true,
            end: true));

        Assert.Equal(text.Length, textBox.document.CursorIndex);
    }

    [Theory]
    [InlineData("AńB", 2)]
    [InlineData("An\u0301B", 3)]
    public void BackspaceDeletesOnlyThePolishGrapheme(string text, int caret)
    {
        var textBox = new TextBox { Width = 200, Height = 40, Text = text };
        textBox.document.SetImeSelection(caret, caret);

        Assert.True(textBox.document.DeleteText(forward: false, wholeWord: false));

        Assert.Equal("AB", textBox.Text);
    }

    [Fact]
    public void PasswordLayoutPreservesOneMaskGlyphPerUtf16Unit()
    {
        var textBox = new TextBox
        {
            Width = 200,
            Height = 40,
            Text = "👋",
            PasswordCharacter = '*'
        };

        Assert.Equal(2, textBox.document.GetLayoutCodePointIndex(textBox.Text.Length));
        Assert.Equal(2, textBox.document.GetUtf16IndexFromLayoutCodePointIndex(2));
    }
}
