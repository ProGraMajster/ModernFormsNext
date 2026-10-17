using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class RefreshViewHostTests
{
    [Fact]
    public void BackgroundCompletionUsesExistingDispatcherAndPreservesContentAvailability()
    {
        using var host = ModernFormsTestHost.Create();
        var refresh = new RefreshView();
        var content = refresh.Controls.Add(new TextBox { Dock = DockStyle.Fill });
        host.Show(refresh, 320, 240);
        var pending = new TaskCompletionSource();
        var command = new AsyncCommand(() => pending.Task);
        refresh.Command = command;
        int uiThread = Environment.CurrentManagedThreadId;
        var threads = new List<int>();
        refresh.RefreshStateChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
        refresh.RequestRefresh();
        Assert.True(refresh.IsRefreshing);
        Assert.False(refresh.CanRequestRefresh);
        Assert.True(content.Enabled);
        Complete(() => pending.SetResult());
        Assert.True(command.ExecutionTask!.IsCompletedSuccessfully);
        Assert.False(refresh.CanRequestRefresh);
        host.Dispatcher.Drain();
        Assert.False(refresh.IsRefreshing);
        Assert.True(refresh.CanRequestRefresh);
        Assert.True(content.Enabled);
        Assert.Equal(2, threads.Count);
        Assert.All(threads, thread => Assert.Equal(uiThread, thread));
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void QueuedOldCompletionCannotEnableNewRunningCommand()
    {
        using var host = ModernFormsTestHost.Create();
        var refresh = new RefreshView();
        host.Show(refresh, 320, 240);
        var first = new TaskCompletionSource();
        var old = new AsyncCommand(() => first.Task);
        refresh.Command = old;
        refresh.RequestRefresh();
        Complete(() => first.SetResult()); // Old guarded source refresh is queued.
        var next = new TaskCompletionSource();
        var current = new AsyncCommand(() => next.Task);
        refresh.Command = current;
        int changes = 0;
        refresh.RefreshStateChanged += (_, _) => changes++;
        refresh.RequestRefresh();
        int before = changes;
        try
        {
            host.Dispatcher.Drain();
            Assert.True(refresh.IsRefreshing);
            Assert.False(refresh.CanRequestRefresh);
            Assert.Same(current, refresh.Command);
            Assert.Equal(before, changes);
        }
        finally
        {
            Complete(() => next.SetResult());
            host.Dispatcher.Drain();
        }
        Assert.True(refresh.CanRequestRefresh);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void DisposedSourceRejectsQueuedCompletionAndDoesNotCancelBorrowedWork()
    {
        using var host = ModernFormsTestHost.Create();
        var refresh = new RefreshView();
        host.Show(refresh, 320, 240);
        var pending = new TaskCompletionSource();
        CancellationToken token = default;
        var command = new AsyncCommand((_, value) => { token = value; return pending.Task; });
        refresh.Command = command;
        refresh.RequestRefresh();
        int changes = 0;
        refresh.RefreshStateChanged += (_, _) => changes++;
        Complete(() => pending.SetResult());
        refresh.Dispose();
        host.Dispatcher.Drain();
        Assert.False(token.IsCancellationRequested);
        Assert.True(command.ExecutionTask!.IsCompletedSuccessfully);
        Assert.Equal(0, changes);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void AsyncFaultRecoversActionAndUsesCanonicalUiExceptionPath()
    {
        using var host = ModernFormsTestHost.Create();
        var refresh = new RefreshView();
        host.Show(refresh, 320, 240);
        var pending = new TaskCompletionSource();
        var failure = new InvalidOperationException("refresh");
        var command = new AsyncCommand(() => pending.Task);
        refresh.Command = command;
        refresh.RequestRefresh();
        Complete(() => pending.SetException(failure));
        Assert.Same(failure, command.ExecutionTask!.Exception!.InnerException);
        host.Dispatcher.Drain();
        Assert.False(refresh.IsRefreshing);
        Assert.True(refresh.CanRequestRefresh);
        Assert.Same(failure, Assert.Single(host.Dispatcher.UnhandledExceptions));
    }

    private static void Complete(Action completion)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { completion(); } catch (Exception error) { failure = error; } });
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
