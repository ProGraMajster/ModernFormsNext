using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

/// <summary>
/// Centralizes Android SDK-dependent permission names without taking a dependency on Android APIs.
/// </summary>
internal static class AndroidPermissionMapper
{
    internal const string Camera = "android.permission.CAMERA";
    internal const string RecordAudio = "android.permission.RECORD_AUDIO";
    internal const string PostNotifications = "android.permission.POST_NOTIFICATIONS";
    internal const string ReadExternalStorage = "android.permission.READ_EXTERNAL_STORAGE";
    internal const string ReadMediaImages = "android.permission.READ_MEDIA_IMAGES";
    internal const string ReadMediaVideo = "android.permission.READ_MEDIA_VIDEO";
    internal const string ReadMediaAudio = "android.permission.READ_MEDIA_AUDIO";
    internal const string AccessCoarseLocation = "android.permission.ACCESS_COARSE_LOCATION";
    internal const string AccessFineLocation = "android.permission.ACCESS_FINE_LOCATION";
    internal const string AccessBackgroundLocation = "android.permission.ACCESS_BACKGROUND_LOCATION";
    internal const string Bluetooth = "android.permission.BLUETOOTH";
    internal const string BluetoothAdmin = "android.permission.BLUETOOTH_ADMIN";
    internal const string BluetoothScan = "android.permission.BLUETOOTH_SCAN";
    internal const string BluetoothConnect = "android.permission.BLUETOOTH_CONNECT";
    internal const string NearbyWifiDevices = "android.permission.NEARBY_WIFI_DEVICES";

    /// <summary>
    /// Maps a logical permission for the supplied Android API level.
    /// </summary>
    public static AndroidPermissionDefinition Map(PlatformPermission permission, int sdkVersion)
    {
        if (sdkVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(sdkVersion));

        return permission switch
        {
            PlatformPermission.Camera => Dangerous(permission, sdkVersion, Camera),
            PlatformPermission.Microphone => Dangerous(permission, sdkVersion, RecordAudio),
            PlatformPermission.Notifications => sdkVersion >= 33
                ? Dangerous(permission, sdkVersion, PostNotifications)
                : NoRequest(permission),
            PlatformPermission.Photos => Media(permission, sdkVersion, ReadMediaImages),
            PlatformPermission.Videos => Media(permission, sdkVersion, ReadMediaVideo),
            PlatformPermission.Audio => Media(permission, sdkVersion, ReadMediaAudio),
            PlatformPermission.LocationWhenInUse => ForegroundLocation(permission, sdkVersion),
            PlatformPermission.LocationAlways => BackgroundLocation(permission, sdkVersion),
            PlatformPermission.BluetoothScan => MapBluetoothScan(permission, sdkVersion),
            PlatformPermission.BluetoothConnect => MapBluetoothConnect(permission, sdkVersion),
            PlatformPermission.NearbyDevices => MapNearbyDevices(permission, sdkVersion),
            _ => AndroidPermissionDefinition.Unsupported(permission)
        };
    }

    private static AndroidPermissionDefinition Dangerous(
        PlatformPermission permission,
        int sdkVersion,
        string androidPermission)
        => new(
            permission,
            sdkVersion >= 23 ? PlatformPermissionRequestKind.RuntimeDialog : PlatformPermissionRequestKind.None,
            [androidPermission],
            Array.Empty<string>(),
            sdkVersion >= 23 ? [androidPermission] : Array.Empty<string>());

    private static AndroidPermissionDefinition NoRequest(PlatformPermission permission)
        => new(
            permission,
            PlatformPermissionRequestKind.None,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

    private static AndroidPermissionDefinition Media(
        PlatformPermission permission,
        int sdkVersion,
        string modernPermission)
    {
        var androidPermission = sdkVersion >= 33 ? modernPermission : ReadExternalStorage;
        return Dangerous(permission, sdkVersion, androidPermission);
    }

    private static AndroidPermissionDefinition ForegroundLocation(
        PlatformPermission permission,
        int sdkVersion)
        => new(
            permission,
            sdkVersion >= 23 ? PlatformPermissionRequestKind.RuntimeDialog : PlatformPermissionRequestKind.None,
            Array.Empty<string>(),
            [AccessCoarseLocation, AccessFineLocation],
            sdkVersion >= 23 ? [AccessCoarseLocation, AccessFineLocation] : Array.Empty<string>(),
            AndroidPermissionGrantRule.Any);

    private static AndroidPermissionDefinition BackgroundLocation(
        PlatformPermission permission,
        int sdkVersion)
    {
        if (sdkVersion < 29)
            return ForegroundLocation(permission, sdkVersion);

        return new AndroidPermissionDefinition(
            permission,
            sdkVersion >= 30
                ? PlatformPermissionRequestKind.ApplicationSettings
                : PlatformPermissionRequestKind.RuntimeDialog,
            [AccessBackgroundLocation],
            [AccessCoarseLocation, AccessFineLocation],
            sdkVersion == 29 ? [AccessBackgroundLocation] : Array.Empty<string>());
    }

    private static AndroidPermissionDefinition MapBluetoothScan(
        PlatformPermission permission,
        int sdkVersion)
    {
        if (sdkVersion >= 31)
            return Dangerous(permission, sdkVersion, BluetoothScan);

        if (sdkVersion >= 23)
        {
            return new AndroidPermissionDefinition(
                permission,
                PlatformPermissionRequestKind.RuntimeDialog,
                Array.Empty<string>(),
                [AccessCoarseLocation, AccessFineLocation],
                [AccessCoarseLocation, AccessFineLocation],
                AndroidPermissionGrantRule.Any);
        }

        return new AndroidPermissionDefinition(
            permission,
            PlatformPermissionRequestKind.None,
            [Bluetooth, BluetoothAdmin],
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static AndroidPermissionDefinition MapBluetoothConnect(
        PlatformPermission permission,
        int sdkVersion)
        => sdkVersion >= 31
            ? Dangerous(permission, sdkVersion, BluetoothConnect)
            : new AndroidPermissionDefinition(
                permission,
                PlatformPermissionRequestKind.None,
                [Bluetooth],
                Array.Empty<string>(),
                Array.Empty<string>());

    private static AndroidPermissionDefinition MapNearbyDevices(
        PlatformPermission permission,
        int sdkVersion)
    {
        if (sdkVersion >= 33)
            return Dangerous(permission, sdkVersion, NearbyWifiDevices);

        if (sdkVersion >= 31)
        {
            return new AndroidPermissionDefinition(
                permission,
                PlatformPermissionRequestKind.RuntimeDialog,
                [BluetoothScan, BluetoothConnect],
                Array.Empty<string>(),
                [BluetoothScan, BluetoothConnect]);
        }

        if (sdkVersion >= 23)
            return MapBluetoothScan(permission, sdkVersion);

        return AndroidPermissionDefinition.Unsupported(permission);
    }
}
