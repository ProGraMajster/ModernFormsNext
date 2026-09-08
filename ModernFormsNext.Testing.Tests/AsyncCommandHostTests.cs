using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class AsyncCommandHostTests
{
    [Fact]
    public void CancellationCallbackFailureStillRefreshesStateAndPreservesFailure()
    {
        using var host = ModernFormsTestHost.Create();
        var pending = new TaskCompletionSource();
        var failure = new InvalidOperationException("cancel callback");
        var command = new AsyncCommand((_, token) => { token.Register(() => throw failure); return pending.Task; });
        int notifications = 0;
        command.CanExecuteChanged += (_, _) => notifications++;
        _ = command.ExecuteAsync();
        var error = Assert.Throws<AggregateException>(command.Cancel);
        Assert.Same(failure, Assert.Single(error.InnerExceptions));
        Assert.Equal(2, notifications);
        Assert.True(command.IsExecuting);
        Assert.False(command.CanCancel);
        Complete(() => pending.SetResult());
        Assert.True(command.ExecutionTask!.IsCompletedSuccessfully); // Ignoring cancellation remains successful.
    }

    [Fact]
    public void CancellationExceptionFromObserverIsAnObservedFaultNotOperationCancellation()
    {
        using var host = ModernFormsTestHost.Create();
        var failure = new OperationCanceledException("observer");
        var command = new AsyncCommand(() => { Assert.Fail(); return Task.CompletedTask; });
        command.Diagnostic += (_, e) => { if (e.Kind == AsyncCommandDiagnosticKind.Started) throw failure; };
        var task = command.ExecuteAsync();
        Assert.True(task.IsFaulted);
        Assert.Same(failure, task.Exception!.InnerException);
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public void AsyncButtonRetainsQueryClickFreshQueryOrderWithoutRedundantPredicate()
    {
        using var host = ModernFormsTestHost.Create();
        var pending = new TaskCompletionSource();
        var calls = new List<string>();
        var command = new AsyncCommand(() => { calls.Add("execute"); return pending.Task; }, () => { calls.Add("query"); return true; });
        using var button = new Button { Command = command };
        button.Click += (_, _) => calls.Add("click");
        calls.Clear();
        button.PerformClick();
        Assert.Equal(new[] { "query", "click", "query", "execute" }, calls);
        Complete(() => pending.SetResult());
        host.Dispatcher.Drain();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("document")]
    [InlineData(42)]
    public void ParameterAndPredicateArePreserved(object? parameter)
    {
        using var host = ModernFormsTestHost.Create();
        object? received = new object();
        int queries = 0;
        var command = new AsyncCommand(p => { received = p; return Task.CompletedTask; }, p =>
        {
            queries++;
            Assert.Same(parameter, p);
            return true;
        });
        Assert.True(command.ExecuteAsync(parameter).IsCompletedSuccessfully);
        Assert.Same(parameter, received);
        Assert.Equal(1, queries);
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public void UnavailableStartDoesNotReplaceLastTask()
    {
        using var host = ModernFormsTestHost.Create();
        bool allowed = false;
        int calls = 0;
        var command = new AsyncCommand(() => { calls++; return Task.CompletedTask; }, () => allowed);
        Assert.False(command.CanExecute(null));
        command.Execute(null);
        Assert.True(command.ExecuteAsync().IsCompletedSuccessfully);
        Assert.Null(command.ExecutionTask);
        allowed = true;
        var previous = command.ExecuteAsync();
        allowed = false;
        command.Execute(null);
        Assert.Same(previous, command.ExecutionTask);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void SingleFlightIncludesReentrantStartNotificationAndRecovers()
    {
        using var host = ModernFormsTestHost.Create();
        var pending = new TaskCompletionSource();
        int calls = 0, notifications = 0;
        var command = new AsyncCommand(() => { calls++; return pending.Task; });
        command.CanExecuteChanged += (_, _) =>
        {
            notifications++;
            if (command.IsExecuting) Assert.True(command.ExecuteAsync().IsCompletedSuccessfully);
        };
        var task = command.ExecuteAsync();
        Assert.True(command.IsExecuting);
        Assert.False(command.CanExecute(null));
        command.Execute(null);
        Assert.Same(task, command.ExecutionTask);
        Assert.Equal(1, calls);
        Complete(() => pending.SetResult());
        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(command.IsExecuting);
        Assert.True(command.CanExecute(null));
        Assert.Equal(2, notifications);
        command.RaiseCanExecuteChanged();
        Assert.Equal(3, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FaultIsObservedWithOriginalIdentityAndStateRecovers(bool supervised)
    {
        using var host = ModernFormsTestHost.Create();
        var pending = new TaskCompletionSource();
        var failure = new InvalidOperationException("private user data");
        var command = new AsyncCommand(() => pending.Task);
        if (supervised) command.Execute(null); else _ = command.ExecuteAsync();
        Complete(() => pending.SetException(failure));
        Assert.False(command.IsExecuting);
        Assert.True(command.CanExecute(null));
        Assert.Same(failure, Assert.Single(command.ExecutionTask!.Exception!.InnerExceptions));
        host.Dispatcher.Drain();
        if (supervised) Assert.Same(failure, Assert.Single(host.Dispatcher.UnhandledExceptions));
        else Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SynchronousCallbackFailuresUseTheSameTaskPolicy(bool nullTask)
    {
        using var host = ModernFormsTestHost.Create();
        var failure = new InvalidOperationException("callback");
        var command = new AsyncCommand(() => nullTask ? null! : throw failure);
        var task = command.ExecuteAsync();
        Assert.True(task.IsFaulted);
        Assert.IsType<InvalidOperationException>(task.Exception!.InnerException);
        if (!nullTask) Assert.Same(failure, task.Exception.InnerException);
        Assert.False(command.IsExecuting);
        host.Dispatcher.Drain();
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void PredicateFailureIsSynchronousAndDoesNotStartExecution()
    {
        using var host = ModernFormsTestHost.Create();
        var failure = new InvalidOperationException("predicate");
        var command = new AsyncCommand(() => Task.CompletedTask, () => throw failure);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => { _ = command.ExecuteAsync(); }));
        Assert.Null(command.ExecutionTask);
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public void CancellationUsesFreshTokensAndWaitsForCooperativeCompletion()
    {
        using var host = ModernFormsTestHost.Create();
        var pending = new TaskCompletionSource();
        CancellationToken token = default;
        var command = new AsyncCommand((_, value) => { token = value; return pending.Task; });
        Assert.False(command.CanCancel);
        command.Cancel();
        var task = command.ExecuteAsync();
        Assert.True(command.CanCancel);
        var first = token;
        command.Cancel();
        command.Cancel();
        Assert.True(token.IsCancellationRequested);
        Assert.True(command.IsCancellationRequested);
        Assert.False(command.CanCancel);
        Assert.True(command.IsExecuting);
        Complete(() => pending.SetCanceled(token));
        Assert.True(task.IsCanceled);
        Assert.False(command.IsExecuting);
        Assert.False(command.IsCancellationRequested);
        pending = new TaskCompletionSource();
        command.Execute(null);
        Assert.NotEqual(first, token);
        Assert.False(token.IsCancellationRequested);
        Complete(() => pending.SetResult());
        Assert.True(command.ExecutionTask!.IsCompletedSuccessfully);
        host.Dispatcher.Drain();
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void InlineCancellationCompletionDoesNotDisposeTokenSourceInsideCancel()
    {
        using var host = ModernFormsTestHost.Create();
        var pending = new TaskCompletionSource();
        var command = new AsyncCommand((_, token) =>
        {
            token.Register(() => pending.SetCanceled(token));
            return pending.Task;
        });
        command.Execute(null);
        // xUnit's synchronization context otherwise asks Task to queue this continuation.
        // Remove it only while exercising the same-thread inline completion edge case.
        var context = SynchronizationContext.Current;
        try { SynchronizationContext.SetSynchronizationContext(null); command.Cancel(); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
        Assert.True(command.ExecutionTask!.IsCanceled);
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public void NonCancellableCommandIgnoresCancel()
    {
        using var host = ModernFormsTestHost.Create();
        var pending = new TaskCompletionSource();
        var command = new AsyncCommand(() => pending.Task);
        var task = command.ExecuteAsync();
        command.Cancel();
        Assert.False(command.CanCancel);
        Assert.False(command.IsCancellationRequested);
        Complete(() => pending.SetResult());
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void BackgroundMutationIsRejectedButStateReadsAndRequeryAreAllowed()
    {
        using var host = ModernFormsTestHost.Create();
        var command = new AsyncCommand(() => Task.CompletedTask);
        Complete(() =>
        {
            Assert.Throws<InvalidOperationException>(() => command.Execute(null));
            Assert.Throws<InvalidOperationException>(() => { _ = command.ExecuteAsync(); });
            Assert.Throws<InvalidOperationException>(() => command.CanExecute(null));
            Assert.Throws<InvalidOperationException>(command.Cancel);
            Assert.False(command.IsExecuting);
            Assert.Null(command.ExecutionTask);
            command.RaiseCanExecuteChanged();
        });
    }

    [Fact]
    public void SharedSourcesAndAccessibilityRecoverOnUiThreadWithoutOverwritingLocalDisabled()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var pending = new TaskCompletionSource();
        var command = new AsyncCommand(() => pending.Task);
        var first = root.Controls.Add(new Button { Command = command });
        var second = root.Controls.Add(new Button { Command = command });
        using var tray = new NotifyIconMenuItem { Command = command };
        var threads = new List<int>();
        int uiThread = Environment.CurrentManagedThreadId;
        second.EnabledChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
        host.Show(root, 400, 200);
        Assert.True(first.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        Assert.False(first.Enabled);
        Assert.False(second.Enabled);
        Assert.False(tray.Enabled);
        Assert.False(second.AccessibilityObject.PerformAction(AccessibleActions.Invoke));
        first.Enabled = false;
        Complete(() => pending.SetResult());
        Assert.False(second.Enabled); // Background completion queues each source's guarded refresh.
        host.Dispatcher.Drain();
        Assert.False(first.Enabled);
        Assert.True(second.Enabled);
        Assert.True(tray.Enabled);
        Assert.All(threads, thread => Assert.Equal(uiThread, thread));
    }

    [Theory]
    [InlineData("dispose")]
    [InlineData("replace")]
    [InlineData("close")]
    public void CompletionCannotMutateRetiredSourcesOrCancelSharedWork(string transition)
    {
        using var host = ModernFormsTestHost.Create();
        var pending = new TaskCompletionSource();
        CancellationToken token = default;
        var command = new AsyncCommand((_, value) => { token = value; return pending.Task; });
        var root = new Panel();
        var button = root.Controls.Add(new Button { Command = command });
        var window = host.Show(root, 300, 200);
        using var other = new NotifyIconMenuItem { Command = command };
        button.PerformClick();
        if (transition == "dispose") button.Dispose();
        else if (transition == "close") window.Close();
        else button.Command = new DelegateCommand(() => { }, () => false);
        int changes = 0;
        button.EnabledChanged += (_, _) => changes++;
        Assert.False(token.IsCancellationRequested);
        Complete(() => pending.SetResult());
        command.RaiseCanExecuteChanged();
        host.Dispatcher.Drain();
        Assert.Equal(0, changes);
        Assert.True(other.Enabled);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void RoutedHandlerComposesWithAsyncHelperAndRequeriesAllSources()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var pending = new TaskCompletionSource();
        object? received = null;
        var work = new AsyncCommand(p => { received = p; return pending.Task; });
        var routed = new RoutedCommand("Load");
        work.CanExecuteChanged += (_, _) => routed.RaiseCanExecuteChanged();
        root.CommandBindings.Add(new(routed, (_, e) => { work.Execute(e.Parameter); e.Handled = true; },
            (_, e) => e.CanExecute = work.CanExecute(e.Parameter)));
        var button = root.Controls.Add(new Button { Command = routed, CommandParameter = "document" });
        host.Show(root, 400, 200);
        button.PerformClick();
        Assert.Equal("document", received);
        Assert.False(button.Enabled);
        Complete(() => pending.SetResult());
        host.Dispatcher.Drain();
        Assert.True(button.Enabled);
    }

    [Theory]
    [InlineData("completed", AsyncCommandDiagnosticKind.Completed)]
    [InlineData("faulted", AsyncCommandDiagnosticKind.Faulted)]
    [InlineData("cancelled", AsyncCommandDiagnosticKind.Cancelled)]
    public void DiagnosticsExposeOnlyTypesAndOutcome(string outcome, AsyncCommandDiagnosticKind expected)
    {
        using var host = ModernFormsTestHost.Create();
        var pending = new TaskCompletionSource();
        var command = new AsyncCommand(_ => pending.Task);
        var events = new List<AsyncCommandDiagnosticEventArgs>();
        command.Diagnostic += (_, e) => events.Add(e);
        _ = command.ExecuteAsync("password-user-data");
        Complete(() =>
        {
            if (outcome == "faulted") pending.SetException(new InvalidOperationException("private text"));
            else if (outcome == "cancelled") pending.SetCanceled();
            else pending.SetResult();
        });
        _ = command.ExecutionTask!.Exception;
        Assert.Equal(new[] { AsyncCommandDiagnosticKind.Started, expected }, events.Select(e => e.Kind));
        Assert.All(events, e => Assert.Equal(typeof(string), e.ParameterType));
        Assert.Equal(outcome == "faulted" ? typeof(InvalidOperationException) : null, events[1].ExceptionType);
        Assert.Equal(new[] { "ExceptionType", "Kind", "ParameterType" },
            typeof(AsyncCommandDiagnosticEventArgs).GetProperties().Select(p => p.Name).Order());
    }

    [Fact]
    public void OriginalOperationFaultWinsOverObserverFaultAndStateStillRecovers()
    {
        using var host = ModernFormsTestHost.Create();
        var pending = new TaskCompletionSource();
        var original = new InvalidOperationException("operation");
        var command = new AsyncCommand(() => pending.Task);
        command.CanExecuteChanged += (_, _) => { if (!command.IsExecuting) throw new Exception("observer"); };
        _ = command.ExecuteAsync();
        Complete(() => pending.SetException(original));
        Assert.False(command.IsExecuting);
        Assert.Same(original, command.ExecutionTask!.Exception!.InnerException);
    }

    [Fact]
    public void NullCallbacksAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncCommand((Func<Task>)null!));
        Assert.Throws<ArgumentNullException>(() => new AsyncCommand((Func<object?, Task>)null!));
        Assert.Throws<ArgumentNullException>(() => new AsyncCommand((Func<object?, CancellationToken, Task>)null!));
    }

    // Completion runs on a controlled thread with no synchronization context; the default TCS
    // executes the registered continuation inline. Joining it includes all completion/posts.
    private static void Complete(Action complete)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { complete(); } catch (Exception error) { failure = error; } });
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
