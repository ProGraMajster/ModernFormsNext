using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class SkiaControlSurfaceTests
{
    [Fact]
    public void ResizeAndRenderUseBorrowedControlTree()
    {
        var root = new Control();
        using var adapter = new SkiaControlSurface(root);
        using var nativeSurface = SKSurface.Create(new SKImageInfo(320, 240));

        adapter.Resize(320, 240);
        adapter.Render(nativeSurface.Canvas);

        Assert.Equal(320, root.Width);
        Assert.Equal(240, root.Height);
        Assert.Equal(new System.Drawing.Size(320, 240), adapter.LogicalSize);
    }

    [Fact]
    public void CommittedUnicodeTextReachesSelectedTextBox()
    {
        var textBox = new TextBox { Width = 240, Height = 40 };
        var root = new Control { Width = 240, Height = 40 };
        root.Controls.Add(textBox);
        using var adapter = new SkiaControlSurface(root);
        adapter.Resize(240, 40);
        Assert.True(textBox.Visible);
        Assert.True(textBox.Enabled);
        Assert.True(textBox.CanSelect);
        Assert.True(textBox.Bounds.Contains(4, 4));
        adapter.ProcessPointer(ControlSurfacePointerAction.Down, 4, 4);
        Assert.Same(textBox, adapter.SelectedControl);

        adapter.CommitText("Zażółć 👋");

        Assert.Equal("Zażółć 👋", textBox.Text);
    }

    [Fact]
    public void DeleteBackwardRemovesACompleteEmojiTextElement()
    {
        var textBox = new TextBox { Width = 240, Height = 40 };
        var root = new Control { Width = 240, Height = 40 };
        root.Controls.Add(textBox);
        using var adapter = new SkiaControlSurface(root);
        adapter.Resize(240, 40);
        adapter.ProcessPointer(ControlSurfacePointerAction.Down, 4, 4);
        adapter.CommitText("A👋🏽");

        adapter.DeleteBackward();

        Assert.Equal("A", textBox.Text);
    }

    [Fact]
    public void CompositionIsReplacedAndCommittedWithoutDuplication()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            adapter.SetComposingText("za");
            adapter.SetComposingText("zaż");
            var composing = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
            Assert.Equal((0, 3), (composing.CompositionStart, composing.CompositionEnd));

            adapter.CommitText("zażółć 👋");

            Assert.Equal("zażółć 👋", textBox.Text);
            var committed = Assert.IsType<ControlSurfaceTextInputState>(adapter.GetTextInputState());
            Assert.Equal(-1, committed.CompositionStart);
            Assert.Equal(committed.Text.Length, committed.SelectionEnd);
        }
    }

    [Fact]
    public void DeleteSurroundingTextPreservesUnicodeTextElementBoundaries()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            adapter.CommitText("A👋🏽B");
            adapter.SetTextSelection("A👋🏽".Length, "A👋🏽".Length);

            adapter.DeleteSurroundingText(1, 1);

            Assert.Equal("A", textBox.Text);
        }
    }

    [Fact]
    public void DeleteSurroundingTextPreservesTheSelectedRange()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            adapter.CommitText("abcXYZdef");
            adapter.SetTextSelection(3, 6);

            adapter.DeleteSurroundingText(2, 2);

            Assert.Equal("aXYZf", textBox.Text);
            Assert.Equal(1, textBox.SelectionStart);
            Assert.Equal(4, textBox.SelectionEnd);
        }
    }

    [Fact]
    public void EnterAndArrowKeysUseTheFrameworkKeyboardPipeline()
    {
        var textBox = CreateSelectedTextBox(out var adapter);
        using (adapter)
        {
            textBox.MultiLine = true;
            adapter.CommitText("AB");
            adapter.ProcessKeyDown(Keys.Left);
            adapter.ProcessKeyDown(Keys.Enter);

            Assert.Equal("A\nB", textBox.Text);
        }
    }

    [Fact]
    public void CancelClearsPointerCaptureWithoutClicking()
    {
        var button = new Button { Width = 120, Height = 40 };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        using var adapter = new SkiaControlSurface(button);
        adapter.Resize(120, 40);

        adapter.ProcessPointer(ControlSurfacePointerAction.Down, 10, 10);
        adapter.ProcessPointer(ControlSurfacePointerAction.Cancel, 10, 10);

        Assert.False(button.Capture);
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void OperationsAfterDisposeAreRejectedButRootRemainsOwnedByCaller()
    {
        var root = new Control();
        var adapter = new SkiaControlSurface(root);
        adapter.Dispose();

        Assert.Throws<ObjectDisposedException>(() => adapter.Resize(1, 1));
        root.Text = "Still alive";
        Assert.Equal("Still alive", root.Text);
    }

    private static TextBox CreateSelectedTextBox(out SkiaControlSurface adapter)
    {
        var textBox = new TextBox { Width = 240, Height = 40 };
        var root = new Control { Width = 240, Height = 40 };
        root.Controls.Add(textBox);
        adapter = new SkiaControlSurface(root);
        adapter.Resize(240, 40);
        adapter.ProcessPointer(ControlSurfacePointerAction.Down, 4, 4);
        return textBox;
    }
}
