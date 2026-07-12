using Xunit;

namespace ModernFormsNext.Tests;

[Collection("Clipboard")]
public class TextEditingShortcutTests
{
    private const Keys AltGraph = Keys.Control | Keys.Alt | Keys.AltGraph;

    [Fact]
    public void CtrlASelectsAllButAltGraphADoesNot()
    {
        using var textBox = new TestRichTextBox { Text = "existing" };
        textBox.Select(textBox.Text.Length, 0);

        textBox.PressKey(AltGraph | Keys.A);

        Assert.Equal(textBox.Text.Length, textBox.SelectionStart);
        Assert.Equal(0, textBox.SelectionLength);

        textBox.PressText("ą", AltGraph);
        Assert.Equal("existingą", textBox.Text);

        textBox.PressKey(Keys.Control | Keys.A);
        Assert.Equal(0, textBox.SelectionStart);
        Assert.Equal(textBox.Text.Length, textBox.SelectionLength);
    }

    [Fact]
    public async Task AltGraphVDoesNotPasteButCtrlVDoes()
    {
        var clipboard = ClipboardTestService.GetOrRegister();
        await clipboard.SetTextAsync(" pasted");
        using var textBox = new TestRichTextBox { Text = "keep" };
        textBox.Select(textBox.Text.Length, 0);

        textBox.PressKey(AltGraph | Keys.V);
        Assert.Equal("keep", textBox.Text);

        textBox.PressKey(Keys.Control | Keys.V);
        Assert.Equal("keep pasted", textBox.Text);
    }

    [Fact]
    public async Task AltGraphXAndCDoNotInvokeClipboardCommands()
    {
        var clipboard = ClipboardTestService.GetOrRegister();
        await clipboard.SetTextAsync("unchanged");
        using var textBox = new TestRichTextBox { Text = "copy me" };
        textBox.Select(0, 4);

        textBox.PressKey(AltGraph | Keys.C);
        Assert.Equal("unchanged", await clipboard.GetTextAsync());
        Assert.Equal("copy me", textBox.Text);

        textBox.PressKey(AltGraph | Keys.X);
        Assert.Equal("unchanged", await clipboard.GetTextAsync());
        Assert.Equal("copy me", textBox.Text);

        textBox.PressText("ć", AltGraph);
        Assert.Equal("ć me", textBox.Text);
    }

    [Fact]
    public void RealControlAltIsNotMisclassifiedAsAltGraph()
    {
        using var textBox = new TestRichTextBox { Text = "select" };
        textBox.Select(textBox.Text.Length, 0);

        textBox.PressKey(Keys.Control | Keys.Alt | Keys.A);

        Assert.Equal(0, textBox.SelectionStart);
        Assert.Equal(textBox.Text.Length, textBox.SelectionLength);
    }

    private sealed class TestRichTextBox : RichTextBox
    {
        public void PressKey(Keys keyData) => OnKeyDown(new KeyEventArgs(keyData));

        public void PressText(string text, Keys modifiers = Keys.None)
            => OnKeyPress(new KeyPressEventArgs(text, modifiers));
    }
}
