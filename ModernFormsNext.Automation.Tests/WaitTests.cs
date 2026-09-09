using System.Diagnostics;
using System.Reflection;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Threading;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

[Trait("Category", "Wait")]
public sealed class WaitTests
{
    [Theory]
    [InlineData(AutomationWaitKind.NodeExists)]
    [InlineData(AutomationWaitKind.Exposed)]
    [InlineData(AutomationWaitKind.Enabled)]
    [InlineData(AutomationWaitKind.Focused)]
    [InlineData(AutomationWaitKind.Selected)]
    [InlineData(AutomationWaitKind.StateContains)]
    [InlineData(AutomationWaitKind.ValueEquals)]
    public void AlreadySatisfiedCompletesWithoutQueuedWork(AutomationWaitKind kind)
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        c.Child.States = AccessibleStates.Focused | AccessibleStates.Selected; c.Child.Value = "Done";
        var result = f.Session.WaitForConditionAsync(f.Root.RootId,
            new() { Kind = kind, Handle = f.Handle(c.Child), Value = "Done", States = AccessibleStates.Selected },
            new() { Timeout = TimeSpan.Zero }).Completed();
        Assert.Equal(AutomationWaitStatus.Satisfied, result.Status);
        AssertNoObservation(f, c.Child);
    }

    [Fact]
    public void ChangeBetweenInitialReadAndSubscriptionIsRereadWithoutWaiting()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl()); int reads = 0;
        c.Child.ValueGetter = () => ++reads == 1 ? "Pending" : "Done";
        var result = f.Session.WaitForConditionAsync(f.Root.RootId,
            new() { Kind = AutomationWaitKind.ValueEquals, Handle = f.Handle(c.Child), Value = "Done" },
            new() { ReconciliationInterval = TimeSpan.FromSeconds(1) }).Completed();
        Assert.Equal(AutomationWaitStatus.Satisfied, result.Status); Assert.Equal(2, reads);
        AssertNoObservation(f, c.Child);
    }

    [Fact]
    public void NotificationWakesAndRereadsActualValue()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl()); c.Child.Value = "Pending";
        var pending = WaitValue(f, c, interval: TimeSpan.FromSeconds(1));
        Assert.False(pending.IsCompleted);
        c.Child.Value = "Done"; c.Child.NotifyClients(AccessibleEvents.ValueChange);
        Assert.Equal(AutomationWaitStatus.Satisfied, Pump(f, pending).Status);
        AssertNoObservation(f, c.Child);
    }

    [Fact]
    public void MissingNotificationUsesBoundedReconciliation()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl()); c.Child.Value = "Pending";
        var pending = WaitValue(f, c); c.Child.Value = "Done";
        Assert.Equal(AutomationWaitStatus.Satisfied, Pump(f, pending).Status); AssertNoObservation(f, c.Child);
    }

    [Theory]
    [InlineData("cancel", AutomationWaitStatus.Cancelled)]
    [InlineData("timeout", AutomationWaitStatus.TimedOut)]
    [InlineData("root", AutomationWaitStatus.RootEnded)]
    [InlineData("session", AutomationWaitStatus.SessionEnded)]
    public void EveryUnsuccessfulEndDetachesObservers(string end, AutomationWaitStatus expected)
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        using var cancellation = new CancellationTokenSource();
        var pending = WaitValue(f, c, cancellation.Token, timeout: end == "timeout" ? TimeSpan.FromMilliseconds(80) : null);
        if (end == "cancel") cancellation.Cancel();
        if (end == "root") f.Root.Dispose();
        if (end == "session") f.Session.Stop();
        Assert.Equal(expected, Pump(f, pending).Status); AssertNoObservation(f, c.Child);
    }

    [Fact]
    public void RootEndedWaitObservesUnregistration()
    {
        using var f = new AutomationFixture();
        var pending = f.Session.WaitForConditionAsync(f.Root.RootId, new() { Kind = AutomationWaitKind.RootEnded });
        f.Root.Dispose(); Assert.Equal(AutomationWaitStatus.Satisfied, Pump(f, pending).Status);
    }

    [Fact]
    public void CompleteAbsenceSatisfiesNodeNotExposed()
    {
        using var f = new AutomationFixture();
        var result = f.Session.WaitForConditionAsync(f.Root.RootId,
            new() { Kind = AutomationWaitKind.NodeNotExposed, Query = new() { AutomationId = "absent" } }).Completed();
        Assert.Equal(AutomationWaitStatus.Satisfied, result.Status); Assert.Null(result.Snapshot);
    }

    [Fact]
    public void IncompleteTraversalCannotCertifyAbsence()
    {
        using var f = new AutomationFixture(new() { MaxNodes = 1 }); f.Add(new Button());
        var result = f.Session.WaitForConditionAsync(f.Root.RootId,
            new() { Kind = AutomationWaitKind.NodeNotExposed, Query = new() { AutomationId = "absent" } }).Completed();
        Assert.Equal(AutomationWaitStatus.Failed, result.Status); Assert.Equal(AutomationErrorCode.LimitExceeded, result.Error);
    }

    [Fact]
    public void RedactedValueCannotBeUsedAsAnEqualityOracle()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl()); int reads = 0;
        c.Child.SensitiveGetter = () => true; c.Child.ValueGetter = () => { reads++; return "secret"; };
        var result = f.Session.WaitForConditionAsync(f.Root.RootId,
            new() { Kind = AutomationWaitKind.ValueEquals, Handle = f.Handle(c.Child), Value = "secret" }).Completed();
        Assert.Equal(AutomationErrorCode.CapabilityDenied, result.Error); Assert.Equal(0, reads); AssertNoObservation(f, c.Child);
    }

    [Fact]
    public void StaleSessionHandleIsNotRootClosure()
    {
        using var f = new AutomationFixture();
        var result = f.Session.WaitForConditionAsync(f.Root.RootId,
            new() { Handle = new("another-session", "1") }).Completed();
        Assert.Equal(AutomationWaitStatus.Failed, result.Status); Assert.Equal(AutomationErrorCode.StaleNode, result.Error);
    }

    [Fact]
    public void QueryRequiresQueryCapability()
    {
        using var f = new AutomationFixture(capabilities: AutomationCapability.Inspect);
        var result = f.Session.WaitForConditionAsync(f.Root.RootId, new() { Query = new() }).Completed();
        Assert.Equal(AutomationErrorCode.CapabilityDenied, result.Error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(60001)]
    public void InvalidTimeoutIsSafeFailure(int milliseconds)
    {
        using var f = new AutomationFixture();
        var result = f.Session.WaitForConditionAsync(f.Root.RootId, new() { Query = new() },
            new() { Timeout = TimeSpan.FromMilliseconds(milliseconds) }).Completed();
        Assert.Equal(AutomationErrorCode.InvalidArgument, result.Error);
    }

    [Fact]
    public void CheckpointAlwaysQueuesAndPreservesEarlierNormalPriorityWork()
    {
        using var f = new AutomationFixture(); bool earlierRan = false;
        f.Host.Dispatcher.Post(() => earlierRan = true);
        var checkpoint = f.Session.CheckpointAsync(); Assert.False(checkpoint.IsCompleted);
        Assert.Equal(AutomationErrorCode.None, Pump(f, checkpoint)); Assert.True(earlierRan);
    }

    private static Task<AutomationWaitResult> WaitValue(AutomationFixture f, SemanticControl c, CancellationToken token = default,
        TimeSpan? interval = null, TimeSpan? timeout = null)
        => f.Session.WaitForConditionAsync(f.Root.RootId,
            new() { Kind = AutomationWaitKind.ValueEquals, Handle = f.Handle(c.Child), Value = "Done" },
            new() { Timeout = timeout ?? TimeSpan.FromSeconds(5), ReconciliationInterval = interval ?? TimeSpan.FromMilliseconds(20) }, token);

    private static T Pump<T>(AutomationFixture f, Task<T> task)
    {
        // TestHost has a manually pumped dispatcher; bounded yielding drives asynchronous wakeups
        // without blocking that dispatcher or adding sleeps to the condition protocol.
        var timer = Stopwatch.StartNew();
        // Drain asserts global queue emptiness, which is intentionally not guaranteed while a
        // background continuation posts more work. Use its existing one-job test seam instead.
        var runOne = typeof(Dispatcher).GetMethod("RunOneJobForTesting", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<bool>>(Dispatcher.UIThread);
        while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(10)) { runOne(); Thread.Yield(); }
        Assert.True(task.IsCompleted); return task.GetAwaiter().GetResult();
    }

    private static void AssertNoObservation(AutomationFixture f, ScriptPeer peer)
    {
        // Test-only inspection verifies cleanup of the actual event subscriptions, not a surrogate counter.
        Assert.Null(typeof(AccessibleObject).GetField("ClientNotification", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(peer));
        Assert.Null(typeof(AutomationSession).GetField("lifetimeChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Session));
    }
}
