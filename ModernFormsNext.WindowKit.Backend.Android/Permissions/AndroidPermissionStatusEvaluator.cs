using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

/// <summary>
/// Classifies runtime grants and rationale state into a platform-neutral status.
/// </summary>
internal static class AndroidPermissionStatusEvaluator
{
    public static PlatformPermissionStatus Evaluate(
        AndroidPermissionDefinition definition,
        Func<string, bool> isGranted,
        Func<string, bool> wasRequested,
        Func<string, bool> shouldShowRationale)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(isGranted);
        ArgumentNullException.ThrowIfNull(wasRequested);
        ArgumentNullException.ThrowIfNull(shouldShowRationale);

        if (definition.RequestKind == PlatformPermissionRequestKind.NotSupported)
            return PlatformPermissionStatus.NotSupported;

        if (definition.RuntimePermissions.Count == 0)
            return PlatformPermissionStatus.Granted;

        var grants = definition.RuntimePermissions.Select(isGranted).ToArray();
        var granted = definition.RuntimeGrantRule == AndroidPermissionGrantRule.All
            ? grants.All(static value => value)
            : grants.Any(static value => value);
        if (granted)
            return PlatformPermissionStatus.Granted;

        var deniedPermissions = definition.RuntimePermissions.Where(permission => !isGranted(permission));
        var permanentlyDenied = definition.RuntimePermissions.Any(wasRequested) &&
            deniedPermissions.All(permission => !shouldShowRationale(permission));

        return permanentlyDenied
            ? PlatformPermissionStatus.PermanentlyDenied
            : PlatformPermissionStatus.Denied;
    }
}
