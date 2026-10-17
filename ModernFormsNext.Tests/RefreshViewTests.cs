using System.Drawing;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class RefreshViewTests
{
    [Fact]
    public void RequestEventPrecedesCommandAndRecursiveRequestIsIgnored()
    {
        using var refresh = new RefreshView();
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 220);
        var order = new List<string>();
        refresh.Command = new DelegateCommand(() => order.Add("command"));
        refresh.RefreshRequested += (_, _) =>
        {
            order.Add("event");
            refresh.RequestRefresh();
        };
        refresh.RequestRefresh();
        Assert.Equal(["event", "command"], order);
        refresh.RequestRefresh();
        Assert.Equal(["event", "command", "event", "command"], order);
    }

    [Fact]
    public void EventOnlyAndAccessibleButtonUseTheSameRequest()
    {
        using var refresh = new RefreshView();
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 220);
        int requests = 0;
        refresh.RefreshRequested += (_, _) => requests++;
        refresh.RequestRefresh();
        var action = refresh.AccessibilityObject.GetChild(0)!;
        Assert.Equal(AccessibleControlType.Button, action.ControlType);
        Assert.True(action.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(2, requests);
    }

    [Theory]
    [InlineData("hide")]
    [InlineData("disable")]
    [InlineData("reparent")]
    [InlineData("dispose")]
    [InlineData("command")]
    [InlineData("parameter")]
    public void RequestCallbackCannotDispatchOlderWork(string action)
    {
        using var root = new Panel();
        using var destination = new Panel();
        var refresh = root.Controls.Add(new RefreshView());
        using var surface = new SkiaControlSurface(root);
        surface.Resize(320, 220);
        int executions = 0, newer = 0;
        refresh.Command = new DelegateCommand(() => executions++);
        refresh.RefreshRequested += (_, _) =>
        {
            switch (action)
            {
                case "hide": refresh.Visible = false; break;
                case "disable": refresh.Enabled = false; break;
                case "reparent": destination.Controls.Add(refresh); break;
                case "dispose": refresh.Dispose(); break;
                case "command": refresh.Command = new DelegateCommand(() => newer++); break;
                case "parameter": refresh.CommandParameter = new object(); break;
            }
        };
        refresh.RequestRefresh();
        Assert.Equal(0, executions);
        Assert.Equal(0, newer);
    }

    [Fact]
    public void CommandAvailabilityDoesNotDisableContentAndCanRecover()
    {
        using var refresh = new RefreshView();
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 220);
        var content = refresh.Controls.Add(new TextBox { Dock = DockStyle.Fill });
        bool available = false;
        int executions = 0;
        var command = new DelegateCommand(() => executions++, () => available);
        refresh.Command = command;
        Assert.True(refresh.Enabled);
        Assert.True(content.Enabled);
        Assert.False(refresh.CanRequestRefresh);
        refresh.RequestRefresh();
        Assert.Equal(0, executions);
        available = true;
        command.RaiseCanExecuteChanged();
        Assert.True(refresh.CanRequestRefresh);
        refresh.RequestRefresh();
        Assert.Equal(1, executions);
    }

    [Fact]
    public void AsyncCommandOwnsSingleFlightCancellationAndCompletion()
    {
        using var refresh = new RefreshView();
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 220);
        var gate = new TaskCompletionSource();
        CancellationToken invocation = default;
        int executions = 0;
        var command = new AsyncCommand((_, token) =>
        {
            executions++;
            invocation = token;
            return gate.Task;
        });
        refresh.Command = command;
        refresh.RequestRefresh();
        Assert.True(refresh.IsRefreshing);
        Assert.True(refresh.CanCancelRefresh);
        refresh.RequestRefresh();
        Assert.Equal(1, executions);
        refresh.CancelRefresh();
        Assert.True(invocation.IsCancellationRequested);
        Assert.True(refresh.IsRefreshing);
        Complete(() => gate.SetCanceled(invocation));
        Assert.True(command.ExecutionTask!.IsCanceled);
        Assert.False(refresh.IsRefreshing);
        Assert.True(refresh.CanRequestRefresh);
        gate = new TaskCompletionSource();
        refresh.RequestRefresh();
        Assert.Equal(2, executions);
        Complete(() => gate.SetResult());
        Assert.True(command.ExecutionTask!.IsCompletedSuccessfully);
        Assert.False(refresh.IsRefreshing);
    }

    [Fact]
    public void DisposalDoesNotCancelBorrowedCommandOrDeliverLaterState()
    {
        var refresh = new RefreshView();
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 220);
        var gate = new TaskCompletionSource();
        CancellationToken token = default;
        var command = new AsyncCommand((_, current) => { token = current; return gate.Task; });
        refresh.Command = command;
        int changes = 0;
        refresh.RefreshStateChanged += (_, _) => changes++;
        refresh.RequestRefresh();
        refresh.Dispose();
        int before = changes;
        Assert.False(token.IsCancellationRequested);
        Assert.True(command.IsExecuting);
        Complete(() => gate.SetResult());
        Assert.True(command.ExecutionTask!.IsCompletedSuccessfully);
        Assert.False(command.IsExecuting);
        Assert.Equal(before, changes);
    }

    [Fact]
    public void OldCommandCompletionCannotOverwriteReplacement()
    {
        using var refresh = new RefreshView();
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 220);
        var gate = new TaskCompletionSource();
        var old = new AsyncCommand(() => gate.Task);
        refresh.Command = old;
        refresh.RequestRefresh();
        int executions = 0, changes = 0;
        refresh.Command = new DelegateCommand(() => executions++);
        refresh.RefreshStateChanged += (_, _) => changes++;
        refresh.RequestRefresh();
        Complete(() => gate.SetResult());
        Assert.True(old.ExecutionTask!.IsCompletedSuccessfully);
        Assert.False(refresh.IsRefreshing);
        Assert.True(refresh.CanRequestRefresh);
        Assert.Equal(1, executions);
        Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData("release", 1)]
    [InlineData("below", 0)]
    [InlineData("cancel", 0)]
    [InlineData("reverse", 0)]
    public void PullThresholdReleaseReversalAndCancellation(string mode, int expected)
    {
        using var refresh = new RefreshView();
        refresh.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 240);
        int executions = 0;
        refresh.Command = new DelegateCommand(() => executions++);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 30, 80);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 30, mode == "below" ? 110 : 170);
        Assert.InRange(refresh.PullProgress, 0.01, 1);
        if (mode == "reverse") surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 30, 90);
        surface.ProcessPointer(1, mode == "cancel" ? ControlSurfacePointerAction.Cancel : ControlSurfacePointerAction.Up,
            30, mode == "reverse" ? 90 : mode == "below" ? 110 : 170);
        Assert.Equal(expected, executions);
        Assert.Equal(0, refresh.PullProgress);
        Assert.Equal(0, surface.ActivePointerCount);
        Assert.False(refresh.Capture);
    }

    [Fact]
    public void NestedScrollingKeepsItsSequenceAndOnlyFreshEdgePullRefreshes()
    {
        using var refresh = new RefreshView();
        var inner = refresh.Controls.Add(new ScrollableControl { AutoScroll = true, Dock = DockStyle.Fill });
        inner.Controls.Add(new Control { Height = 1000, Width = 100 });
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 240);
        inner.PerformLayout();
        int executions = 0;
        refresh.Command = new DelegateCommand(() => executions++);
        inner.VerticalScrollProperties.Value = 40;
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 30, 80);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 30, 150);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 30, 230);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 30, 230);
        Assert.Equal(0, inner.VerticalScrollProperties.Value);
        Assert.Equal(0, executions);
        surface.ProcessPointer(2, ControlSurfacePointerAction.Down, 30, 80);
        surface.ProcessPointer(2, ControlSurfacePointerAction.Move, 30, 170);
        surface.ProcessPointer(2, ControlSurfacePointerAction.Up, 30, 170);
        Assert.Equal(1, executions);
    }

    [Fact]
    public void VerticalPullAroundCarouselDoesNotChangeItsSelectedPage()
    {
        using var refresh = new RefreshView();
        var carousel = refresh.Controls.Add(new CarouselView { Dock = DockStyle.Fill, ItemsSource = new[] { "a", "b", "c" } });
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 240);
        int executions = 0;
        refresh.Command = new DelegateCommand(() => executions++);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 150, 80);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 152, 180);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 152, 180);
        Assert.Equal(1, executions);
        Assert.Equal(0, carousel.SelectedIndex);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void VerticalCarouselChoosesRefreshOnlyAtItsInitialTopEdge(int selected, int expected)
    {
        using var refresh = new RefreshView();
        var carousel = refresh.Controls.Add(new CarouselView { Dock = DockStyle.Fill,
            PageOrientation = Orientation.Vertical, ItemsSource = new[] { "a", "b", "c" } });
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 240);
        carousel.SelectedIndex = selected;
        int executions = 0;
        refresh.Command = new DelegateCommand(() => executions++);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 150, 80);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 150, 180);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 150, 180);
        Assert.Equal(expected, executions);
        Assert.Equal(0, refresh.PullProgress);
        Assert.Equal(0, surface.ActivePointerCount);
    }

    [Theory]
    [InlineData("command")]
    [InlineData("parameter")]
    [InlineData("threshold")]
    [InlineData("height")]
    public void PullRetirementCallbackCannotOverwriteNewerPropertyRequest(string property)
    {
        using var refresh = new RefreshView();
        refresh.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 240);
        var newer = new DelegateCommand(() => { });
        var parameter = new object();
        refresh.Command = new DelegateCommand(() => { });
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 30, 80);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 30, 150);
        Assert.True(refresh.PullProgress > 0);
        bool once = false;
        refresh.PullProgressChanged += (_, _) =>
        {
            if (once || refresh.PullProgress != 0) return;
            once = true;
            switch (property)
            {
                case "command": refresh.Command = newer; break;
                case "parameter": refresh.CommandParameter = parameter; break;
                case "threshold": refresh.PullThreshold = 128; break;
                case "height": refresh.HeaderHeight = 48; break;
            }
        };
        switch (property)
        {
            case "command": refresh.Command = new DelegateCommand(() => { }); Assert.Same(newer, refresh.Command); break;
            case "parameter": refresh.CommandParameter = new object(); Assert.Same(parameter, refresh.CommandParameter); break;
            case "threshold": refresh.PullThreshold = 96; Assert.Equal(128, refresh.PullThreshold); break;
            case "height": refresh.HeaderHeight = 40; Assert.Equal(48, refresh.HeaderHeight); break;
        }
    }

    [Fact]
    public void RequestFromReleaseProgressCallbackSupersedesOlderRelease()
    {
        using var refresh = new RefreshView();
        refresh.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 240);
        int executions = 0;
        refresh.Command = new DelegateCommand(() => executions++);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 30, 80);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Move, 30, 170);
        refresh.PullProgressChanged += (_, _) => { if (refresh.PullProgress == 0) refresh.RequestRefresh(); };
        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 30, 170);
        Assert.Equal(1, executions);
    }

    [Fact]
    public void F5UsesScopedBindingAndPreservesEditorFocus()
    {
        using var refresh = new RefreshView();
        var editor = refresh.Controls.Add(new TextBox { Dock = DockStyle.Fill });
        using var surface = new SkiaControlSurface(refresh);
        surface.Resize(320, 240);
        int executions = 0;
        refresh.Command = new DelegateCommand(() => executions++);
        editor.Select();
        Assert.True(surface.TryProcessKeyDown(new(Keys.F5)));
        Assert.Equal(1, executions);
        Assert.True(editor.Focused);
        surface.TryProcessKeyDown(new(Keys.F5 | Keys.Shift));
        Assert.Equal(1, executions);
    }

    private static void Complete(Action completion)
    {
        // Follow the existing AsyncCommand fixture: model completion on the owning UI
        // thread, without xUnit's synchronization context queueing the continuation.
        // Background completion/dispatcher delivery is qualified by the TestHost lane.
        var context = SynchronizationContext.Current;
        try { SynchronizationContext.SetSynchronizationContext(null); completion(); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
    }
}
