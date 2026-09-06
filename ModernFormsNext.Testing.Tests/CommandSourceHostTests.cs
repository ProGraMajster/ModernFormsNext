using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class CommandSourceHostTests
{
    [Fact]
    public void ConsumerCanInvokeSharedCommandThroughSemanticButton()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var calls = new List<object?>();
        bool allowed = true;
        var command = new DelegateCommand(calls.Add, _ => allowed);
        var first = root.Controls.Add(new Button { Command = command, CommandParameter = "first" });
        var second = root.Controls.Add(new Button { Command = command, CommandParameter = "second" });
        host.Show(root, 400, 200);

        Assert.True(first.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(new object?[] { "first" }, calls);
        allowed = false;
        command.RaiseCanExecuteChanged();
        Assert.False(first.Enabled);
        Assert.False(second.Enabled);
        Assert.False(second.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        allowed = true;
        command.RaiseCanExecuteChanged();
        Assert.True(second.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(new object?[] { "first", "second" }, calls);
    }

    [Fact]
    public void BackgroundRequeryEvaluatesAndNotifiesOnlyOnUiThread()
    {
        using var host = ModernFormsTestHost.Create();
        int uiThread = Environment.CurrentManagedThreadId;
        bool allowed = true;
        var queryThreads = new List<int>();
        var eventThreads = new List<int>();
        var command = new DelegateCommand(() => { }, () =>
        {
            queryThreads.Add(Environment.CurrentManagedThreadId);
            return allowed;
        });
        using var button = new Button { Command = command };
        using var item = new NotifyIconMenuItem { Command = command };
        button.EnabledChanged += (_, _) => eventThreads.Add(Environment.CurrentManagedThreadId);

        RunBackground(() => { allowed = false; command.RaiseCanExecuteChanged(); });
        Assert.True(button.Enabled);
        Assert.True(item.Enabled);
        Assert.Empty(eventThreads);
        Assert.Equal(2, queryThreads.Count);
        host.Dispatcher.Drain();
        Assert.False(button.Enabled);
        Assert.False(item.Enabled);
        Assert.Equal(new[] { uiThread }, eventThreads);
        Assert.All(queryThreads, id => Assert.Equal(uiThread, id));

        RunBackground(() => { allowed = true; command.RaiseCanExecuteChanged(); });
        host.Dispatcher.Drain();
        Assert.True(button.Enabled);
        Assert.True(item.Enabled);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueuedOldNotificationCannotTouchReplacementOrDisposedSource(bool dispose)
    {
        using var host = ModernFormsTestHost.Create();
        int oldQueries = 0;
        int newQueries = 0;
        var oldCommand = new DelegateCommand(() => { }, () => { oldQueries++; return true; });
        using var button = new Button { Command = oldCommand };
        RunBackground(oldCommand.RaiseCanExecuteChanged);
        if (dispose)
            button.Dispose();
        else
            button.Command = new DelegateCommand(() => { }, () => { newQueries++; return false; });
        host.Dispatcher.Drain();
        Assert.Equal(1, oldQueries);
        Assert.Equal(dispose ? 0 : 1, newQueries);
        if (!dispose) Assert.False(button.Enabled);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void BackgroundPredicateExceptionUsesExistingDispatcherExceptionPathAndRecovers()
    {
        using var host = ModernFormsTestHost.Create();
        var failure = new InvalidOperationException("background requery failed");
        bool fail = false;
        var command = new DelegateCommand(() => { }, () => fail ? throw failure : true);
        using var button = new Button { Command = command };
        RunBackground(() => { fail = true; command.RaiseCanExecuteChanged(); });
        host.Dispatcher.Drain();
        Assert.False(button.Enabled);
        Assert.Same(failure, Assert.Single(host.Dispatcher.UnhandledExceptions));
        fail = false;
        command.RaiseCanExecuteChanged();
        Assert.True(button.Enabled);
    }

    [Fact]
    public void PredicateIsNotPolledByDispatcherDrainsOrEnabledReads()
    {
        using var host = ModernFormsTestHost.Create();
        int queries = 0;
        var command = new DelegateCommand(() => { }, () => { queries++; return true; });
        using var button = new Button { Command = command };
        for (int i = 0; i < 5; i++)
        {
            host.Dispatcher.Drain();
            Assert.True(button.Enabled);
        }
        Assert.Equal(1, queries);
    }

    [Fact]
    public void DirectBackgroundMutationIsRejectedWithoutChangingBinding()
    {
        using var host = ModernFormsTestHost.Create();
        using var button = new Button { Command = new DelegateCommand(() => { }) };
        RunBackground(() => Assert.Throws<InvalidOperationException>(() => button.CommandParameter = "invalid thread"));
        Assert.Null(button.CommandParameter);
        Assert.True(button.Enabled);
    }

    private static void RunBackground(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } });
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
