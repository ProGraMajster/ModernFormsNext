using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using System.Runtime.CompilerServices;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidApplicationLifecycleReducerTests
{
    [Fact]
    public void CreatingSecondActivityDoesNotBackgroundAnExistingResumedHost()
    {
        var reducer = new AndroidActivityLifecycleReducer<object>();
        var first = new object();
        var second = new object();
        reducer.Observe(first, AndroidActivityPhase.Resumed);
        reducer.Observe(second, AndroidActivityPhase.Created);
        reducer.Observe(second, AndroidActivityPhase.Started);

        Assert.Equal(PlatformApplicationLifecycleState.Foreground, reducer.Snapshot.State);
        Assert.Same(first, reducer.CurrentResumedHost);
        Assert.Equal(2, reducer.Snapshot.HostCount);
        reducer.Observe(second, AndroidActivityPhase.Resumed);
        reducer.Observe(second, AndroidActivityPhase.Paused);
        Assert.Same(first, reducer.CurrentResumedHost);
        Assert.True(reducer.Snapshot.IsActive);
    }

    [Fact]
    public void PauseStopRecreationAndOldCallbacksPreserveApplicationAndGeneration()
    {
        var reducer = new AndroidActivityLifecycleReducer<object>();
        var old = new object();
        var replacement = new object();
        reducer.Observe(old, AndroidActivityPhase.Resumed);
        reducer.Observe(old, AndroidActivityPhase.Paused);
        Assert.Null(reducer.CurrentResumedHost);
        Assert.Equal(PlatformApplicationLifecycleState.Background, reducer.Snapshot.State);
        reducer.Observe(old, AndroidActivityPhase.Stopped);
        Assert.Equal(PlatformApplicationPhase.Suspended, reducer.Snapshot.Phase);
        reducer.Observe(replacement, AndroidActivityPhase.Resumed);
        long generation = reducer.Snapshot.HostGeneration;
        reducer.Destroy(old);
        reducer.Observe(old, AndroidActivityPhase.Stopped);
        reducer.Observe(old, AndroidActivityPhase.Paused);
        reducer.Observe(old, AndroidActivityPhase.Resumed);
        reducer.Observe(old, AndroidActivityPhase.Started);
        Assert.False(reducer.TryObserveCreation(old, out _));

        Assert.Equal(generation, reducer.Snapshot.HostGeneration);
        Assert.Equal(1, reducer.Snapshot.HostCount);
        Assert.Same(replacement, reducer.CurrentResumedHost);
        reducer.Destroy(replacement);
        Assert.Equal(PlatformApplicationLifecycleState.NoHost, reducer.Snapshot.State);
        Assert.Equal(PlatformApplicationPhase.Running, reducer.Snapshot.Phase);
    }

    [Fact]
    public void OnlyFirstProcessCreationCanRestoreSavedApplicationState()
    {
        var reducer = new AndroidActivityLifecycleReducer<object>();
        var first = new object();
        Assert.True(reducer.TryObserveCreation(first, out bool initial));
        Assert.True(initial);
        reducer.Observe(first, AndroidActivityPhase.Resumed);
        Assert.False(reducer.TryObserveCreation(first, out _));
        Assert.True(reducer.IsResumed(first));
        reducer.Destroy(first);

        var replacement = new object();
        Assert.True(reducer.TryObserveCreation(replacement, out initial));
        Assert.False(initial);
        reducer.Observe(replacement, AndroidActivityPhase.Paused);
        Assert.True(reducer.IsKnown(replacement)); // OnNewIntent can arrive while paused.
        Assert.False(reducer.IsKnown(first));
        reducer.Destroy(replacement);
        Assert.False(reducer.IsKnown(replacement));
    }

    [Fact]
    public void CollectedHostIsNotRetainedByActivityAggregation()
    {
        var reducer = new AndroidActivityLifecycleReducer<object>();
        WeakReference reference = AddCollectible(reducer);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(reference.IsAlive);
        Assert.Equal(0, reducer.Snapshot.HostCount);
        Assert.Null(reducer.CurrentResumedHost);
    }

    [Fact]
    public void RetiredIdentityDoesNotRetainDestroyedHost()
    {
        var reducer = new AndroidActivityLifecycleReducer<object>();
        WeakReference reference = AddRetired(reducer);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(reference.IsAlive);
    }

    [Theory]
    [InlineData(0, 0, 400, 800, 12, 24, 8, 36)]
    [InlineData(12, 24, 380, 740, 0, 0, 0, 0)]
    [InlineData(0, 10, 400, 770, 12, 14, 8, 16)]
    public void InsetsDescribeOnlyOcclusionOverlappingTheNativeSurface(
        double x, double y, double width, double height, double left, double top, double right, double bottom)
    {
        var insets = AndroidInsetsMapper.ToSurface(new Thickness(12, 24, 8, 36),
            new Rect(x, y, width, height), new Size(400, 800), 2);
        Assert.Equal(new Thickness(left / 2, top / 2, right / 2, bottom / 2), insets);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddCollectible(AndroidActivityLifecycleReducer<object> reducer)
    {
        var host = new object();
        reducer.Observe(host, AndroidActivityPhase.Resumed);
        return new WeakReference(host);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddRetired(AndroidActivityLifecycleReducer<object> reducer)
    {
        var host = new object();
        reducer.Observe(host, AndroidActivityPhase.Resumed);
        reducer.Destroy(host);
        return new WeakReference(host);
    }
}
