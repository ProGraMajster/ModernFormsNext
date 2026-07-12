using ModernFormsNext.WindowKit.Backend.Android.Permissions;
using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidPermissionMapperTests
{
    [Fact]
    public void NotificationsBeforeApi33NeedNoRuntimePermission()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.Notifications, 32);

        Assert.Equal(PlatformPermissionRequestKind.None, definition.RequestKind);
        Assert.Empty(definition.RuntimePermissions);
        Assert.Empty(definition.RequiredManifestPermissions);
    }

    [Fact]
    public void NotificationsFromApi33UsePostNotifications()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.Notifications, 33);

        Assert.Equal(PlatformPermissionRequestKind.RuntimeDialog, definition.RequestKind);
        Assert.Equal([AndroidPermissionMapper.PostNotifications], definition.RuntimePermissions);
    }

    [Theory]
    [InlineData(PlatformPermission.Photos, AndroidPermissionMapper.ReadMediaImages)]
    [InlineData(PlatformPermission.Videos, AndroidPermissionMapper.ReadMediaVideo)]
    [InlineData(PlatformPermission.Audio, AndroidPermissionMapper.ReadMediaAudio)]
    public void MediaFromApi33UsesGranularPermissions(
        PlatformPermission permission,
        string expectedAndroidPermission)
    {
        var definition = AndroidPermissionMapper.Map(permission, 33);

        Assert.Equal([expectedAndroidPermission], definition.RuntimePermissions);
    }

    [Theory]
    [InlineData(PlatformPermission.Photos)]
    [InlineData(PlatformPermission.Videos)]
    [InlineData(PlatformPermission.Audio)]
    public void MediaBeforeApi33UsesReadExternalStorage(PlatformPermission permission)
    {
        var definition = AndroidPermissionMapper.Map(permission, 32);

        Assert.Equal([AndroidPermissionMapper.ReadExternalStorage], definition.RuntimePermissions);
    }

    [Fact]
    public void ForegroundLocationAcceptsCoarseOrFine()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.LocationWhenInUse, 35);

        Assert.Equal(AndroidPermissionGrantRule.Any, definition.RuntimeGrantRule);
        Assert.Contains(AndroidPermissionMapper.AccessCoarseLocation, definition.RuntimePermissions);
        Assert.Contains(AndroidPermissionMapper.AccessFineLocation, definition.RuntimePermissions);
    }

    [Fact]
    public void BackgroundLocationOnApi29UsesStagedRuntimeDialog()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.LocationAlways, 29);

        Assert.Equal(PlatformPermissionRequestKind.RuntimeDialog, definition.RequestKind);
        Assert.Equal([AndroidPermissionMapper.AccessBackgroundLocation], definition.RuntimePermissions);
        Assert.Contains(AndroidPermissionMapper.AccessFineLocation, definition.AlternativeManifestPermissions);
    }

    [Fact]
    public void BackgroundLocationFromApi30UsesApplicationSettings()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.LocationAlways, 30);

        Assert.Equal(PlatformPermissionRequestKind.ApplicationSettings, definition.RequestKind);
        Assert.Empty(definition.RuntimePermissions);
    }

    [Fact]
    public void BluetoothFromApi31UsesNearbyDevicePermissions()
    {
        var scan = AndroidPermissionMapper.Map(PlatformPermission.BluetoothScan, 31);
        var connect = AndroidPermissionMapper.Map(PlatformPermission.BluetoothConnect, 31);

        Assert.Equal([AndroidPermissionMapper.BluetoothScan], scan.RuntimePermissions);
        Assert.Equal([AndroidPermissionMapper.BluetoothConnect], connect.RuntimePermissions);
    }

    [Fact]
    public void BluetoothScanBeforeApi31UsesLocation()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.BluetoothScan, 30);

        Assert.Equal(AndroidPermissionGrantRule.Any, definition.RuntimeGrantRule);
        Assert.Contains(AndroidPermissionMapper.AccessFineLocation, definition.RuntimePermissions);
    }

    [Fact]
    public void NearbyDevicesBeforeRuntimePermissionsIsUnsupported()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.NearbyDevices, 22);

        Assert.Equal(PlatformPermissionRequestKind.NotSupported, definition.RequestKind);
    }
}
