using System.IO;
using System.Text;
using ModernFormsNext;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public class RichTextBoxTests
{
    [Fact]
    public void DefaultsAreMultilineAndVerticallyScrolled()
    {
        var richTextBox = new RichTextBox();

        Assert.True(richTextBox.MultiLine);
        Assert.Equal(RichTextBoxScrollBars.Vertical, richTextBox.ScrollBars);
        Assert.Equal(ContentAlignment.TopLeft, richTextBox.TextAlign);
    }

    [Fact]
    public void SelectionFormattingCanBeAppliedAndRead()
    {
        var richTextBox = new RichTextBox { Text = "Read Write Admin" };
        var font = new Font("Segoe UI", 14, FontStyle.Bold | FontStyle.Italic);

        richTextBox.Select(5, 5);
        richTextBox.SelectionColor = SKColors.DodgerBlue;
        richTextBox.SelectionBackColor = SKColors.LightYellow;
        richTextBox.SelectionFont = font;

        Assert.Equal(SKColors.DodgerBlue, richTextBox.SelectionColor);
        Assert.Equal(SKColors.LightYellow, richTextBox.SelectionBackColor);
        Assert.Equal(font, richTextBox.SelectionFont);
    }

    [Fact]
    public void EmptySelectionFormattingAppliesToInsertedText()
    {
        var richTextBox = new RichTextBox { Text = "Hello" };

        richTextBox.Select(5, 0);
        richTextBox.SelectionColor = SKColors.Red;
        richTextBox.SelectedText = "!";

        richTextBox.Select(5, 1);

        Assert.Equal("Hello!", richTextBox.Text);
        Assert.Equal(SKColors.Red, richTextBox.SelectionColor);
    }

    [Fact]
    public void InsertingBeforeFormattedTextKeepsFormattingOnOriginalCharacters()
    {
        var richTextBox = new RichTextBox { Text = "abc" };

        richTextBox.Select(1, 1);
        richTextBox.SelectionColor = SKColors.Red;

        richTextBox.Select(0, 0);
        richTextBox.SelectedText = "X";

        richTextBox.Select(2, 1);

        Assert.Equal("Xabc", richTextBox.Text);
        Assert.Equal(SKColors.Red, richTextBox.SelectionColor);
    }

    [Fact]
    public void RemovingFormattedTextDoesNotMoveFormattingToAnotherCharacter()
    {
        var richTextBox = new RichTextBox { Text = "abc" };

        richTextBox.Select(1, 1);
        richTextBox.SelectionColor = SKColors.Red;
        richTextBox.SelectedText = string.Empty;

        richTextBox.Select(1, 1);

        Assert.Equal("ac", richTextBox.Text);
        Assert.Equal(SKColor.Empty, richTextBox.SelectionColor);
    }

    [Fact]
    public void PlainTextAssignmentClearsFormatting()
    {
        var richTextBox = new RichTextBox { Text = "abc" };

        richTextBox.Select(0, 1);
        richTextBox.SelectionColor = SKColors.Red;
        richTextBox.Text = "abc";
        richTextBox.Select(0, 1);

        Assert.Equal(SKColor.Empty, richTextBox.SelectionColor);
    }

    [Fact]
    public void FindHighlightsByDefault()
    {
        var richTextBox = new RichTextBox { Text = "Read write read" };

        var index = richTextBox.Find("write");

        Assert.Equal(5, index);
        Assert.Equal(5, richTextBox.SelectionStart);
        Assert.Equal(5, richTextBox.SelectionLength);
    }

    [Fact]
    public void FindNoHighlightLeavesSelectionAlone()
    {
        var richTextBox = new RichTextBox { Text = "Read write read" };

        richTextBox.Select(0, 4);
        var index = richTextBox.Find("write", RichTextBoxFinds.NoHighlight);

        Assert.Equal(5, index);
        Assert.Equal(0, richTextBox.SelectionStart);
        Assert.Equal(4, richTextBox.SelectionLength);
    }

    [Fact]
    public void FindCanMatchWholeWordsAndCase()
    {
        var richTextBox = new RichTextBox { Text = "Read Reader read" };

        Assert.Equal(0, richTextBox.Find("Read", RichTextBoxFinds.WholeWord | RichTextBoxFinds.MatchCase));
        Assert.Equal(12, richTextBox.Find("read", RichTextBoxFinds.WholeWord | RichTextBoxFinds.MatchCase));
        Assert.Equal(-1, richTextBox.Find("rea", RichTextBoxFinds.WholeWord));
    }

    [Fact]
    public void RtfRoundTripPreservesBasicFormatting()
    {
        var richTextBox = new RichTextBox { Text = "Red Plain" };
        var font = new Font("Segoe UI", 13, FontStyle.Bold | FontStyle.Underline);

        richTextBox.Select(0, 3);
        richTextBox.SelectionColor = SKColors.Red;
        richTextBox.SelectionBackColor = SKColors.LightYellow;
        richTextBox.SelectionFont = font;

        var copy = new RichTextBox { Rtf = richTextBox.Rtf };
        copy.Select(0, 3);

        Assert.Equal("Red Plain", copy.Text);
        Assert.Equal(SKColors.Red, copy.SelectionColor);
        Assert.Equal(SKColors.LightYellow, copy.SelectionBackColor);
        Assert.Equal(font, copy.SelectionFont);
    }

    [Fact]
    public void SelectedRtfReplacesOnlySelection()
    {
        var source = new RichTextBox { Text = "Bold" };
        source.Select(0, 4);
        source.SelectionFont = new Font("Segoe UI", 11, FontStyle.Bold);

        var target = new RichTextBox { Text = "one two three" };
        target.Select(4, 3);
        target.SelectedRtf = source.SelectedRtf;

        target.Select(4, 4);

        Assert.Equal("one Bold three", target.Text);
        Assert.NotNull(target.SelectionFont);
        Assert.True(target.SelectionFont!.Bold);
    }

    [Fact]
    public void SaveAndLoadPlainTextUseTextOnly()
    {
        var richTextBox = new RichTextBox { Text = "Hello plain text" };
        using var stream = new MemoryStream();

        richTextBox.SaveFile(stream, RichTextBoxStreamType.PlainText);
        stream.Position = 0;

        var copy = new RichTextBox();
        copy.LoadFile(stream, RichTextBoxStreamType.PlainText);

        Assert.Equal("Hello plain text", copy.Text);
    }

    [Fact]
    public void SaveAndLoadUnicodePlainTextUseUtf16()
    {
        var richTextBox = new RichTextBox { Text = "Zażółć" };
        using var stream = new MemoryStream();

        richTextBox.SaveFile(stream, RichTextBoxStreamType.UnicodePlainText);
        var bytes = stream.ToArray();

        Assert.Contains("Zażółć", Encoding.Unicode.GetString(bytes));
    }

    [Fact]
    public void SelectionChangedRaisesWhenSelectingRange()
    {
        var richTextBox = new RichTextBox { Text = "abc" };
        var count = 0;

        richTextBox.SelectionChanged += (_, _) => count++;
        richTextBox.Select(1, 1);

        Assert.Equal(1, count);
    }

    [Fact]
    public void ContentsResizedRaisesWhenTextChanges()
    {
        var richTextBox = new RichTextBox();
        var count = 0;

        richTextBox.ContentsResized += (_, _) => count++;
        richTextBox.Text = "Line 1\nLine 2";

        Assert.True(count > 0);
    }

    [Fact]
    public void ZoomFactorValidatesRange()
    {
        var richTextBox = new RichTextBox();

        richTextBox.ZoomFactor = 2f;

        Assert.Equal(2f, richTextBox.ZoomFactor);
        Assert.Throws<ArgumentOutOfRangeException>(() => richTextBox.ZoomFactor = 0f);
    }

    [Fact]
    public void EnterKeyDownInsertsSingleNewLine()
    {
        var richTextBox = new TestRichTextBox { Text = "ab" };

        richTextBox.Select(1, 0);
        richTextBox.PressKey(Keys.Enter);
        richTextBox.PressText("\r");

        Assert.Equal("a\nb", richTextBox.Text);
    }

    [Fact]
    public void TabKeyDownInsertsSingleTab()
    {
        var richTextBox = new TestRichTextBox { Text = "ab" };

        richTextBox.Select(1, 0);
        richTextBox.PressKey(Keys.Tab);
        richTextBox.PressText("\t");

        Assert.Equal("a\tb", richTextBox.Text);
    }

    [Fact]
    public void TabKeyPressInsertsTabWhenNoKeyDownArrives()
    {
        var richTextBox = new TestRichTextBox { Text = "ab" };

        richTextBox.Select(1, 0);
        richTextBox.PressText("\t");

        Assert.Equal("a\tb", richTextBox.Text);
    }

    [Fact]
    public void AcceptsTabFalseLeavesTabForFocusNavigation()
    {
        var richTextBox = new TestRichTextBox { Text = "ab", AcceptsTab = false };

        richTextBox.Select(1, 0);
        richTextBox.PressKey(Keys.Tab);
        richTextBox.PressText("\t");

        Assert.Equal("ab", richTextBox.Text);
    }

    [Fact]
    public void TrailingNewLineContributesToContentHeight()
    {
        var richTextBox = new RichTextBox { Width = 200, Height = 100 };
        var heights = new List<int>();

        richTextBox.ContentsResized += (_, e) => heights.Add(e.NewRectangle.Height);
        richTextBox.Text = "a";
        var singleLineHeight = heights.Last();

        richTextBox.Text = "a\n";
        var trailingLineBreakHeight = heights.Last();

        Assert.True(trailingLineBreakHeight > singleLineHeight);
    }

    [Fact]
    public void ControlMouseWheelChangesZoomFactor()
    {
        var richTextBox = new TestRichTextBox();

        richTextBox.MouseWheelDelta(1, control: true);

        Assert.True(richTextBox.ZoomFactor > 1f);

        var zoomed = richTextBox.ZoomFactor;
        richTextBox.MouseWheelDelta(-1, control: true);

        Assert.True(richTextBox.ZoomFactor < zoomed);
    }

    [Fact]
    public void MouseWheelWithoutControlDoesNotChangeZoomFactor()
    {
        var richTextBox = new TestRichTextBox();

        richTextBox.MouseWheelDelta(1, control: false);

        Assert.Equal(1f, richTextBox.ZoomFactor);
    }

    [Fact]
    public void AltGraphMouseWheelDoesNotChangeZoomFactor()
    {
        var richTextBox = new TestRichTextBox();

        richTextBox.MouseWheelDelta(1, control: true, altGraph: true);

        Assert.Equal(1f, richTextBox.ZoomFactor);
    }

    [Fact]
    public void EmptySelectionColorClearsExplicitForegroundColor()
    {
        var richTextBox = new RichTextBox { Text = "abc" };

        richTextBox.Select(0, 3);
        richTextBox.SelectionColor = SKColors.Red;
        richTextBox.SelectionColor = SKColor.Empty;

        Assert.Equal(SKColor.Empty, richTextBox.SelectionColor);
    }

    private sealed class TestRichTextBox : RichTextBox
    {
        public void MouseWheelDelta(int deltaY, bool control, bool altGraph = false)
        {
            var modifiers = control ? Keys.Control : Keys.None;
            if (altGraph)
                modifiers |= Keys.Alt | Keys.AltGraph;
            OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, new System.Drawing.Point(0, deltaY), keyData: modifiers));
        }

        public void PressKey(Keys key)
        {
            OnKeyDown(new KeyEventArgs(key));
        }

        public void PressText(string text)
        {
            OnKeyPress(new KeyPressEventArgs(text));
        }
    }
}
