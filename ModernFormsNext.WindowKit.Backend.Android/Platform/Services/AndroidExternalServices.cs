using Android.Content;
using Android.Content.PM;
using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Platform.Storage;
using NativeUri = Android.Net.Uri;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

internal sealed class AndroidExternalServices(Context context, AndroidActivityTracker tracker,
    AndroidActivityResultCoordinator requests) : IPlatformLauncherService, IPlatformShareService
{
    public bool IsSupported => true;
    public bool CanOpenUri(Uri uri)
    {
        requests.VerifyAccess();
        if (!AndroidServicePlans.IsExternalUri(uri) || requests.Availability != PlatformServiceStatus.Success) return false;
        using var intent = View(uri);
        return intent.ResolveActivity(context.PackageManager!) is not null;
    }
    public PlatformServiceStatus OpenUri(Uri uri)
    {
        requests.VerifyAccess();
        if (!AndroidServicePlans.IsExternalUri(uri)) return PlatformServiceStatus.NotSupported;
        using var intent = View(uri);
        return Launch(intent);
    }
    private static Intent View(Uri uri)
    {
        using var native = NativeUri.Parse(uri.AbsoluteUri)!;
        return new Intent(AndroidServicePlans.ViewAction(uri), native);
    }
    private PlatformServiceStatus Launch(Intent intent)
    {
        var status = requests.Availability;
        if (status != PlatformServiceStatus.Success) return status;
        var activity = tracker.CurrentActivity;
        if (activity is null) return PlatformServiceStatus.Unavailable;
        if (intent.ResolveActivity(context.PackageManager!) is null) return PlatformServiceStatus.NoHandler;
        try { activity.StartActivity(intent); return PlatformServiceStatus.Success; }
        catch (ActivityNotFoundException) { return PlatformServiceStatus.NoHandler; }
        catch (Java.Lang.SecurityException) { return PlatformServiceStatus.PermissionDenied; }
    }
    public PlatformServiceStatus OpenFile(IStorageFile file, string? mimeType = null)
    {
        requests.VerifyAccess();
        ArgumentNullException.ThrowIfNull(file);
        if (mimeType is not null) AndroidServicePlans.ValidateMime(mimeType);
        if (!file.Path.IsAbsoluteUri || file.Path.Scheme != "content") return PlatformServiceStatus.NotSupported;
        using var uri = NativeUri.Parse(file.Path.AbsoluteUri)!;
        if (!CanRead(uri)) return PlatformServiceStatus.PermissionDenied;
        using var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(uri, mimeType ?? context.ContentResolver!.GetType(uri) ?? "application/octet-stream");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        intent.ClipData = ClipData.NewRawUri("", uri);
        return Launch(intent);
    }
    public async Task<PlatformServiceStatus> ShareAsync(PlatformShareRequest request, CancellationToken cancellationToken = default)
    {
        requests.VerifyAccess();
        cancellationToken.ThrowIfCancellationRequested();
        var plan = AndroidServicePlans.Share(request);
        if (plan is null) return PlatformServiceStatus.NotSupported;
        var items = plan.Items;
        if (requests.Availability != PlatformServiceStatus.Success) return requests.Availability;
        using var send = new Intent(plan.Action);
        send.SetType(plan.Mime);
        if (request.Text is not null) send.PutExtra(Intent.ExtraText, request.Text);
        var native = items.Select(u => NativeUri.Parse(u.AbsoluteUri)!).ToArray();
        try
        {
            if (native.Any(u => !CanRead(u))) return PlatformServiceStatus.PermissionDenied;
            if (plan.GrantReadAccess)
            {
                var clip = ClipData.NewRawUri("", native[0]);
                for (int i = 1; i < native.Length; i++) clip!.AddItem(new ClipData.Item(native[i]));
                send.ClipData = clip;
                send.AddFlags(ActivityFlags.GrantReadUriPermission);
                if (native.Length == 1) send.PutExtra(Intent.ExtraStream, native[0]);
                else send.PutParcelableArrayListExtra(Intent.ExtraStream, native.Cast<global::Android.OS.IParcelable>().ToList());
            }
            // Resolve the target intent, not just the always-present system chooser.
            if (send.ResolveActivity(context.PackageManager!) is null) return PlatformServiceStatus.NoHandler;
            using var chooser = Intent.CreateChooser(send, request.Title)!;
            if (plan.GrantReadAccess) { chooser.AddFlags(ActivityFlags.GrantReadUriPermission); chooser.ClipData = send.ClipData; }
            await requests.Start(chooser, cancellationToken).ConfigureAwait(false);
            return PlatformServiceStatus.Success;
        }
        catch (PlatformServiceException e) { return e.Status; }
        catch (ActivityNotFoundException) { return PlatformServiceStatus.NoHandler; }
        catch (Java.Lang.SecurityException) { return PlatformServiceStatus.PermissionDenied; }
        finally { foreach (var uri in native) uri.Dispose(); }
    }
    private bool CanRead(NativeUri uri) => context.CheckUriPermission(uri,
        global::Android.OS.Process.MyPid(), global::Android.OS.Process.MyUid(), ActivityFlags.GrantReadUriPermission) == Permission.Granted;
}
