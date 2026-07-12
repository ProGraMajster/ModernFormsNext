using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class PlatformPermissionContractTests
{
    [Fact]
    public void StatusContractContainsDiagnosticStates()
    {
        var statuses = Enum.GetValues<PlatformPermissionStatus>();

        Assert.Contains(PlatformPermissionStatus.Unknown, statuses);
        Assert.Contains(PlatformPermissionStatus.Granted, statuses);
        Assert.Contains(PlatformPermissionStatus.Denied, statuses);
        Assert.Contains(PlatformPermissionStatus.Restricted, statuses);
        Assert.Contains(PlatformPermissionStatus.PermanentlyDenied, statuses);
        Assert.Contains(PlatformPermissionStatus.NotDeclared, statuses);
        Assert.Contains(PlatformPermissionStatus.NotSupported, statuses);
    }
}
