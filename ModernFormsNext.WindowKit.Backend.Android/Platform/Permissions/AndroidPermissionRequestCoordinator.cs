using Android.App;
using Android.Content.PM;
using ModernFormsNext.WindowKit.Backend.Android.Dispatching;
using ModernFormsNext.WindowKit.Backend.Android.Services;

namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

// Permission policy remains in AndroidPermissionService. Native ownership is shared with
// pickers/chooser, so competing calls fail Busy instead of stacking unrelated system modals.
internal sealed class AndroidPermissionRequestCoordinator(
    AndroidActivityResultCoordinator requests, AndroidMainThreadDispatcher dispatcher, TimeSpan timeout)
{
    internal async Task<IReadOnlyDictionary<string, bool>> RequestAsync(
        IReadOnlyList<string> permissions, CancellationToken cancellationToken, Action onLaunching)
    {
        var task = await dispatcher.InvokeAsync(
            () => requests.RequestPermissions(permissions.ToArray(), cancellationToken, timeout, onLaunching),
            cancellationToken).ConfigureAwait(false);
        return (await task.ConfigureAwait(false)).Permissions ?? new Dictionary<string, bool>();
    }
    internal bool HandleRequestPermissionsResult(int code, string[] permissions, Permission[] grants)
        => requests.HandlePermissions(null, code, permissions, grants);
    internal bool HandleRequestPermissionsResult(Activity activity, int code, string[] permissions, Permission[] grants)
        => requests.HandlePermissions(activity, code, permissions, grants);
}
