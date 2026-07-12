using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

/// <summary>
/// Produces the deterministic Android runtime-permission union for a logical group request.
/// </summary>
internal sealed record AndroidPermissionRequestPlan(
    IReadOnlyList<AndroidPermissionDefinition> Definitions,
    IReadOnlyList<string> RuntimePermissions)
{
    public static AndroidPermissionRequestPlan Create(
        IEnumerable<PlatformPermission> permissions,
        int sdkVersion)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var definitions = permissions
            .Distinct()
            .Select(permission => AndroidPermissionMapper.Map(permission, sdkVersion))
            .ToArray();
        var runtimePermissions = definitions
            .SelectMany(static definition => definition.RuntimePermissions)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new AndroidPermissionRequestPlan(definitions, runtimePermissions);
    }
}
