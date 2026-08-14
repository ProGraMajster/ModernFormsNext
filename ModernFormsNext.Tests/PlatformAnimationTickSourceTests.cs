using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class PlatformAnimationTickSourceTests
{
    [Fact]
    public void ActiveFallbackDemandPromotesAfterBackendRegistration()
    {
        var fallback = new ManualAnimationTickSource();
        TestPlatformFrameSource? registered = null;
        using var tickSource = new PlatformAnimationTickSource(fallback, () => registered);
        var clock = new ManualAnimationClock();
        using var scheduler = new AnimationScheduler(
            clock,
            new ImmediateAnimationDispatcher(),
            tickSource,
            new AnimationPolicy());
        float value = 0f;

        AnimationHandle handle = scheduler.Start(
            new object(),
            "LateBackend",
            progress => value = progress,
            new AnimationOptions { Duration = TimeSpan.FromMilliseconds(100) });

        Assert.True(fallback.IsRunning);
        registered = new TestPlatformFrameSource();

        // The next temporary fallback signal performs the handoff but is not delivered too, so
        // backend startup cannot create a duplicate scheduler tick.
        fallback.Fire();

        Assert.False(fallback.IsRunning);
        Assert.True(registered.IsCallbackPending);
        Assert.Equal(1, registered.StartCount);
        Assert.Equal(0f, value);

        clock.Advance(TimeSpan.FromMilliseconds(100));
        registered.Fire();

        Assert.Equal(1f, value);
        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.False(tickSource.IsRunning);
        Assert.False(registered.IsCallbackPending);
        Assert.Equal(1, registered.StopCount);
    }

    [Fact]
    public void BackendRegisteredBeforeFirstDemandBypassesFallback()
    {
        var fallback = new ManualAnimationTickSource();
        var registered = new TestPlatformFrameSource();
        using var source = new PlatformAnimationTickSource(fallback, () => registered);

        source.Start(static () => { });

        Assert.True(source.IsRunning);
        Assert.True(registered.IsCallbackPending);
        Assert.Equal(0, fallback.StartTransitions);

        source.Stop();

        Assert.False(source.IsRunning);
        Assert.False(registered.IsCallbackPending);
    }

    [Fact]
    public void RepeatedIdleWakeKeepsOnePlatformCallbackAndReleasesDelegateOnDispose()
    {
        var fallback = new ManualAnimationTickSource();
        var registered = new TestPlatformFrameSource();
        var source = new PlatformAnimationTickSource(fallback, () => registered);

        source.Start(static () => { });
        source.Start(static () => { });

        Assert.Equal(2, registered.StartCount);
        Assert.Equal(1, registered.MaximumPendingCount);

        source.Stop();
        source.Start(static () => { });
        source.Dispose();

        Assert.False(source.IsRunning);
        Assert.False(registered.IsCallbackPending);
        Assert.False(registered.HasCallback);
        Assert.Equal(0, fallback.StartTransitions);
        Assert.Throws<ObjectDisposedException>(() => source.Start(static () => { }));
    }

    [Fact]
    public void RejectedPlatformSourceFallsBackWithoutStrandingDemand()
    {
        var fallback = new ManualAnimationTickSource();
        var rejected = new TestPlatformFrameSource
        {
            StartException = new InvalidOperationException("rejected"),
            RetainCallbackBeforeStartFailure = true
        };
        using var source = new PlatformAnimationTickSource(fallback, () => rejected);
        int ticks = 0;

        source.Start(() => ticks++);
        fallback.Fire();

        Assert.True(source.IsRunning);
        Assert.True(fallback.IsRunning);
        Assert.Equal(1, rejected.StartCount);
        Assert.Equal(1, rejected.StopCount);
        Assert.False(rejected.HasCallback);
        Assert.Equal(1, ticks);
    }

    private sealed class TestPlatformFrameSource : IPlatformAnimationFrameSource
    {
        private Action? callback;

        public bool IsCallbackPending { get; private set; }

        public bool HasCallback => callback is not null;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public int MaximumPendingCount { get; private set; }

        public Exception? StartException { get; init; }

        public bool RetainCallbackBeforeStartFailure { get; init; }

        public void Start(Action frameRequested)
        {
            ArgumentNullException.ThrowIfNull(frameRequested);
            StartCount++;
            if (RetainCallbackBeforeStartFailure)
            {
                callback = frameRequested;
                IsCallbackPending = true;
            }
            if (StartException is not null)
                throw StartException;

            callback = frameRequested;
            IsCallbackPending = true;
            MaximumPendingCount = Math.Max(MaximumPendingCount, 1);
        }

        public void Stop()
        {
            if (!IsCallbackPending && callback is null)
                return;

            StopCount++;
            IsCallbackPending = false;
            callback = null;
        }

        public void Fire()
        {
            Action? requested = callback;
            IsCallbackPending = false;
            requested?.Invoke();
        }
    }
}
