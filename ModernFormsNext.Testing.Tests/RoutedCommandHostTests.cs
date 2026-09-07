using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class RoutedCommandHostTests
{
    [Fact]
    public void BackgroundRequeryUsesExistingDispatcherAndSemanticActivation()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var command = new RoutedCommand("Save");
        var button = root.Controls.Add(new Button { Command = command });
        int uiThread = Environment.CurrentManagedThreadId, executions = 0;
        bool available = true;
        var threads = new List<int>();
        root.CommandBindings.Add(new(command, (_, e) => { executions++; e.Handled = true; },
            (_, e) => { threads.Add(Environment.CurrentManagedThreadId); e.CanExecute = available; }));
        host.Show(root, 400, 200);
        Assert.True(button.Enabled);
        RunBackground(() => { available = false; command.RaiseCanExecuteChanged(); });
        Assert.True(button.Enabled);
        host.Dispatcher.Drain();
        Assert.False(button.Enabled);
        Assert.False(button.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        RunBackground(() => { available = true; command.RaiseCanExecuteChanged(); });
        host.Dispatcher.Drain();
        Assert.True(button.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(1, executions);
        Assert.All(threads, thread => Assert.Equal(uiThread, thread));
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void QueuedRoutedNotificationCannotQueryDisposedButton()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var command = new RoutedCommand();
        int queries = 0;
        root.CommandBindings.Add(new(command, (_, _) => { }, (_, e) => { queries++; e.CanExecute = true; }));
        var button = root.Controls.Add(new Button { Command = command });
        host.Show(root, 400, 200);
        RunBackground(command.RaiseCanExecuteChanged);
        button.Dispose();
        int beforeDrain = queries;
        host.Dispatcher.Drain();
        Assert.Equal(beforeDrain, queries);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    private static void RunBackground(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } });
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
