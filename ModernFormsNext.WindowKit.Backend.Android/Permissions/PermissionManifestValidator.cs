namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

internal sealed record PermissionManifestValidationResult(
    bool IsDeclared,
    string? MissingPermission);

/// <summary>
/// Validates a mapped permission against the declarations present in the final application manifest.
/// </summary>
internal static class PermissionManifestValidator
{
    public static PermissionManifestValidationResult Validate(
        AndroidPermissionDefinition definition,
        IReadOnlySet<string> declaredPermissions)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(declaredPermissions);

        foreach (var requiredPermission in definition.RequiredManifestPermissions)
        {
            if (!declaredPermissions.Contains(requiredPermission))
                return new PermissionManifestValidationResult(false, requiredPermission);
        }

        if (definition.AlternativeManifestPermissions.Count > 0 &&
            !definition.AlternativeManifestPermissions.Any(declaredPermissions.Contains))
        {
            return new PermissionManifestValidationResult(
                false,
                definition.AlternativeManifestPermissions[0]);
        }

        return new PermissionManifestValidationResult(true, null);
    }
}
