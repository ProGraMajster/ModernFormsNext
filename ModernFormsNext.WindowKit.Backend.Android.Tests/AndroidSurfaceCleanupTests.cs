using ModernFormsNext.WindowKit.Backend.Android.Rendering;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidSurfaceCleanupTests
{
    [Theory]
    [InlineData(AndroidSurfaceLifecycleState.Paused)]
    [InlineData(AndroidSurfaceLifecycleState.Stopped)]
    [InlineData(AndroidSurfaceLifecycleState.Resumed)] // Temporary native surface detach.
    [InlineData(AndroidSurfaceLifecycleState.Disposed)]
    public void CancellationFailureCannotSkipOtherPointersOrMandatorySurfaceCleanup(AndroidSurfaceLifecycleState transition)
    {
        var state = new AndroidSurfaceHostState();
        state.AttachSurface();
        state.Resume();
        state.TrackPointer(3, AndroidPointerAction.Down, isPrimary: true);
        state.TrackPointer(7, AndroidPointerAction.Down);
        int? primary = state.PrimaryPointerId;
        IReadOnlyList<int> cancellations = transition switch
        {
            AndroidSurfaceLifecycleState.Paused => state.Pause(),
            AndroidSurfaceLifecycleState.Stopped => state.Stop(),
            AndroidSurfaceLifecycleState.Disposed => state.Dispose(),
            _ => state.DetachSurface()
        };
        var delivered = new List<int>();
        bool reconciled = false;
        bool nativeReleased = false;
        EventHandler<AndroidPointerEvent> handlers = (_, _) => throw new InvalidOperationException("pointer observer");
        handlers += (_, args) =>
        {
            Assert.Equal(0, state.ActivePointerCount);
            Assert.Equal(transition, state.LifecycleState);
            Assert.Equal(AndroidPointerAction.Cancel, args.Action);
            Assert.Equal(args.PointerId == 3, args.IsPrimary);
            delivered.Add(args.PointerId);
        };

        var failure = Assert.Throws<AggregateException>(() => AndroidSurfaceCleanup.Complete(
            () => AndroidSurfaceCleanup.CancelPointers(state, handlers, cancellations, primary),
            () => { reconciled = !state.CanRender; },
            () => throw new ArgumentException("another cleanup failure"),
            () => nativeReleased = true));

        Assert.Equal(new[] { 3, 7 }, delivered);
        Assert.True(reconciled);
        Assert.True(nativeReleased);
        Assert.Equal(3, failure.Flatten().InnerExceptions.Count);
    }

    [Fact]
    public void ReentrantDisposalDuringPauseStillCompletesCancellationForEverySubscriber()
    {
        var state = new AndroidSurfaceHostState();
        state.AttachSurface();
        state.Resume();
        state.TrackPointer(1, AndroidPointerAction.Down);
        state.TrackPointer(2, AndroidPointerAction.Down);
        IReadOnlyList<int> cancellations = state.Pause();
        int delivered = 0;
        EventHandler<AndroidPointerEvent> handlers = (_, _) => state.Dispose();
        handlers += (_, _) => delivered++;
        AndroidSurfaceCleanup.Complete(
            () => AndroidSurfaceCleanup.CancelPointers(state, handlers, cancellations, 1),
            () => Assert.False(state.CanRender));
        Assert.Equal(2, delivered);
        Assert.Equal(AndroidSurfaceLifecycleState.Disposed, state.LifecycleState);
        Assert.Empty(state.Dispose());
    }
}
