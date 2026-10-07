using Android.App;
using Android.Content;
using Android.Content.PM;
using ModernFormsNext.WindowKit.Backend.Android.Permissions;
using ModernFormsNext.WindowKit.Platform.Permissions;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

internal sealed class AndroidNotificationService : IPlatformNotificationService
{
    private readonly object sync = new();
    private readonly Context context;
    private readonly NotificationManager manager;
    private readonly AndroidManifestInspector manifest;
    private bool shutdown;
    internal AndroidNotificationService(Context context)
    {
        this.context = context.ApplicationContext!;
        manager = (NotificationManager)this.context.GetSystemService(Context.NotificationService)!;
        manifest = new AndroidManifestInspector(this.context);
    }
    public PlatformServiceStatus Status
    {
        get
        {
            lock (sync)
            {
                if (shutdown) return PlatformServiceStatus.Shutdown;
                int api = (int)global::Android.OS.Build.VERSION.SdkInt;
                var permission = PlatformPermissionStatus.Granted;
                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                    permission = !manifest.GetDeclaredPermissions().Contains(AndroidPermissionMapper.PostNotifications)
                        ? PlatformPermissionStatus.NotDeclared :
                        context.CheckSelfPermission(AndroidPermissionMapper.PostNotifications) == Permission.Granted
                            ? PlatformPermissionStatus.Granted : PlatformPermissionStatus.Denied;
                bool enabled = !OperatingSystem.IsAndroidVersionAtLeast(24) || manager.AreNotificationsEnabled();
                bool channelEnabled = true;
                if (OperatingSystem.IsAndroidVersionAtLeast(26))
                {
                    using var channel = manager.GetNotificationChannel(AndroidNotificationPolicy.ChannelId);
                    channelEnabled = channel is null || channel.Importance != NotificationImportance.None;
                }
                return AndroidNotificationPolicy.Evaluate(api, permission, enabled, channelEnabled, shutdown);
            }
        }
    }
    public PlatformServiceStatus Show(PlatformNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.Id);
        ArgumentNullException.ThrowIfNull(notification.Title);
        ArgumentNullException.ThrowIfNull(notification.Body);
        lock (sync)
        {
            if (Status != PlatformServiceStatus.Success) return Status;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                // Reusing a channel must preserve user importance/sound choices.
                using var existing = manager.GetNotificationChannel(AndroidNotificationPolicy.ChannelId);
                if (existing is null)
                {
                    using var channel = new NotificationChannel(AndroidNotificationPolicy.ChannelId, "Application notifications", NotificationImportance.Default);
                    manager.CreateNotificationChannel(channel);
                }
            }
            using var builder = OperatingSystem.IsAndroidVersionAtLeast(26)
                ? new Notification.Builder(context, AndroidNotificationPolicy.ChannelId) : new Notification.Builder(context);
            builder.SetContentTitle(notification.Title);
            builder.SetContentText(notification.Body);
            builder.SetSmallIcon(context.ApplicationInfo?.Icon is > 0 ? context.ApplicationInfo.Icon : global::Android.Resource.Drawable.IcDialogInfo);
            builder.SetAutoCancel(true);
            using var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
            using var pending = launch is null ? null : PendingIntent.GetActivity(context, 0,
                launch.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop),
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            if (pending is not null) builder.SetContentIntent(pending);
            using var native = builder.Build();
            try
            {
                // Tag+integer is Android's exact identity; do not hash IDs into colliding integers.
                manager.Notify(notification.Id, 0, native);
                return PlatformServiceStatus.Success;
            }
            catch (Java.Lang.SecurityException) { return PlatformServiceStatus.PermissionDenied; }
        }
    }
    public PlatformServiceStatus Dismiss(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (sync)
        {
            if (shutdown) return PlatformServiceStatus.Shutdown;
            manager.Cancel(id, 0);
            return PlatformServiceStatus.Success;
        }
    }
    internal void Shutdown() { lock (sync) shutdown = true; }
}
