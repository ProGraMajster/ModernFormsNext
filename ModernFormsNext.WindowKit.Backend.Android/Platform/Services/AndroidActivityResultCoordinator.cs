using Android.App;
using Android.Content;
using Android.Content.PM;
using ModernFormsNext.WindowKit.Backend.Android.Dispatching;
using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

// One backend-owned coordinator for SAF, chooser and permission UI. Only weak host identities
// cross asynchronous boundaries. Activity references exist only in synchronous launch frames;
// the calling service owns and disposes its Intent after the result or failure completes.
internal sealed class AndroidActivityResultCoordinator
{
    private readonly NativeRequestCoordinator<Activity, AndroidServiceResult> requests = new();
    private readonly AndroidActivityTracker tracker;
    private readonly AndroidMainThreadDispatcher dispatcher;
    private WeakReference<Activity>? permissionOwner;
    internal AndroidActivityResultCoordinator(AndroidActivityTracker tracker, AndroidMainThreadDispatcher dispatcher)
    {
        this.tracker = tracker;
        this.dispatcher = dispatcher;
        tracker.ActivityDestroyed += requests.Destroy;
    }
    internal bool Busy => requests.Busy;
    internal bool IsShutdown => requests.IsShutdown;
    internal void VerifyAccess()
    {
        if (!dispatcher.CheckAccess()) throw new InvalidOperationException("Android service UI requires the main thread.");
    }
    internal PlatformServiceStatus Availability
        => IsShutdown ? PlatformServiceStatus.Shutdown : Busy ? PlatformServiceStatus.Busy :
            tracker.CurrentActivity is null ? PlatformServiceStatus.Unavailable : PlatformServiceStatus.Success;

    internal Task<AndroidServiceResult> Start(Intent intent, CancellationToken token)
        => Start((activity, code) =>
        {
            if (intent.ResolveActivity(activity.PackageManager!) is null)
                throw new PlatformServiceException(PlatformServiceStatus.NoHandler);
            activity.StartActivityForResult(intent, code);
        }, token, TimeSpan.FromMinutes(5));

    internal Task<AndroidServiceResult> RequestPermissions(string[] permissions, CancellationToken token, TimeSpan timeout, Action onLaunching)
        => Start((activity, code) =>
        {
            permissionOwner = new(activity);
            onLaunching();
            activity.RequestPermissions(permissions, code);
        }, token, timeout);

    private Task<AndroidServiceResult> Start(Action<Activity, int> launch, CancellationToken token, TimeSpan timeout)
    {
        VerifyAccess();
        token.ThrowIfCancellationRequested();
        if (requests.IsShutdown) throw new PlatformServiceException(PlatformServiceStatus.Shutdown);
        if (requests.Busy) throw new PlatformServiceException(PlatformServiceStatus.Busy);
        var activity = tracker.CurrentActivity ?? throw new PlatformServiceException(PlatformServiceStatus.Unavailable);
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        if (timeout != Timeout.InfiniteTimeSpan) deadline.CancelAfter(timeout);
        (int Code, Task<AndroidServiceResult> Task) request;
        try { request = requests.Begin(activity, deadline.Token); }
        catch { deadline.Dispose(); throw; }
        try
        {
            token.ThrowIfCancellationRequested();
            launch(activity, request.Code);
        }
        catch (Exception exception) { requests.FailStart(request.Code, exception); }
        return Wait(request.Task, deadline, token);
    }

    private static async Task<AndroidServiceResult> Wait(Task<AndroidServiceResult> task,
        CancellationTokenSource deadline, CancellationToken caller)
    {
        using (deadline)
        {
            try { return await task.ConfigureAwait(false); }
            catch (OperationCanceledException) when (!caller.IsCancellationRequested)
            { throw new TimeoutException("Android service UI did not return within its deadline."); }
        }
    }

    internal bool Handle(Activity activity, int code, Result result, Intent? data)
    {
        VerifyAccess();
        // Copy only neutral values; never retain the returned Intent or Java Uri.
        var values = new List<Uri>();
        if (data?.ClipData is { } clip)
            for (int i = 0; i < clip.ItemCount; i++)
                if (Uri.TryCreate(clip.GetItemAt(i)?.Uri?.ToString(), UriKind.Absolute, out var uri)) values.Add(uri);
        Uri.TryCreate(data?.Data?.ToString(), UriKind.Absolute, out var single);
        return requests.Complete(activity, code, new(result == Result.Ok,
            AndroidServicePlans.SelectUris(values, single), (int)(data?.Flags ?? 0), null));
    }

    internal bool HandlePermissions(Activity? activity, int code, string[] permissions, Permission[] grants)
    {
        VerifyAccess();
        if (activity is null && permissionOwner?.TryGetTarget(out activity) != true) return false;
        var results = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (int i = 0; i < permissions.Length; i++) results[permissions[i]] = i < grants.Length && grants[i] == Permission.Granted;
        return requests.Complete(activity!, code, new(true, [], 0, results));
    }

    internal (int Code, Task<AndroidServiceResult> Task) BeginDialog(Activity activity, Action retire, long generation)
    {
        VerifyAccess();
        return requests.Begin(activity, default, retire, generation);
    }
    internal bool CompleteDialog(Activity activity, int code, int index, long generation)
    {
        VerifyAccess();
        return requests.Complete(activity, code, new(true, [], 0, null, index), generation);
    }
    internal void CancelDialog(Activity activity, int code, CancellationToken token, long generation)
    {
        VerifyAccess();
        requests.Cancel(activity, code, token, generation);
    }
    internal void FailDialog(int code, Exception exception)
    {
        VerifyAccess();
        requests.FailStart(code, exception);
    }

    internal void Shutdown()
    {
        VerifyAccess();
        tracker.ActivityDestroyed -= requests.Destroy;
        permissionOwner = null;
        requests.Shutdown();
    }
}
internal sealed record AndroidServiceResult(bool Accepted, IReadOnlyList<Uri> Uris, int Flags,
    IReadOnlyDictionary<string, bool>? Permissions, int MessageButton = -1);
