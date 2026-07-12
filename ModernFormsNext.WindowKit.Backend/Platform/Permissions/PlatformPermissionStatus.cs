namespace ModernFormsNext.WindowKit.Platform.Permissions;

/// <summary>
/// Describes the effective authorization state of a platform permission.
/// </summary>
public enum PlatformPermissionStatus
{
    /// <summary>The current state could not be determined.</summary>
    Unknown,
    /// <summary>The capability is currently authorized.</summary>
    Granted,
    /// <summary>The user denied the request and the application may request it again.</summary>
    Denied,
    /// <summary>Policy, parental controls, or another system restriction prevents authorization.</summary>
    Restricted,
    /// <summary>The user denied the request and the system no longer offers a runtime dialog.</summary>
    PermanentlyDenied,
    /// <summary>The required declaration is absent from the final application manifest.</summary>
    NotDeclared,
    /// <summary>The operating system or backend does not support this capability.</summary>
    NotSupported
}
