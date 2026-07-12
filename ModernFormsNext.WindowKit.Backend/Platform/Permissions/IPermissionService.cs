namespace ModernFormsNext.WindowKit.Platform.Permissions;

/// <summary>
/// Provides platform-neutral permission checks and user authorization requests.
/// </summary>
/// <remarks>
/// Calling this service does not add declarations to an application manifest. Applications must
/// explicitly declare every capability they use. UI-host methods should be called on the UI thread;
/// backend implementations marshal native interactions when necessary.
/// </remarks>
public interface IPermissionService
{
    /// <summary>
    /// Checks one logical permission without displaying a system prompt.
    /// </summary>
    /// <param name="permission">The permission to inspect.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The effective permission state and any diagnostic information.</returns>
    Task<PlatformPermissionResult> CheckAsync(
        PlatformPermission permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests one logical permission when a runtime dialog is available.
    /// </summary>
    /// <param name="permission">The permission to request.</param>
    /// <param name="cancellationToken">A token used to cancel the caller's wait.</param>
    /// <returns>The permission state after the request.</returns>
    Task<PlatformPermissionResult> RequestAsync(
        PlatformPermission permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a group of logical permissions through one serialized platform operation.
    /// </summary>
    /// <param name="permissions">The permissions to request. Duplicate values are ignored.</param>
    /// <param name="cancellationToken">A token used to cancel the caller's wait.</param>
    /// <returns>One result for each distinct input permission, in input order.</returns>
    Task<IReadOnlyList<PlatformPermissionResult>> RequestAsync(
        IEnumerable<PlatformPermission> permissions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the host should explain why a permission is needed before requesting it.
    /// </summary>
    /// <param name="permission">The permission to inspect.</param>
    /// <returns><see langword="true"/> when the platform recommends showing rationale UI.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the check requires an active UI host but none is available.
    /// </exception>
    bool ShouldShowRationale(PlatformPermission permission);

    /// <summary>
    /// Opens the current application's system settings page after an explicit host request.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel before navigation starts.</param>
    /// <returns>
    /// <see langword="true"/> when the settings activity was started; otherwise,
    /// <see langword="false"/> when no suitable UI host is available.
    /// </returns>
    Task<bool> OpenApplicationSettingsAsync(CancellationToken cancellationToken = default);
}
