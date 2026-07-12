using ModernFormsNext.WindowKit.Backend.Android.Permissions;
using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidPermissionStatusEvaluatorTests
{
    [Fact]
    public void NeverRequestedDenialRemainsDenied()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.Camera, 35);

        var status = AndroidPermissionStatusEvaluator.Evaluate(
            definition,
            _ => false,
            _ => false,
            _ => false);

        Assert.Equal(PlatformPermissionStatus.Denied, status);
    }

    [Fact]
    public void RequestedDenialWithoutRationaleIsPermanent()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.Camera, 35);

        var status = AndroidPermissionStatusEvaluator.Evaluate(
            definition,
            _ => false,
            _ => true,
            _ => false);

        Assert.Equal(PlatformPermissionStatus.PermanentlyDenied, status);
    }

    [Fact]
    public void RequestedDenialWithRationaleCanBeRequestedAgain()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.Camera, 35);

        var status = AndroidPermissionStatusEvaluator.Evaluate(
            definition,
            _ => false,
            _ => true,
            _ => true);

        Assert.Equal(PlatformPermissionStatus.Denied, status);
    }

    [Fact]
    public void AnyGrantRuleAcceptsCoarseLocation()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.LocationWhenInUse, 35);

        var status = AndroidPermissionStatusEvaluator.Evaluate(
            definition,
            permission => permission == AndroidPermissionMapper.AccessCoarseLocation,
            _ => false,
            _ => false);

        Assert.Equal(PlatformPermissionStatus.Granted, status);
    }
}
