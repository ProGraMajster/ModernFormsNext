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
}
