using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class AnimationPlatformPolicyTests
{
    [Fact]
    public void StartupSnapshotUpdatesEffectiveReducedMotion()
    {
        var provider = new TestPlatformAnimationSettings(reducedMotion: true, animationsEnabled: false);

        using var harness = new AnimationSchedulerTestHarness(animationSettings: provider);

        Assert.True(harness.Policy.ReducedMotion);
        Assert.Equal(1, provider.SubscriberCount);
        Assert.Equal("Deterministic test provider", harness.Scheduler.GetPlatformDiagnostics().Source);
    }

    [Fact]
    public void LivePlatformChangeCompletesActiveAnimationAndReturnsSchedulerToIdle()
    {
        var provider = new TestPlatformAnimationSettings();
        using var harness = new AnimationSchedulerTestHarness(animationSettings: provider);
        float value = 0f;
        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "PlatformPolicy",
            progress => value = progress,
            new AnimationOptions { Duration = TimeSpan.FromSeconds(1) });

        provider.Set(reducedMotion: true, animationsEnabled: false);

        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.Equal(1f, value);
        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void FractionalPlatformScaleAppliesToNewAnimationsWithoutDisablingMotion()
    {
        var provider = new TestPlatformAnimationSettings(durationScale: 0.5d);
        using var harness = new AnimationSchedulerTestHarness(animationSettings: provider);

        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "ShortenedByPlatform",
            _ => { },
            new AnimationOptions { Duration = TimeSpan.FromMilliseconds(200) });

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(99));
        Assert.Equal(AnimationState.Running, handle.State);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(1));

        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.False(harness.Policy.ReducedMotion);
        Assert.Equal(0.5d, harness.Scheduler.GetPlatformDiagnostics().PlatformDurationScale);
    }

    [Fact]
    public void DynamicScaleZeroCompletesActiveAnimationAtEndpointAndStopsTicks()
    {
        var provider = new TestPlatformAnimationSettings();
        using var harness = new AnimationSchedulerTestHarness(animationSettings: provider);
        float value = 0f;
        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "DynamicScaleZero",
            progress => value = progress,
            new AnimationOptions { Duration = TimeSpan.FromSeconds(1) });
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        provider.Set(reducedMotion: true, animationsEnabled: false, durationScale: 0d);

        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.Equal(1f, value);
        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void ApplicationAndPlatformDurationScalesComposeForNewAnimations()
    {
        var provider = new TestPlatformAnimationSettings(durationScale: 0.5d);
        using var harness = new AnimationSchedulerTestHarness(animationSettings: provider);
        harness.Policy.DurationScale = 2d;

        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "ComposedScale",
            _ => { },
            new AnimationOptions { Duration = TimeSpan.FromMilliseconds(100) });
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(AnimationState.Completed, handle.State);
    }

    [Fact]
    public void RepeatedRefreshAndStartsDoNotDuplicateProviderSubscription()
    {
        var provider = new TestPlatformAnimationSettings();
        using var harness = new AnimationSchedulerTestHarness(animationSettings: provider);

        harness.Scheduler.RefreshPlatformPolicy();
        harness.Scheduler.RefreshPlatformPolicy();
        AnimationHandle handle = harness.Scheduler.Start(
            new object(),
            "OneSubscription",
            _ => { },
            new AnimationOptions { Duration = TimeSpan.FromSeconds(1) });

        Assert.Equal(1, provider.SubscriberCount);
        handle.Cancel();
    }

    [Fact]
    public void ShutdownRemovesProviderSubscription()
    {
        var provider = new TestPlatformAnimationSettings();
        var harness = new AnimationSchedulerTestHarness(animationSettings: provider);

        harness.Dispose();

        Assert.Equal(0, provider.SubscriberCount);
    }

    [Fact]
    public void MissingProviderUsesSafeEnabledFallback()
    {
        using var harness = new AnimationSchedulerTestHarness();

        AnimationPlatformDiagnostics diagnostics = harness.Scheduler.GetPlatformDiagnostics();

        Assert.False(harness.Policy.ReducedMotion);
        Assert.True(diagnostics.AnimationsEnabled);
        Assert.True(diagnostics.FallbackUsed);
        Assert.Equal(PlatformAnimationProviderState.Unavailable, diagnostics.ProviderState);
    }

    [Fact]
    public void DesignerModeHasNoProviderSubscriptionOrRefreshSideEffects()
    {
        var provider = new TestPlatformAnimationSettings(reducedMotion: true, animationsEnabled: false);
        using var harness = new AnimationSchedulerTestHarness(
            animationSettings: provider,
            isDesignMode: static () => true);

        harness.Scheduler.RefreshPlatformPolicy();
        AnimationPlatformDiagnostics diagnostics = harness.Scheduler.GetPlatformDiagnostics();

        Assert.Equal(0, provider.SubscriberCount);
        Assert.Equal(0, provider.RefreshCount);
        Assert.False(harness.Policy.ReducedMotion);
        Assert.Equal(PlatformAnimationProviderState.Disabled, diagnostics.ProviderState);
    }

    [Fact]
    public void PlatformChangeIsAppliedThroughUiDispatcher()
    {
        var dispatcher = new QueuedAnimationDispatcher();
        var provider = new TestPlatformAnimationSettings();
        using var harness = new AnimationSchedulerTestHarness(dispatcher, animationSettings: provider);
        dispatcher.Drain();

        var worker = new Thread(() => provider.Set(reducedMotion: true, animationsEnabled: false));
        worker.Start();
        worker.Join();

        Assert.False(harness.Policy.ReducedMotion);
        Assert.Equal(1, dispatcher.PendingCount);
        dispatcher.Drain();
        Assert.True(harness.Policy.ReducedMotion);
        Assert.Equal(Environment.CurrentManagedThreadId, dispatcher.ThreadId);
    }

    [Fact]
    public void ProviderInvokesCallbacksOutsideItsLock()
    {
        var provider = new TestPlatformAnimationSettings();
        bool? lockHeld = null;
        provider.Changed += (_, _) => lockHeld = provider.IsLockHeldByCurrentThread;

        provider.Set(reducedMotion: true, animationsEnabled: false);

        Assert.False(lockHeld);
    }

    [Fact]
    public void ForegroundTransitionRefreshesProviderAndAppliesLatestSnapshot()
    {
        var lifecycle = new TestPlatformApplicationLifecycle(PlatformApplicationLifecycleState.Background);
        var provider = new TestPlatformAnimationSettings();
        provider.SetOnNextRefresh(reducedMotion: true, animationsEnabled: false);
        using var harness = new AnimationSchedulerTestHarness(
            lifecycle: lifecycle,
            animationSettings: provider);

        lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);

        Assert.Equal(1, provider.RefreshCount);
        Assert.True(harness.Policy.ReducedMotion);
    }

    [Fact]
    public void DiagnosticsExposeFallbackStateAndError()
    {
        var provider = new TestPlatformAnimationSettings(
            providerState: PlatformAnimationProviderState.Fallback,
            fallbackUsed: true,
            lastError: "Native setting unavailable");
        using var harness = new AnimationSchedulerTestHarness(animationSettings: provider);

        AnimationPlatformDiagnostics diagnostics = harness.Scheduler.GetPlatformDiagnostics();

        Assert.True(diagnostics.FallbackUsed);
        Assert.Equal(PlatformAnimationProviderState.Fallback, diagnostics.ProviderState);
        Assert.Equal("Native setting unavailable", diagnostics.LastError);
        Assert.Equal(DateTimeOffset.UnixEpoch, diagnostics.LastPlatformUpdate);
    }

    [Fact]
    public void ApplicationAndPlatformReducedMotionPreferencesAreCombined()
    {
        var provider = new TestPlatformAnimationSettings(reducedMotion: true, animationsEnabled: false);
        using var harness = new AnimationSchedulerTestHarness(animationSettings: provider);

        harness.Policy.ReducedMotion = false;
        Assert.True(harness.Policy.ReducedMotion);
        Assert.False(harness.Policy.ApplicationReducedMotion);

        harness.Policy.ReducedMotion = true;
        Assert.True(harness.Policy.ApplicationReducedMotion);
        provider.Set(reducedMotion: false, animationsEnabled: true);
        Assert.True(harness.Policy.ReducedMotion);
    }
}
