using ModernFormsNext.WindowKit.Backend.Android.Animation;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidFrameCallbackStateTests
{
    [Fact]
    public void SchedulerDemandRemainsIdleWithoutAnActiveSurface()
    {
        var state = new AndroidFrameCallbackState();

        state.SetSchedulerDemand(active: true);

        Assert.Equal(AndroidFrameCallbackAction.None, state.Reconcile());
        Assert.False(state.CallbackPending);
        Assert.Equal(0, state.PostedCallbackCount);
    }

    [Fact]
    public void ActiveSurfaceAndSchedulerDemandPostExactlyOneCallback()
    {
        var state = new AndroidFrameCallbackState();
        state.AddActiveSurface();
        state.SetSchedulerDemand(active: true);

        Assert.Equal(AndroidFrameCallbackAction.Post, state.Reconcile());
        Assert.Equal(AndroidFrameCallbackAction.None, state.Reconcile());
        Assert.True(state.CallbackPending);
        Assert.Equal(1, state.PostedCallbackCount);
    }

    [Fact]
    public void DeliveredFrameDoesNotBuildBacklogAndRepostsOnlyAfterReconcile()
    {
        var state = new AndroidFrameCallbackState();
        state.AddActiveSurface();
        state.SetSchedulerDemand(active: true);
        state.Reconcile();

        Assert.True(state.BeginFrameDelivery());
        Assert.False(state.CallbackPending);
        Assert.False(state.BeginFrameDelivery());
        Assert.Equal(AndroidFrameCallbackAction.Post, state.Reconcile());
        Assert.Equal(2, state.PostedCallbackCount);
        Assert.Equal(1, state.DeliveredCallbackCount);
    }

    [Fact]
    public void IdleThenWakeAndRepeatedStartStopNeverDuplicateCallbacks()
    {
        var state = new AndroidFrameCallbackState();
        state.AddActiveSurface();

        for (int iteration = 0; iteration < 5; iteration++)
        {
            state.SetSchedulerDemand(active: true);
            Assert.Equal(AndroidFrameCallbackAction.Post, state.Reconcile());
            Assert.Equal(AndroidFrameCallbackAction.None, state.Reconcile());

            state.SetSchedulerDemand(active: false);
            Assert.Equal(AndroidFrameCallbackAction.Remove, state.Reconcile());
            Assert.Equal(AndroidFrameCallbackAction.None, state.Reconcile());
        }

        Assert.False(state.CallbackPending);
        Assert.Equal(5, state.PostedCallbackCount);
    }

    [Fact]
    public void SurfaceDetachRemovesPendingCallbackAndReattachWakesDemand()
    {
        var state = new AndroidFrameCallbackState();
        state.AddActiveSurface();
        state.SetSchedulerDemand(active: true);
        state.Reconcile();

        state.RemoveActiveSurface();
        Assert.Equal(AndroidFrameCallbackAction.Remove, state.Reconcile());

        state.AddActiveSurface();
        Assert.Equal(AndroidFrameCallbackAction.Post, state.Reconcile());
    }

    [Fact]
    public void DisposeRemovesPendingCallbackAndRejectsNewSurfaces()
    {
        var state = new AndroidFrameCallbackState();
        state.AddActiveSurface();
        state.SetSchedulerDemand(active: true);
        state.Reconcile();

        Assert.Equal(AndroidFrameCallbackAction.Remove, state.Dispose());
        Assert.Equal(AndroidFrameCallbackAction.None, state.Dispose());
        Assert.Throws<ObjectDisposedException>(state.AddActiveSurface);
    }
}
