using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class AnimationSchedulerTests
{
    [Fact]
    public void SchedulerRemainsIdleUntilAnAnimationNeedsTicks()
    {
        using var harness = new AnimationSchedulerTestHarness();

        AnimationSchedulerDiagnostics diagnostics = harness.Scheduler.GetDiagnostics();

        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.TickSource.StartTransitions);
        Assert.Equal(0, diagnostics.ActiveAnimationCount);
        Assert.Equal(0, diagnostics.TickCount);
    }

    [Fact]
    public async Task ProgressUsesElapsedTimeAndStopsAfterTheLastAnimation()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var values = new List<float>();
        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "Progress",
            values.Add,
            Options(100));

        harness.TickSource.Fire();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(75));

        Assert.Equal([0f, 0.25f, 1f], values);
        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.Equal(AnimationState.Completed, await handle.Completion);
        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(1, harness.TickSource.StartTransitions);
        Assert.Equal(1, harness.TickSource.StopTransitions);
    }

    [Fact]
    public void ADelayedDispatcherFrameDoesNotExtendDuration()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var values = new List<float>();
        AnimationHandle handle = harness.Scheduler.Start(new object(), "Jump", values.Add, Options(100));

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(275));

        Assert.Equal([1f], values);
        Assert.Equal(AnimationState.Completed, handle.State);
    }

    [Fact]
    public void DelayDefersUpdatesButStillUsesTheSameMonotonicTimeline()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var values = new List<float>();
        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "Delayed",
            values.Add,
            new AnimationOptions
            {
                Delay = TimeSpan.FromMilliseconds(50),
                Duration = TimeSpan.FromMilliseconds(100)
            });

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(49));
        Assert.Empty(values);
        Assert.Equal(AnimationState.Delayed, handle.State);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(1));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal([0f, 0.5f, 1f], values);
        Assert.Equal(AnimationState.Completed, handle.State);
    }

    [Fact]
    public async Task CancellationBeforeFirstUpdateAndDuringDelayNeverReportsCompletion()
    {
        using var harness = new AnimationSchedulerTestHarness();
        int updates = 0;
        AnimationHandle beforeFirstUpdate = harness.Scheduler.Start(
            new object(),
            "BeforeFirstUpdate",
            _ => updates++,
            Options(100));
        AnimationHandle duringDelay = harness.Scheduler.Start(
            new object(),
            "DuringDelay",
            _ => updates++,
            new AnimationOptions
            {
                Delay = TimeSpan.FromMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(100)
            });

        beforeFirstUpdate.Cancel();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        duringDelay.Cancel();
        harness.AdvanceAndTick(TimeSpan.FromSeconds(1));

        Assert.Equal(0, updates);
        Assert.Equal(AnimationState.Canceled, await beforeFirstUpdate.Completion);
        Assert.Equal(AnimationState.Canceled, await duringDelay.Completion);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void CancelAfterCompletionDoesNotChangeTerminalStateOrCounters()
    {
        using var harness = new AnimationSchedulerTestHarness();
        AnimationHandle handle = harness.Scheduler.Start(new object(), "Completed", _ => { }, Options(100));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        handle.Cancel();

        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().CompletedCount);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().CanceledCount);
    }

    [Fact]
    public void ZeroDurationCompletesOnDispatcherWithoutStartingTickSource()
    {
        var dispatcher = new QueuedAnimationDispatcher();
        using var harness = new AnimationSchedulerTestHarness(dispatcher);
        float value = -1f;

        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "Immediate",
            progress => value = progress,
            Options(0));

        Assert.Equal(AnimationState.Running, handle.State);
        Assert.Equal(1, dispatcher.PendingCount);
        Assert.False(harness.TickSource.IsRunning);

        dispatcher.Drain();

        Assert.Equal(1f, value);
        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.Equal(0, harness.TickSource.StartTransitions);
    }

    [Fact]
    public async Task StartFromBackgroundMarshalsAllUpdatesToUiDispatcher()
    {
        var dispatcher = new QueuedAnimationDispatcher();
        using var harness = new AnimationSchedulerTestHarness(dispatcher);
        var updateThreads = new List<int>();

        AnimationHandle handle = await Task.Run(() => harness.Scheduler.Start(
            new object(),
            "BackgroundStart",
            _ => updateThreads.Add(Environment.CurrentManagedThreadId),
            Options(100)));

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Empty(updateThreads);
        Assert.Equal(1, dispatcher.PendingCount);

        dispatcher.Drain();

        Assert.Equal([dispatcher.ThreadId], updateThreads);
        Assert.Equal(AnimationState.Completed, handle.State);
    }

    [Fact]
    public void ReplacementCancelsOnlyTheMatchingOwnerAndKey()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var owner = new object();
        var otherOwner = new object();
        AnimationHandle replaced = harness.Scheduler.Start(owner, "Opacity", _ => { }, Options(100));
        AnimationHandle separateKey = harness.Scheduler.Start(owner, "Position", _ => { }, Options(100));
        AnimationHandle separateOwner = harness.Scheduler.Start(otherOwner, "Opacity", _ => { }, Options(100));

        AnimationHandle replacement = harness.Scheduler.Start(owner, "Opacity", _ => { }, Options(100));

        Assert.Equal(AnimationState.Canceled, replaced.State);
        Assert.Equal(AnimationState.Running, replacement.State);
        Assert.Equal(AnimationState.Running, separateKey.State);
        Assert.Equal(AnimationState.Running, separateOwner.State);
        Assert.Equal(3, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void IgnoreNewReturnsExistingHandleWithoutReplacingIt()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var owner = new object();
        AnimationHandle existing = harness.Scheduler.Start(owner, "Channel", _ => { }, Options(100));

        AnimationHandle ignored = harness.Scheduler.Start(
            owner,
            "Channel",
            _ => throw new InvalidOperationException("The ignored callback must not run."),
            new AnimationOptions
            {
                Duration = TimeSpan.FromMilliseconds(100),
                ReplacementMode = AnimationReplacementMode.IgnoreNew
            });

        Assert.Same(existing, ignored);
        Assert.Equal(AnimationState.Running, existing.State);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void OwnerCancellationSuppressesPolicyCompletionAlreadyQueuedToDispatcher()
    {
        var dispatcher = new QueuedAnimationDispatcher();
        using var harness = new AnimationSchedulerTestHarness(dispatcher);
        var owner = new object();
        int updates = 0;
        AnimationHandle handle = harness.Scheduler.Start(
            owner,
            "PolicyCompletion",
            _ => updates++,
            Options(100));

        harness.Policy.ReducedMotion = true;
        harness.Scheduler.CancelAll(owner);
        dispatcher.Drain();

        Assert.Equal(AnimationState.Canceled, handle.State);
        Assert.Equal(0, updates);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void CancelAndCancelAllAreIdempotentAndLeaveOtherOwnersRunning()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var owner = new object();
        var otherOwner = new object();
        AnimationHandle first = harness.Scheduler.Start(owner, "First", _ => { }, Options(100));
        AnimationHandle second = harness.Scheduler.Start(owner, "Second", _ => { }, Options(100));
        AnimationHandle other = harness.Scheduler.Start(otherOwner, "First", _ => { }, Options(100));

        first.Cancel();
        first.Cancel();
        harness.Scheduler.CancelAll(owner);
        harness.Scheduler.CancelAll(owner);

        Assert.Equal(AnimationState.Canceled, first.State);
        Assert.Equal(AnimationState.Canceled, second.State);
        Assert.Equal(AnimationState.Running, other.State);
        Assert.Equal(2, harness.Scheduler.GetDiagnostics().CanceledCount);
        Assert.True(harness.TickSource.IsRunning);
    }

    [Fact]
    public void GlobalPauseExcludesBackgroundTimeFromProgress()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var values = new List<float>();
        AnimationHandle handle = harness.Scheduler.Start(new object(), "Pause", values.Add, Options(100));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(40));

        harness.Scheduler.Pause();
        harness.Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(AnimationState.Paused, handle.State);
        Assert.False(harness.TickSource.IsRunning);

        harness.Scheduler.Resume();
        harness.TickSource.Fire();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(60));

        Assert.Equal([0.4f, 0.4f, 1f], values);
        Assert.Equal(AnimationState.Completed, handle.State);
    }

    [Fact]
    public void PlatformBackgroundLifecyclePausesTimeAndShutdownUnsubscribes()
    {
        var lifecycle = new TestPlatformApplicationLifecycle(PlatformApplicationLifecycleState.Foreground);
        var harness = new AnimationSchedulerTestHarness(lifecycle: lifecycle);
        var values = new List<float>();
        AnimationHandle handle = harness.Scheduler.Start(new object(), "Lifecycle", values.Add, Options(100));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(40));

        lifecycle.SetState(PlatformApplicationLifecycleState.Background);
        harness.Clock.Advance(TimeSpan.FromSeconds(20));

        Assert.Equal(AnimationState.Paused, handle.State);
        Assert.False(harness.TickSource.IsRunning);

        lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);
        harness.TickSource.Fire();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(60));

        Assert.Equal([0.4f, 0.4f, 1f], values);
        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.Equal(1, lifecycle.SubscriberCount);

        harness.Dispose();
        Assert.Equal(0, lifecycle.SubscriberCount);
    }

    [Fact]
    public void IndividualPauseFreezesOnlyThatHandle()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var pausedValues = new List<float>();
        var runningValues = new List<float>();
        AnimationHandle paused = harness.Scheduler.Start(new object(), "Paused", pausedValues.Add, Options(100));
        AnimationHandle running = harness.Scheduler.Start(new object(), "Running", runningValues.Add, Options(100));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(40));

        paused.Pause();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(60));

        Assert.Equal(AnimationState.Paused, paused.State);
        Assert.Equal(AnimationState.Completed, running.State);

        paused.Resume();
        harness.TickSource.Fire();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(60));

        Assert.Equal([0.4f, 0.4f, 1f], pausedValues);
        Assert.Equal([0.4f, 1f], runningValues);
        Assert.Equal(AnimationState.Completed, paused.State);
    }

    [Fact]
    public void CallbackCanStartAndCancelAnimationsDuringATick()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var events = new List<string>();
        AnimationHandle? second = null;
        bool started = false;
        harness.Scheduler.Start(
            new object(),
            "First",
            _ =>
            {
                events.Add("first");
                if (started)
                    return;
                started = true;
                second = harness.Scheduler.Start(new object(), "Second", _ => events.Add("second"), Options(100));
            },
            Options(100));
        AnimationHandle canceledDuringTick = harness.Scheduler.Start(
            new object(),
            "Canceled",
            _ => events.Add("canceled callback"),
            Options(100));
        harness.Scheduler.Start(
            new object(),
            "Canceler",
            _ => canceledDuringTick.Cancel(),
            Options(100));

        harness.TickSource.Fire();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.NotNull(second);
        Assert.Contains("second", events);
        Assert.Equal(AnimationState.Canceled, canceledDuringTick.State);
        Assert.Equal(1, events.Count(item => item == "canceled callback"));
    }

    [Fact]
    public void OneFaultDoesNotPreventOtherAnimationsFromCompleting()
    {
        using var harness = new AnimationSchedulerTestHarness();
        AnimationHandle faulted = harness.Scheduler.Start(
            new object(),
            "Fault",
            _ => throw new InvalidOperationException("Expected test fault."),
            Options(100));
        float completedValue = 0f;
        AnimationHandle completed = harness.Scheduler.Start(
            new object(),
            "Healthy",
            value => completedValue = value,
            Options(100));

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(AnimationState.Faulted, faulted.State);
        Assert.IsType<InvalidOperationException>(faulted.Exception);
        Assert.Equal(AnimationState.Completed, completed.State);
        Assert.Equal(1f, completedValue);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().FaultedCount);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().CompletedCount);
    }

    [Fact]
    public void InvalidEasingResultFaultsOnlyItsAnimation()
    {
        using var harness = new AnimationSchedulerTestHarness();
        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "NaN",
            _ => { },
            new AnimationOptions
            {
                Duration = TimeSpan.FromMilliseconds(100),
                Easing = _ => float.NaN
            });

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(AnimationState.Faulted, handle.State);
        Assert.IsType<InvalidOperationException>(handle.Exception);
    }

    [Fact]
    public void ThrowingEasingIsCapturedOnTheFaultedHandle()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var expected = new ArithmeticException("Expected easing failure.");
        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "ThrowingEasing",
            _ => { },
            new AnimationOptions
            {
                Duration = TimeSpan.FromMilliseconds(100),
                Easing = _ => throw expected
            });

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(AnimationState.Faulted, handle.State);
        Assert.Same(expected, handle.Exception);
    }

    [Fact]
    public void FiniteOvershootFromCustomEasingIsPassedThrough()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var values = new List<float>();
        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "Overshoot",
            values.Add,
            new AnimationOptions
            {
                Duration = TimeSpan.FromMilliseconds(100),
                Easing = _ => 1.25f
            });

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal([1.25f, 1f], values);
        Assert.Equal(AnimationState.Completed, handle.State);
    }

    [Theory]
    [InlineData(0.5, 50)]
    [InlineData(1.0, 100)]
    [InlineData(2.0, 200)]
    public void DurationScaleIsCapturedForNewAnimations(double scale, int expectedMilliseconds)
    {
        using var harness = new AnimationSchedulerTestHarness();
        harness.Policy.DurationScale = scale;
        AnimationHandle handle = harness.Scheduler.Start(new object(), "Scale", _ => { }, Options(100));

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(expectedMilliseconds - 1));
        Assert.Equal(AnimationState.Running, handle.State);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(1));
        Assert.Equal(AnimationState.Completed, handle.State);
    }

    [Theory]
    [InlineData(false, false, 0.0)]
    [InlineData(false, false, 1.0)]
    [InlineData(true, true, 1.0)]
    public void PolicyCompletesWithoutTicksWhenMotionIsDisabled(
        bool animationsEnabled,
        bool reducedMotion,
        double durationScale)
    {
        using var harness = new AnimationSchedulerTestHarness();
        harness.Policy.AnimationsEnabled = animationsEnabled;
        harness.Policy.ReducedMotion = reducedMotion;
        harness.Policy.DurationScale = durationScale;
        float value = 0f;

        AnimationHandle handle = harness.Scheduler.Start(new object(), "Policy", result => value = result, Options(100));

        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.Equal(1f, value);
        Assert.Equal(0, harness.TickSource.StartTransitions);
    }

    [Fact]
    public void EnablingReducedMotionCompletesActiveAnimationsAtFinalValue()
    {
        using var harness = new AnimationSchedulerTestHarness();
        float value = 0f;
        AnimationHandle handle = harness.Scheduler.Start(new object(), "PolicyChange", result => value = result, Options(100));

        harness.Policy.ReducedMotion = true;

        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.Equal(1f, value);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void ManyAnimationsShareOneTickSourceAndKeepRegistrationOrder()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var order = new List<int>();
        for (int index = 0; index < 128; index++)
        {
            int captured = index;
            harness.Scheduler.Start(new object(), "Parallel", _ => order.Add(captured), Options(100));
        }

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(Enumerable.Range(0, 128), order);
        Assert.Equal(1, harness.TickSource.StartTransitions);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().TickCount);
        Assert.Equal(128, harness.Scheduler.GetDiagnostics().CompletedCount);
    }

    [Fact]
    public void ShutdownCancelsEntriesDisposesTickSourceAndCannotRestart()
    {
        var harness = new AnimationSchedulerTestHarness();
        AnimationHandle handle = harness.Scheduler.Start(new object(), "Shutdown", _ => { }, Options(100));

        harness.Scheduler.Shutdown();
        harness.Scheduler.Shutdown();

        Assert.Equal(AnimationState.Canceled, handle.State);
        Assert.True(harness.TickSource.IsDisposed);
        Assert.True(harness.Scheduler.GetDiagnostics().IsShutdown);
        Assert.Throws<ObjectDisposedException>(() =>
            harness.Scheduler.Start(new object(), "AfterShutdown", _ => { }, Options(100)));
        harness.Dispose();
    }

    [Fact]
    public void OptionsAndPolicyRejectInvalidTimeConfiguration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationOptions { Duration = TimeSpan.FromTicks(-1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationOptions { Delay = TimeSpan.FromTicks(-1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationPolicy { DurationScale = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationPolicy { DurationScale = -0.1d });
    }

    private static AnimationOptions Options(int durationMilliseconds)
        => new() { Duration = TimeSpan.FromMilliseconds(durationMilliseconds) };
}
