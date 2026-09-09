using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

[Trait("Category", "Threading")]
public sealed class ThreadingTests
{
    [Fact]
    public void BackgroundQueryReadsOnlyOnProductionDispatcher()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        int ui = Environment.CurrentManagedThreadId; var reads = new List<int>();
        c.Child.NameGetter = () => { reads.Add(Environment.CurrentManagedThreadId); return "child"; };
        Task<AutomationResult<AutomationNodeSnapshot>>? pending = null;
        CompletedTaskAssertions.Worker(() => pending = f.Session.FindOneAsync(f.Root.RootId, new() { Name = "child" }));
        Assert.Empty(reads); Assert.False(pending!.IsCompleted); Assert.True(f.Host.Dispatcher.PendingWorkCount > 0);
        f.Host.Dispatcher.Drain();
        Assert.Equal(AutomationErrorCode.None, CompletedTaskAssertions.Finish(pending).Error);
        Assert.NotEmpty(reads); Assert.All(reads, thread => Assert.Equal(ui, thread));
    }

    [Fact]
    public void BackgroundActionExecutesCanonicalPathOnlyOnUiThread()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        int ui = Environment.CurrentManagedThreadId, actionThread = 0;
        c.Child.Action = (_, _) => { actionThread = Environment.CurrentManagedThreadId; return true; };
        var handle = f.Handle(c.Child);
        Task<AutomationActionResult>? pending = null;
        CompletedTaskAssertions.Worker(() => pending = f.Session.PerformActionAsync(f.Root.RootId, handle, AccessibleActions.Invoke));
        Assert.Equal(0, actionThread);
        f.Host.Dispatcher.Drain();
        Assert.Equal(AutomationActionStatus.Accepted, CompletedTaskAssertions.Finish(pending!).Status);
        Assert.Equal(ui, actionThread);
    }

    [Fact]
    public void StopBeforeQueuedActionRunsInvalidatesIt()
    {
        using var f = new AutomationFixture(); var b = f.Add(new Button()); int clicks = 0; b.Click += (_, _) => clicks++;
        var handle = f.Handle(b); Task<AutomationActionResult>? pending = null;
        CompletedTaskAssertions.Worker(() => pending = f.Session.PerformActionAsync(f.Root.RootId, handle, AccessibleActions.Invoke));
        f.Session.Stop(); f.Host.Dispatcher.Drain();
        Assert.Equal(AutomationErrorCode.SessionEnded, CompletedTaskAssertions.Finish(pending!).Error);
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void BackgroundStopAsyncRemovesRootsOnDispatcher()
    {
        using var f = new AutomationFixture(); Task? pending = null;
        CompletedTaskAssertions.Worker(() => pending = f.Session.StopAsync());
        Assert.False(f.Session.IsStopped); f.Host.Dispatcher.Drain();
        Assert.True(pending!.IsCompletedSuccessfully); Assert.True(f.Session.IsStopped); Assert.False(f.Root.IsRegistered);
    }

    [Fact]
    public void CancellationBeforeQueuedExecutionProducesNoMutation()
    {
        using var f = new AutomationFixture(); var b = f.Add(new Button()); int clicks = 0; b.Click += (_, _) => clicks++;
        using var cancellation = new CancellationTokenSource(); var handle = f.Handle(b);
        Task<AutomationActionResult>? pending = null;
        CompletedTaskAssertions.Worker(() => pending = f.Session.PerformActionAsync(f.Root.RootId, handle, AccessibleActions.Invoke, cancellationToken: cancellation.Token));
        cancellation.Cancel(); f.Host.Dispatcher.Drain();
        Assert.ThrowsAny<OperationCanceledException>(() => CompletedTaskAssertions.Finish(pending!)); Assert.Equal(0, clicks);
    }

    [Fact]
    public void RegistrationRejectsNonUiCaller()
    {
        using var f = new AutomationFixture(); Exception? failure = null;
        CompletedTaskAssertions.Worker(() => failure = Record.Exception(() => f.Session.RegisterRoot(f.Form)));
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void StopBeforeQueuedQueryPreventsSemanticReads()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        int reads = 0; c.Child.NameGetter = () => { reads++; return "child"; };
        Task<AutomationResult<AutomationNodeSnapshot>>? pending = null;
        CompletedTaskAssertions.Worker(() => pending = f.Session.FindOneAsync(f.Root.RootId, new()));
        f.Session.Stop(); f.Host.Dispatcher.Drain();
        Assert.Equal(AutomationErrorCode.SessionEnded, CompletedTaskAssertions.Finish(pending!).Error);
        Assert.Equal(0, reads);
    }

    [Theory]
    [InlineData("close")]
    [InlineData("dispose")]
    [InlineData("unregister")]
    public void EndingRootBeforeQueuedActionPreventsMutation(string end)
    {
        using var f = new AutomationFixture(); var b = f.Add(new Button());
        int clicks = 0; b.Click += (_, _) => clicks++;
        var handle = f.Handle(b); Task<AutomationActionResult>? pending = null;
        CompletedTaskAssertions.Worker(() => pending = f.Session.PerformActionAsync(f.Root.RootId, handle, AccessibleActions.Invoke));
        if (end == "close") f.Form.Close(); else if (end == "dispose") f.Form.Dispose(); else f.Root.Dispose();
        f.Host.Dispatcher.Drain();
        Assert.Equal(AutomationErrorCode.StaleNode, CompletedTaskAssertions.Finish(pending!).Error);
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void BackgroundStopDuringGetterCompletesAfterCurrentDispatcherTurn()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        Task? stopped = null;
        c.Child.NameGetter = () =>
        {
            CompletedTaskAssertions.Worker(() => stopped = f.Session.StopAsync());
            Assert.False(stopped!.IsCompleted); Assert.False(f.Session.IsStopped);
            return "child";
        };
        Assert.Equal(AutomationErrorCode.None, f.Session.FindOneAsync(f.Root.RootId, new() { Name = "child" }).Completed().Error);
        f.Host.Dispatcher.Drain();
        Assert.True(stopped!.IsCompletedSuccessfully); Assert.True(f.Session.IsStopped);
        Assert.False(f.Root.IsRegistered);
    }

    [Fact]
    public void CancellationAndStopBeforeQueuedActionNeverMutate()
    {
        using var f = new AutomationFixture(); var b = f.Add(new Button());
        int clicks = 0; b.Click += (_, _) => clicks++;
        using var cancellation = new CancellationTokenSource();
        var handle = f.Handle(b); Task<AutomationActionResult>? pending = null;
        CompletedTaskAssertions.Worker(() => pending = f.Session.PerformActionAsync(f.Root.RootId, handle, AccessibleActions.Invoke, cancellationToken: cancellation.Token));
        cancellation.Cancel(); f.Session.Stop(); f.Session.Dispose(); f.Host.Dispatcher.Drain();
        Assert.ThrowsAny<OperationCanceledException>(() => CompletedTaskAssertions.Finish(pending!));
        Assert.Equal(0, clicks); Assert.True(f.Session.IsStopped);
    }

    [Fact]
    public void CancellationDuringGetterStopsBeforeNextPayloadRead()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        using var cancellation = new CancellationTokenSource(); int valueReads = 0;
        c.Child.NameGetter = () => { cancellation.Cancel(); return "child"; };
        c.Child.ValueGetter = () => { valueReads++; return "value"; };
        var pending = f.Session.InspectAsync(f.Root.RootId, f.Handle(c.Child), cancellation.Token);
        Assert.ThrowsAny<OperationCanceledException>(() => CompletedTaskAssertions.Finish(pending));
        Assert.Equal(0, valueReads);
    }

    [Fact]
    public void CancellationDuringFinalActionGetterPreventsMutation()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        using var cancellation = new CancellationTokenSource(); int actions = 0, reads = 0;
        c.Child.ActionsGetter = () => { if (++reads == 2) cancellation.Cancel(); return AccessibleActions.Invoke; };
        c.Child.Action = (_, _) => { actions++; return true; };
        var pending = f.Session.PerformActionAsync(f.Root.RootId, f.Handle(c.Child), AccessibleActions.Invoke, cancellationToken: cancellation.Token);
        Assert.ThrowsAny<OperationCanceledException>(() => CompletedTaskAssertions.Finish(pending));
        Assert.Equal(0, actions);
    }
}
