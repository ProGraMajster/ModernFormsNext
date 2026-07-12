namespace ModernFormsNext.WindowKit.Platform.Permissions;

/// <summary>
/// Identifies the system interaction used to authorize a platform capability.
/// </summary>
public enum PlatformPermissionRequestKind
{
    /// <summary>No user interaction is needed for this operating-system version.</summary>
    None,
    /// <summary>The platform can display a standard runtime permission dialog.</summary>
    RuntimeDialog,
    /// <summary>The user must make the change in the application's settings page.</summary>
    ApplicationSettings,
    /// <summary>The user must make the change in a broader system settings page.</summary>
    SystemSettings,
    /// <summary>The operating system does not support the capability.</summary>
    NotSupported
}
