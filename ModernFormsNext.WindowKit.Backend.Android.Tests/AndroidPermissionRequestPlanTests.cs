using ModernFormsNext.WindowKit.Backend.Android.Permissions;
using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidPermissionRequestPlanTests
{
    [Fact]
    public void GroupRequestDeduplicatesLogicalAndAndroidPermissions()
    {
        var plan = AndroidPermissionRequestPlan.Create(
            [
                PlatformPermission.Photos,
                PlatformPermission.Videos,
                PlatformPermission.Photos
            ],
            32);

        Assert.Equal(2, plan.Definitions.Count);
        Assert.Equal([AndroidPermissionMapper.ReadExternalStorage], plan.RuntimePermissions);
    }
}
