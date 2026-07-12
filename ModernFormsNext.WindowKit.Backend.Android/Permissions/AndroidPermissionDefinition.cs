using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

internal enum AndroidPermissionGrantRule
{
    All,
    Any
}

/// <summary>
/// Describes Android declarations and runtime requests for one logical permission at one SDK level.
/// </summary>
internal sealed record AndroidPermissionDefinition(
    PlatformPermission Permission,
    PlatformPermissionRequestKind RequestKind,
    IReadOnlyList<string> RequiredManifestPermissions,
    IReadOnlyList<string> AlternativeManifestPermissions,
    IReadOnlyList<string> RuntimePermissions,
    AndroidPermissionGrantRule RuntimeGrantRule = AndroidPermissionGrantRule.All)
{
    public static AndroidPermissionDefinition Unsupported(PlatformPermission permission)
        => new(
            permission,
            PlatformPermissionRequestKind.NotSupported,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
}
