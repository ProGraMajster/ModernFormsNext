using System.Drawing;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class SkiaControlSurfaceLifecycleTests
{
    [Fact]
    public void DisposeCancelsEveryPointerDespiteThrowingOwnerAndAllowsBorrowedTreeReuse()
    {
        using var root = new Panel();
        var first = new Button { Bounds = new Rectangle(0, 0, 100, 40) };
        var second = new Button { Bounds = new Rectangle(120, 0, 100, 40) };
        root.Controls.AddRange(first, second);
        var surface = new SkiaControlSurface(root);
        surface.Resize(240, 80);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 10, 10);
        surface.ProcessPointer(2, ControlSurfacePointerAction.Down, 130, 10);
        var failure = new InvalidOperationException("First pointer cancellation failed.");
        EventHandler failingLeave = (_, _) => throw failure;
        first.MouseLeave += failingLeave;
        var secondCanceled = 0;
        second.MouseLeave += (_, _) => secondCanceled++;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(surface.Dispose));

        Assert.Equal(0, surface.ActivePointerCount);
        Assert.Equal(1, secondCanceled);
        Assert.False(first.Capture);
        Assert.False(second.Capture);
        Assert.Null(root.Parent);
        Assert.False(root.IsDisposed);
        first.MouseLeave -= failingLeave;
        using var replacement = new SkiaControlSurface(root);
        replacement.Resize(240, 80);
        var clicks = 0;
        first.Click += (_, _) => clicks++;
        replacement.ProcessPointer(3, ControlSurfacePointerAction.Down, 10, 10);
        replacement.ProcessPointer(3, ControlSurfacePointerAction.Up, 10, 10);
        Assert.Equal(1, clicks);
        surface.Dispose();
    }

    [Fact]
    public void DisposeRevokesCallbacksBeforeReentrantCancellation()
    {
        using var root = new Button();
        var surface = new SkiaControlSurface(root);
        surface.Resize(160, 40);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 10, 10);
        var leaveCount = 0;
        var lateInvalidations = 0;
        surface.Invalidated += (_, _) => lateInvalidations++;
        root.MouseLeave += (_, _) =>
        {
            leaveCount++;
            surface.Dispose();
            Assert.Throws<ObjectDisposedException>(() =>
                surface.ProcessPointer(2, ControlSurfacePointerAction.Down, 10, 10));
        };

        surface.Dispose();

        Assert.Equal(1, leaveCount);
        Assert.Equal(0, lateInvalidations);
        Assert.Null(root.Parent);
        Assert.False(root.IsDisposed);
    }

    [Fact]
    public void RetiredSurfaceCannotReadoptBorrowedRootFromDetachCallback()
    {
        using var root = new Panel();
        var surface = new SkiaControlSurface(root);
        var oldParent = root.Parent!;
        EventHandler reattach = (_, _) =>
        {
            if (root.Parent is null) oldParent.Controls.Add(root);
        };
        root.ParentChanged += reattach;

        Assert.Throws<ObjectDisposedException>(surface.Dispose);

        Assert.Null(root.Parent);
        Assert.Empty(oldParent.Controls);
        Assert.False(root.IsDisposed);
        root.ParentChanged -= reattach;
        using var replacement = new SkiaControlSurface(root);
        replacement.Resize(120, 80);
        Assert.Equal(new Size(120, 80), root.Size);
    }

    [Fact]
    public void ThrowingDetachStillFinishesCompositionAndDetachesBorrowedTree()
    {
        using var root = new TextBox();
        var surface = new SkiaControlSurface(root);
        surface.Resize(240, 40);
        root.Select();
        surface.SetComposingText("retained");
        Assert.Equal(0, Assert.IsType<ControlSurfaceTextInputState>(surface.GetTextInputState()).CompositionStart);
        var failure = new InvalidOperationException("Application detach callback failed.");
        EventHandler failingDetach = (_, _) =>
        {
            if (root.Parent is null) throw failure;
        };
        root.ParentChanged += failingDetach;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(surface.Dispose));

        Assert.Null(root.Parent);
        Assert.False(root.IsDisposed);
        root.ParentChanged -= failingDetach;
        using var replacement = new SkiaControlSurface(root);
        replacement.Resize(240, 40);
        root.Select();
        var state = Assert.IsType<ControlSurfaceTextInputState>(replacement.GetTextInputState());
        Assert.Equal("retained", state.Text);
        Assert.Equal(-1, state.CompositionStart);
        Assert.Equal(-1, state.CompositionEnd);
    }
}
