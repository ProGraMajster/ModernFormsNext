using System.Drawing;
using System.Runtime.ExceptionServices;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Threading;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class TestClockTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(1);

    [Fact]
    public void StartsAtZeroAndIdleAdvanceDoesNotCreateAnimationFrames()
    {
        using var host = ModernFormsTestHost.Create();
        Assert.Equal(TimeSpan.Zero, host.Clock.CurrentTime);

        host.Clock.Advance(TimeSpan.FromHours(2));

        Assert.Equal(TimeSpan.FromHours(2), host.Clock.CurrentTime);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().TickCount);
        Assert.False(AnimationScheduler.Default.GetDiagnostics().IsTickSourceRunning);
    }

    [Fact]
    public void NegativeAndOverflowingAdvanceDoNotDrainOrChangeTime()
    {
        using var host = ModernFormsTestHost.Create();
        var ran = false;
        host.Dispatcher.Post(() => ran = true);

        Assert.Throws<ArgumentOutOfRangeException>(() => host.Clock.Advance(TimeSpan.FromTicks(-1)));
        Assert.False(ran);
        Assert.Equal(TimeSpan.Zero, host.Clock.CurrentTime);
        host.Clock.Advance(TimeSpan.MaxValue);
        ran = false;
        host.Dispatcher.Post(() => ran = true);

        Assert.Throws<OverflowException>(() => host.Clock.Advance(TimeSpan.FromTicks(1)));
        Assert.False(ran);
        Assert.Equal(TimeSpan.MaxValue, host.Clock.CurrentTime);
    }

    [Fact]
    public void RealDefaultSchedulerUsesExactIntermediateAndFinalTimes()
    {
        using var host = ModernFormsTestHost.Create();
        List<float> values = [];
        AnimationHandle handle = AnimationScheduler.Default.Start(new object(), "progress", values.Add, Linear());

        host.Clock.Advance(Duration / 4);
        host.Clock.Advance(Duration / 4);
        host.Clock.Advance(Duration / 2);

        Assert.Equal(new[] { .25f, .5f, 1f }, values);
        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.True(handle.Completion.IsCompletedSuccessfully);
        Assert.Equal(0, host.GetDiagnostics().ActiveAnimationCount);
        Assert.False(AnimationScheduler.Default.GetDiagnostics().IsTickSourceRunning);
    }

    [Fact]
    public void QueuedStartRunsAtOldTimeBeforeAdvanceCommits()
    {
        using var host = ModernFormsTestHost.Create();
        List<string> order = [];
        float value = -1;
        host.Dispatcher.Post(() =>
        {
            Assert.Equal(TimeSpan.Zero, host.Clock.CurrentTime);
            order.Add("start");
            AnimationScheduler.Default.Start(new object(), "queued", progress =>
            {
                value = progress;
                order.Add("frame");
                host.Dispatcher.Post(() => order.Add("after-frame"));
            }, Linear());
        });

        host.Clock.Advance(Duration / 2);

        Assert.Equal(.5f, value);
        Assert.Equal(new[] { "start", "frame", "after-frame" }, order);
    }

    [Fact]
    public void LargeAdvanceUsesOneDelayedFrameRatherThanSyntheticFrames()
    {
        using var host = ModernFormsTestHost.Create();
        List<float> values = [];
        AnimationScheduler.Default.Start(new object(), "delayed-frame", values.Add, Linear());

        host.Clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(1f, Assert.Single(values));
        Assert.Equal(1, AnimationScheduler.Default.GetDiagnostics().TickCount);
    }

    [Fact]
    public void DelayedAnimationBeginsOnlyAtItsDeadline()
    {
        using var host = ModernFormsTestHost.Create();
        List<float> values = [];
        AnimationOptions options = Linear();
        options.Delay = TimeSpan.FromMilliseconds(100);
        AnimationHandle handle = AnimationScheduler.Default.Start(new object(), "delay", values.Add, options);

        host.Clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.Empty(values);
        Assert.Equal(AnimationState.Delayed, handle.State);
        host.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(0f, Assert.Single(values));
        host.Clock.Advance(Duration);
        Assert.Equal(AnimationState.Completed, handle.State);
    }

    [Fact]
    public void ExplicitZeroAdvanceRunsOneFrameWithoutChangingTime()
    {
        using var host = ModernFormsTestHost.Create();
        List<float> values = [];
        AnimationScheduler.Default.Start(new object(), "zero", values.Add, Linear());

        host.Clock.Advance(TimeSpan.Zero);

        Assert.Equal(0f, Assert.Single(values));
        Assert.Equal(TimeSpan.Zero, host.Clock.CurrentTime);
    }

    [Fact]
    public void ReplacementAndCancelUseProductionSchedulerOwnership()
    {
        using var host = ModernFormsTestHost.Create();
        object owner = new();
        float value = 0;
        AnimationHandle first = AnimationScheduler.Default.Animate(owner, "value", 0f, 1f,
            AnimationInterpolators.Float, next => value = next, Linear());
        host.Clock.Advance(Duration / 2);
        AnimationHandle second = AnimationScheduler.Default.Animate(owner, "value", value, 0f,
            AnimationInterpolators.Float, next => value = next, Linear());

        host.Clock.Advance(Duration / 2);
        Assert.Equal(AnimationState.Canceled, first.State);
        Assert.Equal(.25f, value);
        second.Cancel();
        host.Clock.Advance(Duration);

        Assert.Equal(.25f, value);
        Assert.Equal(AnimationState.Canceled, second.State);
        Assert.Equal(0, host.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void ProductionLayoutTransitionUsesHostClockAndCanonicalAccessibilityBounds()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var child = new Control { Bounds = new Rectangle(10, 20, 40, 30) };
        root.Controls.Add(child);
        host.Show(root, 300, 200);
        child.LayoutTransition = new LayoutTransition { Duration = Duration, Easing = Easings.Linear };
        Rectangle oldAccessible = child.AccessibilityObject.Bounds;

        child.Left += 100;
        Assert.Equal(110, child.Left);
        Assert.Equal(oldAccessible, child.AccessibilityObject.Bounds);
        host.Clock.Advance(Duration / 2);
        Assert.Equal(oldAccessible.X + 50, child.AccessibilityObject.Bounds.X);
        host.Clock.Advance(Duration / 2);
        Assert.Equal(oldAccessible.X + 100, child.AccessibilityObject.Bounds.X);
    }

    [Fact]
    public void LifecycleBackgroundTimeIsExcludedByTheProductionScheduler()
    {
        using var host = ModernFormsTestHost.Create();
        float value = 0;
        AnimationHandle handle = AnimationScheduler.Default.Start(new object(), "lifecycle", next => value = next, Linear());
        host.Clock.Advance(Duration / 4);

        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background);
        host.Clock.Advance(TimeSpan.FromHours(3));
        Assert.Equal(.25f, value);
        Assert.Equal(AnimationState.Paused, handle.State);
        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);
        host.Clock.Advance(Duration / 4);

        Assert.Equal(.5f, value);
        Assert.Equal(TimeSpan.FromHours(3) + Duration / 2, host.Clock.CurrentTime);
    }

    [Fact]
    public void ReducedMotionCompletesUsingProductionPolicyWithoutClockAdvancement()
    {
        using var host = ModernFormsTestHost.Create();
        float value = 0;
        AnimationHandle handle = AnimationScheduler.Default.Start(new object(), "motion", next => value = next, Linear());

        AnimationScheduler.Default.Policy.ReducedMotion = true;

        Assert.Equal(1f, value);
        Assert.Equal(TimeSpan.Zero, host.Clock.CurrentTime);
        Assert.Equal(AnimationState.Completed, handle.State);
    }

    [Fact]
    public void BackgroundStartMarshalsCallbackToTheHostUiThread()
    {
        using var host = ModernFormsTestHost.Create();
        int ownerThread = Environment.CurrentManagedThreadId;
        int callbackThread = 0;
        AnimationScheduler scheduler = AnimationScheduler.Default;
        AnimationHandle? handle = null;
        RunOnBackgroundThread(() =>
        {
            Assert.Same(scheduler, AnimationScheduler.Default);
            handle = AnimationScheduler.Default.Start(new object(), "worker", _ => callbackThread = Environment.CurrentManagedThreadId, Linear());
        });

        Assert.Equal(0, callbackThread);
        host.Clock.Advance(Duration);

        Assert.Equal(ownerThread, callbackThread);
        Assert.Equal(AnimationState.Completed, handle!.State);
    }

    [Fact]
    public void BackgroundAdvanceIsRejectedWithoutChangingTime()
    {
        using var host = ModernFormsTestHost.Create();

        RunOnBackgroundThread(() => Assert.Throws<InvalidOperationException>(() => host.Clock.Advance(Duration)));

        Assert.Equal(TimeSpan.Zero, host.Clock.CurrentTime);
    }

    [Fact]
    public void ReentrantAdvanceFaultsOnlyItsAnimationAndReleasesTheGuard()
    {
        using var host = ModernFormsTestHost.Create();
        AnimationHandle handle = AnimationScheduler.Default.Start(new object(), "recursive",
            _ => host.Clock.Advance(Duration), Linear());

        host.Clock.Advance(Duration / 2);

        Assert.Equal(AnimationState.Faulted, handle.State);
        Assert.IsType<InvalidOperationException>(handle.Exception);
        Assert.Equal(Duration / 2, host.Clock.CurrentTime);
        host.Clock.Advance(Duration / 2);
        Assert.Equal(Duration, host.Clock.CurrentTime);
    }

    [Fact]
    public void AdvanceRetainsPostedFailureInDispatcherDiagnostics()
    {
        using var host = ModernFormsTestHost.Create();
        host.Dispatcher.Post(() => throw new TestFailureException());

        host.Clock.Advance(Duration);

        Assert.IsType<TestFailureException>(Assert.Single(host.Dispatcher.UnhandledExceptions));
        Assert.Equal(Duration, host.Clock.CurrentTime);
    }

    [Fact]
    public void ProductionTimerWaitsForClockAndCoalescesMissedPeriods()
    {
        using var host = ModernFormsTestHost.Create();
        List<TimeSpan> ticks = [];
        using var timer = new ModernFormsNext.Timer { Interval = 100 };
        timer.Tick += (_, _) => ticks.Add(host.Clock.CurrentTime);
        timer.Start();

        Assert.Equal(0, host.Dispatcher.Drain());
        host.Clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.Empty(ticks);
        host.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(TimeSpan.FromMilliseconds(100), Assert.Single(ticks));
        host.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1100) }, ticks);
    }

    [Fact]
    public void TimerPriorityIsPreservedAndSchedulerFrameRunsBeforePromotedCallbacks()
    {
        using var host = ModernFormsTestHost.Create();
        List<string> order = [];
        using var background = DispatcherTimer.RunOnce(() => order.Add("background"), Duration, DispatcherPriority.Background);
        using var send = DispatcherTimer.RunOnce(() => order.Add("send"), Duration, DispatcherPriority.Send);
        AnimationScheduler.Default.Start(new object(), "frame", _ => order.Add("frame"), Linear());

        host.Clock.Advance(Duration);

        Assert.Equal(new[] { "frame", "send", "background" }, order);
    }

    [Fact]
    public void ExactDrainBudgetDoesNotFailBecauseAFutureTimerIsDormant()
    {
        using var host = ModernFormsTestHost.Create();
        using var timer = DispatcherTimer.RunOnce(() => throw new TestFailureException(), Duration);
        int calls = 0;
        for (int i = 0; i < 4096; i++)
            host.Dispatcher.Post(() => calls++);

        Assert.Equal(4096, host.Dispatcher.Drain());
        Assert.Equal(4096, calls);
        Assert.Equal(0, host.Dispatcher.Drain());
        Assert.False(host.Dispatcher.HasReadyWork);
    }

    [Fact]
    public void ZeroIntervalTimerFailsBoundedlyAndCanBeStoppedForRecovery()
    {
        using var host = ModernFormsTestHost.Create();
        using var timer = DispatcherTimer.Run(() => true, TimeSpan.Zero);

        Assert.Throws<InvalidOperationException>(() => host.Clock.Advance(Duration));
        Assert.Equal(TimeSpan.Zero, host.Clock.CurrentTime);
        timer.Dispose();
        host.Clock.Advance(Duration);
        Assert.Equal(Duration, host.Clock.CurrentTime);
    }

    [Fact]
    public void DisposalStopsDormantProductionTimers()
    {
        var host = ModernFormsTestHost.Create();
        var timer = new DispatcherTimer { Interval = Duration };
        timer.Start();

        host.Dispose();

        Assert.False(timer.IsEnabled);
        using var next = ModernFormsTestHost.Create();
        next.Clock.Advance(Duration);
        Assert.Empty(next.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void DisposalStopsTimerCreatedDuringItsFinalDispatcherDrain()
    {
        var dispatcher = new UiTestDispatcher();
        DispatcherTimer? timer = null;
        dispatcher.Post(() =>
        {
            timer = new DispatcherTimer { Interval = Duration };
            timer.Start();
        });

        dispatcher.Dispose();

        Assert.NotNull(timer);
        Assert.False(timer.IsEnabled);
        using var next = ModernFormsTestHost.Create();
        next.Clock.Advance(Duration);
        Assert.Empty(next.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void ScopeRestoresPreviousDefaultAndCapturedContextsCannotResurrectIt()
    {
        AnimationScheduler previous = AnimationScheduler.Default;
        ExecutionContext? captured;
        AnimationScheduler temporary;
        TestClock clock;
        AnimationHandle animation;
        using (var host = ModernFormsTestHost.Create())
        {
            temporary = AnimationScheduler.Default;
            clock = host.Clock;
            Assert.NotSame(previous, temporary);
            captured = ExecutionContext.Capture();
            animation = temporary.Start(new object(), "cleanup", _ => { }, Linear());
        }

        Assert.Same(previous, AnimationScheduler.Default);
        Assert.True(temporary.GetDiagnostics().IsShutdown);
        Assert.Equal(AnimationState.Canceled, animation.State);
        Assert.Throws<ObjectDisposedException>(() => clock.Advance(Duration));
        Assert.Throws<ObjectDisposedException>(() => clock.CurrentTime);
        ExecutionContext.Run(captured!, _ => Assert.Same(previous, AnimationScheduler.Default), null);
    }

    [Fact]
    public void FailingHostDrainStillRestoresDefaultScheduler()
    {
        AnimationScheduler previous = AnimationScheduler.Default;
        var host = ModernFormsTestHost.Create();
        AnimationScheduler temporary = AnimationScheduler.Default;
        Action? repeat = null;
        repeat = () => host.Dispatcher.Post(repeat!);
        host.Dispatcher.Post(repeat);

        Assert.Throws<AggregateException>(host.Dispose);

        Assert.Same(previous, AnimationScheduler.Default);
        Assert.True(temporary.GetDiagnostics().IsShutdown);
        using var next = ModernFormsTestHost.Create();
        next.Clock.Advance(Duration);
    }

    [Fact]
    public void SequentialHostsHaveIndependentClockAndPolicy()
    {
        using (var first = ModernFormsTestHost.Create())
        {
            first.Clock.Advance(Duration);
            AnimationScheduler.Default.Policy.AnimationsEnabled = false;
        }

        using var next = ModernFormsTestHost.Create();

        Assert.Equal(TimeSpan.Zero, next.Clock.CurrentTime);
        Assert.True(AnimationScheduler.Default.Policy.AnimationsEnabled);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().TickCount);
    }

    [Fact]
    public void PrewarmedApplicationThemeManagerUsesTheCurrentHostClock()
    {
        ThemeManager manager = ThemeManager.Current;
        using var host = ModernFormsTestHost.Create();

        VerifyThemeTransitionUsesClock(host, manager);
    }

    [Fact]
    public void ApplicationThemeManagerUsesNewClockAcrossSequentialHosts()
    {
        // Run this test alone in a fresh process to cover a singleton first initialized inside
        // the first host. A full-suite run also covers the already-initialized singleton case.
        for (int index = 0; index < 2; index++)
        {
            using var host = ModernFormsTestHost.Create();
            VerifyThemeTransitionUsesClock(host, ThemeManager.Current);
        }
    }

    [Fact]
    public void OldThemeCompletionCannotOverwriteImmediateReplacementInTheSameFrame()
    {
        using var host = ModernFormsTestHost.Create();
        ThemeManager manager = ThemeManager.Current;
        ThemeDefinition from = BuiltInThemes.Light;
        from.Colors[ThemeTokens.Colors.Primary.Name] = Color.Black;
        Assert.True(manager.Apply(from).Success);
        ThemeDefinition target = BuiltInThemes.Light;
        target.Colors[ThemeTokens.Colors.Primary.Name] = Color.White;
        ThemeApplyResult transition = manager.Apply(target, new ThemeApplyOptions
        {
            Transition = new ThemeTransitionOptions { Enabled = true, Duration = Duration, Easing = ThemeEasing.Linear }
        });
        Assert.NotNull(transition.Transition);
        ThemeDefinition replacement = BuiltInThemes.Light;
        replacement.Colors[ThemeTokens.Colors.Primary.Name] = Color.Red;
        AnimationScheduler.Default.Start(new object(), "replace-after-theme-frame",
            _ => Assert.True(manager.Apply(replacement).Success), Linear());

        host.Clock.Advance(Duration);

        Assert.Equal(Color.Red.ToArgb(), Assert.IsType<Color>(Application.ThemeResources[ThemeTokens.Colors.Primary.ResourceKey]).ToArgb());
        Assert.Equal(Color.Red.ToArgb(), manager.ActiveSnapshot!.Get(ThemeTokens.Colors.Primary).ToArgb());
        Assert.True(transition.Transition!.Completion.IsCompletedSuccessfully);
    }

    private static void VerifyThemeTransitionUsesClock(ModernFormsTestHost host, ThemeManager manager)
    {
        ThemeDefinition from = BuiltInThemes.Light;
        from.Colors[ThemeTokens.Colors.Primary.Name] = Color.Black;
        Assert.True(manager.Apply(from).Success);
        ThemeDefinition to = BuiltInThemes.Light;
        to.Colors[ThemeTokens.Colors.Primary.Name] = Color.White;
        ThemeApplyResult result = manager.Apply(to, new ThemeApplyOptions
        {
            Transition = new ThemeTransitionOptions { Enabled = true, Duration = Duration, Easing = ThemeEasing.Linear }
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Transition);
        Assert.Equal(1, host.GetDiagnostics().ActiveAnimationCount);
        host.Clock.Advance(Duration / 2);
        Color midpoint = Assert.IsType<Color>(Application.ThemeResources[ThemeTokens.Colors.Primary.ResourceKey]);
        Assert.InRange(midpoint.R, 127, 128);
        host.Clock.Advance(Duration / 2);
        Assert.Equal(Color.White.ToArgb(), Assert.IsType<Color>(Application.ThemeResources[ThemeTokens.Colors.Primary.ResourceKey]).ToArgb());
        Assert.Equal(0, host.GetDiagnostics().ActiveAnimationCount);
        Assert.True(result.Transition!.Completion.IsCompletedSuccessfully);
        // Framework terminal bookkeeping drains deterministically; arbitrary application Task
        // continuations awaiting this public completion still execute asynchronously.
    }

    private static AnimationOptions Linear() => new() { Duration = Duration, Easing = Easings.Linear };

    private static void RunOnBackgroundThread(Action action)
    {
        // A dedicated thread makes thread-affinity observable without awaiting back onto an
        // arbitrary xUnit worker. The action must never wait for the host dispatcher to drain.
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        });
        thread.Start();
        thread.Join();
        failure?.Throw();
    }

    private sealed class TestFailureException : Exception { }
}
