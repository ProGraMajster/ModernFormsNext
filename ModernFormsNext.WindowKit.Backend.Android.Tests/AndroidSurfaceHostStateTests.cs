using ModernFormsNext.WindowKit.Backend.Android.Rendering;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidSurfaceHostStateTests
{
    [Fact]
    public void DensityConversionUsesLogicalPixelsAndRejectsInvalidDensity()
    {
        Assert.Equal(120f, AndroidDensityConverter.ToLogical(360f, 3f));
        Assert.Throws<ArgumentOutOfRangeException>(() => AndroidDensityConverter.ToLogical(1, 0));
    }

    [Fact]
    public void InvalidationBeforeInitializationIsRejected()
    {
        var state = new AndroidSurfaceHostState();

        Assert.Throws<InvalidOperationException>(() => state.RequestInvalidation());
        Assert.Throws<InvalidOperationException>(() => state.CompleteRender());
    }

    [Fact]
    public void RepeatedInvalidationIsCoalescedUntilRenderCompletes()
    {
        var state = new AndroidSurfaceHostState();
        state.Resume();

        Assert.True(state.RequestInvalidation());
        Assert.False(state.RequestInvalidation());
        state.CompleteRender();
        Assert.Equal(1, state.RenderCount);
        Assert.True(state.RequestInvalidation());
    }

    [Fact]
    public void ResizeRequestsOnlyOneRenderForChangedLogicalSize()
    {
        var state = new AndroidSurfaceHostState();
        state.Resume();

        Assert.True(state.Resize(320, 640));
        Assert.False(state.Resize(320, 640));
        Assert.True(state.IsInvalidationPending);
        Assert.Equal(320, state.LogicalWidth);
        Assert.Equal(640, state.LogicalHeight);
    }

    [Fact]
    public void PauseCancelsAllPointersAndRejectsFurtherInput()
    {
        var state = new AndroidSurfaceHostState();
        state.Resume();
        Assert.True(state.TrackPointer(7, AndroidPointerAction.Down));
        Assert.True(state.TrackPointer(3, AndroidPointerAction.Down));

        Assert.Equal([3, 7], state.Pause());
        Assert.Equal(0, state.ActivePointerCount);
        Assert.False(state.TrackPointer(9, AndroidPointerAction.Down));
    }

    [Fact]
    public void ResumeAfterPauseAcceptsInputAgain()
    {
        var state = new AndroidSurfaceHostState();
        state.Resume();
        state.Pause();

        state.Resume();

        Assert.True(state.TrackPointer(1, AndroidPointerAction.Down));
        Assert.Equal(AndroidSurfaceLifecycleState.Resumed, state.LifecycleState);
    }

    [Fact]
    public void DisposeIsIdempotentAndAllLaterOperationsFail()
    {
        var state = new AndroidSurfaceHostState();
        state.Resume();
        state.TrackPointer(11, AndroidPointerAction.Down);

        Assert.Equal([11], state.Dispose());
        Assert.Empty(state.Dispose());
        Assert.Equal(AndroidSurfaceLifecycleState.Disposed, state.LifecycleState);
        Assert.Throws<ObjectDisposedException>(() => state.Resize(10, 10));
        Assert.Throws<ObjectDisposedException>(() => state.CompleteRender());
        Assert.Throws<ObjectDisposedException>(() => state.TrackPointer(1, AndroidPointerAction.Down));
    }
}
