namespace ModernFormsNext.WindowKit.Platform.Permissions;

/// <summary>
/// Identifies a platform capability that may require declaration or user authorization.
/// </summary>
/// <remarks>
/// Clipboard access is intentionally absent because ordinary clipboard use is not an Android
/// runtime permission. Platforms may report <see cref="PlatformPermissionStatus.NotSupported"/>
/// for capabilities that do not have an equivalent authorization model.
/// </remarks>
public enum PlatformPermission
{
    /// <summary>Access to a device camera.</summary>
    Camera,
    /// <summary>Access to microphone input.</summary>
    Microphone,
    /// <summary>Permission to deliver user notifications.</summary>
    Notifications,
    /// <summary>Read access to user photos.</summary>
    Photos,
    /// <summary>Read access to user videos.</summary>
    Videos,
    /// <summary>Read access to user audio files.</summary>
    Audio,
    /// <summary>Location access while the application is in use.</summary>
    LocationWhenInUse,
    /// <summary>Location access while the application is in the background.</summary>
    LocationAlways,
    /// <summary>Permission to discover nearby Bluetooth devices.</summary>
    BluetoothScan,
    /// <summary>Permission to connect to paired Bluetooth devices.</summary>
    BluetoothConnect,
    /// <summary>Permission to discover or communicate with nearby devices.</summary>
    NearbyDevices
}
