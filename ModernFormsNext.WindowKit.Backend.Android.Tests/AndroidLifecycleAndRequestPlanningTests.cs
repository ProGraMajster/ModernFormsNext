using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Backend.Android.Permissions;
using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidLifecycleAndRequestPlanningTests
{
    [Fact]
    public void DestroyingOldHostCannotClearRecreatedHost()
    {
        var reference = new WeakHostReference<object>();
        var oldActivity = new object();
        var recreatedActivity = new object();
        reference.Set(oldActivity);
        reference.Set(recreatedActivity);

        Assert.False(reference.ClearIfCurrent(oldActivity));
        Assert.Same(recreatedActivity, reference.Target);
        Assert.True(reference.ClearIfCurrent(recreatedActivity));
        Assert.Null(reference.Target);
    }

    [Theory]
    [InlineData(PlatformPermissionStatus.NotDeclared)]
    [InlineData(PlatformPermissionStatus.NotSupported)]
    [InlineData(PlatformPermissionStatus.Granted)]
    [InlineData(PlatformPermissionStatus.PermanentlyDenied)]
    public void TerminalPermissionStatusNeverContinuesToPlatformDialog(PlatformPermissionStatus status)
    {
        Assert.False(AndroidPermissionRequestPlanner.ShouldContinueRequestFlow(status));
    }

    [Theory]
    [InlineData(PlatformPermissionStatus.Denied)]
    [InlineData(PlatformPermissionStatus.Restricted)]
    [InlineData(PlatformPermissionStatus.Unknown)]
    public void NonTerminalPermissionStatusCanContinueToPlatformSpecificHandling(PlatformPermissionStatus status)
    {
        Assert.True(AndroidPermissionRequestPlanner.ShouldContinueRequestFlow(status));
    }
}
