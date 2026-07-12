namespace ModernFormsNext.WindowKit.Platform.Permissions;

/// <summary>
/// Contains the status and diagnostic details produced by a permission operation.
/// </summary>
/// <param name="Permission">The logical permission that was evaluated.</param>
/// <param name="Status">The resulting authorization status.</param>
/// <param name="RequestKind">The interaction required by the current platform version.</param>
/// <param name="DiagnosticMessage">An optional actionable diagnostic intended for developers.</param>
public sealed record PlatformPermissionResult(
    PlatformPermission Permission,
    PlatformPermissionStatus Status,
    PlatformPermissionRequestKind RequestKind,
    string? DiagnosticMessage = null);
