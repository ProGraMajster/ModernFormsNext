using System.Drawing;
using System.Reflection;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class TouchScrollGestureTests
{
    [Theory]
    [InlineData(1d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void WindowPointerDragAppliesLogicalScrollDistanceOnce(double scale)
    {
        using var window = new ScaledWindow(scale);
        var scroll = new ScrollableControl { AutoScroll = true, Width = 200, Height = 120 };
        scroll.Controls.Add(new Button { Left = 10, Top = 10, Width = 150, Height = 80 });
        scroll.Controls.Add(new Control { Left = 10, Top = 1000, Width = 40, Height = 20 });
        window.Controls.Add(scroll);
        scroll.PerformLayout();
        using var surface = new SkiaControlSurface(window);

        int x = (int)(30 * scale), down = (int)(60 * scale), end = (int)(30 * scale);
        surface.ProcessPointer(7, ControlSurfacePointerAction.Down, x, down);
        surface.ProcessPointer(7, ControlSurfacePointerAction.Move, x, end);
        surface.ProcessPointer(7, ControlSurfacePointerAction.Up, x, end);

        Assert.Equal(30, scroll.VerticalScrollProperties.Value);
        Assert.Equal(0, surface.ActivePointerCount);
        Assert.False(scroll.Capture);
    }

    [Fact]
    public void ReplacementDownPreservesNewerPointerStartedByCancellation()
    {
        using var root = new Panel();
        var first = root.Controls.Add(new Button { Width = 100, Height = 80 });
        var second = root.Controls.Add(new Button { Left = 150, Width = 100, Height = 80 });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(300, 100);
        surface.ProcessPointer(9, ControlSurfacePointerAction.Down, 10, 10);
        bool once = false;
        first.MouseLeave += (_, _) =>
        {
            if (once) return;
            once = true;
            surface.ProcessPointer(9, ControlSurfacePointerAction.Down, 160, 10);
        };

        surface.ProcessPointer(9, ControlSurfacePointerAction.Down, 20, 10);

        Assert.Equal(1, surface.ActivePointerCount);
        Assert.True(second.Capture);
        Assert.False(first.Capture);
        surface.ProcessPointer(9, ControlSurfacePointerAction.Up, 160, 10);
        Assert.Equal(0, surface.ActivePointerCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReentrantDownCannotEraseNewerPointerEvenWhenOldCallbackThrows(bool throws)
    {
        using var root = new Panel();
        var first = root.Controls.Add(new Button { Width = 100, Height = 80 });
        var second = root.Controls.Add(new Button { Left = 150, Width = 100, Height = 80 });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(300, 100);
        first.MouseDown += (_, _) =>
        {
            surface.ProcessPointer(5, ControlSurfacePointerAction.Down, 160, 10);
            if (throws) throw new InvalidOperationException("old-down");
        };

        if (throws)
            Assert.Equal("old-down", Assert.Throws<InvalidOperationException>(
                () => surface.ProcessPointer(5, ControlSurfacePointerAction.Down, 10, 10)).Message);
        else
            surface.ProcessPointer(5, ControlSurfacePointerAction.Down, 10, 10);

        Assert.Equal(1, surface.ActivePointerCount);
        Assert.True(second.Capture);
        Assert.False(first.Capture);
        surface.ProcessPointer(5, ControlSurfacePointerAction.Up, 160, 10);
    }

    [Fact]
    public void ThrowingClickStillReleasesItsOldCapture()
    {
        using var button = new Button();
        using var surface = new SkiaControlSurface(button);
        surface.Resize(100, 80);
        button.Click += (_, _) => throw new InvalidOperationException("click");
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 10, 10);
        Assert.Equal("click", Assert.Throws<InvalidOperationException>(
            () => surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 10, 10)).Message);
        Assert.False(button.Capture);
        Assert.Equal(0, surface.ActivePointerCount);
    }

    [Fact]
    public void InnerScrollKeepsGestureAndCannotStartAncestorAtItsLaterEdge()
    {
        using var outer = new EdgeProbe();
        var inner = outer.Controls.Add(new ScrollableControl { AutoScroll = true, Width = 200, Height = 150 });
        inner.Controls.Add(new Control { Width = 100, Height = 1000 });
        using var surface = new SkiaControlSurface(outer);
        surface.Resize(220, 180);
        inner.PerformLayout();
        inner.VerticalScrollProperties.Value = 50;

        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 20, 30);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 20, 60);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 20, 160);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 20, 160);

        Assert.Equal(0, inner.VerticalScrollProperties.Value);
        Assert.Equal(0, outer.Starts);
    }

    [Fact]
    public void InnerTopEdgeChoosesAncestorOnceAndCancellationRetiresIt()
    {
        using var outer = new EdgeProbe();
        var inner = outer.Controls.Add(new ScrollableControl { AutoScroll = true, Width = 200, Height = 150 });
        inner.Controls.Add(new Control { Width = 100, Height = 1000 });
        using var surface = new SkiaControlSurface(outer);
        surface.Resize(220, 180);
        inner.PerformLayout();

        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 20, 30);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 20, 90);
        Assert.Equal(1, outer.Starts);
        Assert.Equal(60, outer.Distance);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Cancel, 20, 90);

        Assert.True(outer.LastCanceled);
        Assert.Null(outer.Active);
        Assert.Equal(0, surface.ActivePointerCount);
        Assert.False(outer.Capture);
    }

    [Fact]
    public void CompletionCannotReleaseNewerGestureOnSameOwnerAndPointerId()
    {
        using var outer = new EdgeProbe();
        using var surface = new SkiaControlSurface(outer);
        surface.Resize(220, 180);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 20, 30);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 20, 80);
        object? old = outer.Active;
        bool once = false;
        outer.Ended = () =>
        {
            if (once) return;
            once = true;
            surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 20, 30);
            surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 20, 90);
        };

        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 20, 80);

        Assert.Equal(1, surface.ActivePointerCount);
        Assert.True(outer.Capture);
        Assert.NotNull(outer.Active);
        Assert.NotSame(old, outer.Active);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 20, 90);
        Assert.Equal(0, surface.ActivePointerCount);
        Assert.False(outer.Capture);
    }

    [Theory]
    [InlineData(1, 90, 1)]
    [InlineData(90, 1, 0)]
    public void DominantMovementChoosesAxisBeforeNestedEdgeArbitration(int dx, int dy, int ancestorStarts)
    {
        using var outer = new EdgeProbe();
        var inner = outer.Controls.Add(new ScrollableControl { AutoScroll = true, Width = 200, Height = 150 });
        inner.Controls.Add(new Control { Width = 1000, Height = 40 });
        using var surface = new SkiaControlSurface(outer);
        surface.Resize(220, 180);
        inner.PerformLayout();
        inner.HorizontalScrollProperties.Value = 50;
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 20, 50);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 20 + dx, 50 + dy);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 20 + dx, 50 + dy);
        Assert.Equal(ancestorStarts, outer.Starts);
    }

    private sealed class EdgeProbe : ScrollableControl
    {
        internal EdgeProbe() => AutoScroll = true;
        internal object? Active;
        internal int Starts, Distance;
        internal bool LastCanceled;
        internal Action? Ended;
        internal override bool CanStartTouchScroll(Point origin, Point movement) => movement.Y > 0 && Active is null;
        internal override void BeginTouchScroll(object gesture, Point origin, Point movement)
        {
            if (Active is not null) return;
            Active = gesture;
            Starts++;
        }
        internal override bool ContinueTouchScroll(object gesture, Point movement, Point delta)
        {
            if (!ReferenceEquals(Active, gesture)) return false;
            Distance = movement.Y;
            return true;
        }
        internal override void EndTouchScroll(object gesture, bool canceled)
        {
            if (!ReferenceEquals(Active, gesture)) return;
            Active = null;
            LastCanceled = canceled;
            Ended?.Invoke();
        }
    }

    private sealed class ScaledWindow : WindowBase
    {
        internal ScaledWindow(double scale)
            : this(DispatchProxy.Create<IWindowBaseImpl, ScalingProxy>(), scale) { }
        private ScaledWindow(IWindowBaseImpl backend, double scale) : base(backend)
            => ((ScalingProxy)backend).RenderScaling = scale;
    }

    private class ScalingProxy : DispatchProxy
    {
        public double RenderScaling { get; set; } = 1;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_RenderScaling") return RenderScaling;
            return targetMethod is null || targetMethod.ReturnType == typeof(void) ? null
                : targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
    }
}
