using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsApplicationLifecycleTests
{
    [Fact]
    public void DeactivationKeepsUsableDesktopHostsForegroundAndDoesNotSuspend()
    {
        var native = new WindowsApplicationLifecycle(() => { }, action => action());
        native.WindowCreated(1);
        native.WindowCreated(2);
        StartApplication(native);
        native.ApplicationActivated(true);
        native.ApplicationActivated(false);
        Assert.Equal(PlatformApplicationLifecycleState.Foreground, native.Publisher.State);
        Assert.Equal(PlatformApplicationPhase.Running, native.Publisher.Snapshot.Phase);
        Assert.False(native.Publisher.Snapshot.IsActive);
        Assert.Equal(2, native.Publisher.Snapshot.HostCount);
        native.WindowDestroyed(1);
        Assert.Equal(PlatformApplicationLifecycleState.Foreground, native.Publisher.State);
        native.WindowDestroyed(2);
        Assert.Equal(PlatformApplicationLifecycleState.NoHost, native.Publisher.State);
        Assert.NotEqual(PlatformApplicationPhase.Exited, native.Publisher.Snapshot.Phase);
    }

    [Fact]
    public void PowerResumeDuplicatesAreNormalizedAndObserverFailureCannotPreventSuspension()
    {
        var native = new WindowsApplicationLifecycle(() => { }, action => action());
        native.WindowCreated(1);
        StartApplication(native);
        int changed = 0;
        native.Publisher.LifecycleChanged += (_, _) => changed++;
        native.Publisher.StateSaving += (_, _) => throw new InvalidOperationException("save failure");
        native.PowerChanged(4);
        Assert.Equal(PlatformApplicationPhase.Suspended, native.Publisher.Snapshot.Phase);
        Assert.Equal(PlatformApplicationLifecycleState.Background, native.Publisher.State);
        native.PowerChanged(18);
        native.PowerChanged(7);
        Assert.Equal(2, changed);
        Assert.Equal(PlatformApplicationLifecycleState.Foreground, native.Publisher.State);
    }

    [Fact]
    public void SessionCancellationIsNotExitAndNativeShutdownWaitsForManagedCleanup()
    {
        var native = new WindowsApplicationLifecycle(() => { }, action => action());
        native.WindowCreated(1);
        StartApplication(native);
        native.SessionEnded(false);
        Assert.Equal(PlatformApplicationPhase.Running, native.Publisher.Snapshot.Phase);
        native.SessionEnded(true);
        native.ApplicationActivated(true);
        native.WindowCreated(2);
        Assert.Equal(PlatformApplicationPhase.Exiting, native.Publisher.Snapshot.Phase);
        Assert.Equal(1, native.Publisher.Snapshot.HostCount);
        Assert.False(native.Publisher.Snapshot.IsActive);
    }

    [Fact]
    public void NativeHostUpdatesDoNotStartApplicationOrDeliverInitialActivation()
    {
        var native = new WindowsApplicationLifecycle(() => { }, action => action());
        native.WindowCreated(1);
        native.ApplicationActivated(true);
        Assert.Equal(PlatformApplicationPhase.NotStarted, native.Publisher.Snapshot.Phase);
        Assert.Null(native.Publisher.LastActivation);
        var current = native.Publisher.Snapshot;
        native.Publisher.Publish(new(PlatformApplicationPhase.Starting, current.State,
            current.IsActive, current.HostCount, current.HostGeneration));
        native.WindowCreated(2);
        native.ApplicationActivated(false);
        Assert.Equal(PlatformApplicationPhase.Starting, native.Publisher.Snapshot.Phase);
        native.PowerChanged(4);
        native.PowerChanged(18);
        Assert.Equal(PlatformApplicationPhase.Starting, native.Publisher.Snapshot.Phase);
        Assert.Null(native.Publisher.LastActivation);
    }

    private static void StartApplication(WindowsApplicationLifecycle native)
    {
        var current = native.Publisher.Snapshot;
        native.Publisher.Publish(new(PlatformApplicationPhase.Running, current.State,
            current.IsActive, current.HostCount, current.HostGeneration));
    }
}
