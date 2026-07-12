using Android.App;
using Android.Content.PM;
using ModernFormsNext.WindowKit.Backend.Android.Dispatching;
using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

/// <summary>
/// Owns the single in-flight Android runtime permission dialog and serializes additional requests.
/// </summary>
internal sealed class AndroidPermissionRequestCoordinator
{
    private readonly object sync = new();
    private readonly AndroidActivityTracker activityTracker;
    private readonly AndroidMainThreadDispatcher dispatcher;
    private readonly PermissionRequestQueue queue = new();
    private readonly TimeSpan timeout;
    private PendingRequest? pendingRequest;
    private int nextRequestCode = 42000;

    public AndroidPermissionRequestCoordinator(
        AndroidActivityTracker activityTracker,
        AndroidMainThreadDispatcher dispatcher,
        TimeSpan timeout)
    {
        this.activityTracker = activityTracker;
        this.dispatcher = dispatcher;
        this.timeout = timeout;
        activityTracker.ActivityDestroyed += OnActivityDestroyed;
    }

    public Task<IReadOnlyDictionary<string, bool>> RequestAsync(
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (permissions.Count == 0)
            return Task.FromResult<IReadOnlyDictionary<string, bool>>(
                new Dictionary<string, bool>(StringComparer.Ordinal));

        return queue.EnqueueAsync(
            operationToken => ExecuteAsync(permissions, operationToken),
            timeout,
            cancellationToken);
    }

    public bool HandleRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
    {
        PendingRequest? request;
        lock (sync)
        {
            if (pendingRequest is null || pendingRequest.RequestCode != requestCode)
                return false;

            request = pendingRequest;
            pendingRequest = null;
        }

        var results = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (var index = 0; index < permissions.Length; index++)
        {
            results[permissions[index]] =
                index < grantResults.Length && grantResults[index] == Permission.Granted;
        }

        foreach (var requestedPermission in request.Permissions)
            results.TryAdd(requestedPermission, false);

        request.Completion.TrySetResult(results);
        return true;
    }

    private async Task<IReadOnlyDictionary<string, bool>> ExecuteAsync(
        IReadOnlyList<string> permissions,
        CancellationToken operationToken)
    {
        var activity = activityTracker.CurrentActivity
            ?? throw new InvalidOperationException(
                "Android cannot show a permission dialog because there is no active Activity. " +
                "Wait until the application is in the foreground and try again.");
        var completion = new TaskCompletionSource<IReadOnlyDictionary<string, bool>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCode = Interlocked.Increment(ref nextRequestCode);
        var request = new PendingRequest(requestCode, activity, permissions, completion);

        lock (sync)
        {
            if (pendingRequest is not null)
                throw new InvalidOperationException("A permission dialog is already active.");
            pendingRequest = request;
        }

        using var registration = operationToken.Register(
            static state =>
            {
                var source = (TaskCompletionSource<IReadOnlyDictionary<string, bool>>)state!;
                source.TrySetCanceled();
            },
            completion);

        try
        {
            await dispatcher.InvokeAsync(
                () => activity.RequestPermissions(permissions.ToArray(), requestCode),
                operationToken).ConfigureAwait(false);
            return await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(pendingRequest, request))
                    pendingRequest = null;
            }
        }
    }

    private void OnActivityDestroyed(Activity activity)
    {
        PendingRequest? request;
        lock (sync)
            request = pendingRequest;

        if (request is not null && ReferenceEquals(request.Activity, activity))
        {
            request.Completion.TrySetException(new InvalidOperationException(
                "The Activity that owned the Android permission dialog was destroyed. " +
                "Retry after the replacement Activity reaches the foreground."));
        }
    }

    private sealed record PendingRequest(
        int RequestCode,
        Activity Activity,
        IReadOnlyList<string> Permissions,
        TaskCompletionSource<IReadOnlyDictionary<string, bool>> Completion);
}
