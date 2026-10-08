using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;
using ModernFormsNext.WindowKit.Backend.Windows.Notifications;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;

namespace ModernFormsNext.SystemNotifications.WinRT;

/// <summary>Optional classic OS WinRT toast transport, independent of Windows App SDK.</summary>
/// <remarks>Unpackaged applications must install their AUMID shortcut and COM activator registration.
/// Missing installer identity is reported, never silently installed. Windows 8 legacy templates remain
/// an isolated design path; this .NET 10 assembly is not a Windows 8 compatibility runtime.
/// The native group <c>mfn.default</c> represents the empty logical group. Nonempty groups use stable hashes.</remarks>
public sealed class WinRTNotificationBackend : IWindowsNotificationBackend
{
    private readonly object gate = new();
    private readonly Dictionary<SystemNotificationKey, Subscription> subscriptions = [];
    private ToastNotifier? notifier;
    private ClassicNotificationActivator? activator;
    private string? appId;
    private bool packaged;
    private bool legacy;
    private bool disposed;
    private Action<SystemNotificationActivation>? activated;
    private Action<SystemNotificationChange>? changed;

    private readonly Guid historyOwner = Guid.NewGuid();

    /// <inheritdoc/>
    public SystemNotificationCapabilities Capabilities { get; private set; } = SystemNotificationCapabilities.Unavailable;

    /// <inheritdoc/>
    public void Initialize(WindowsNotificationRegistrationOptions options, Action<SystemNotificationActivation> activated, Action<SystemNotificationChange> changed)
    {
        this.activated = activated; this.changed = changed;
        uint length = 0;
        packaged = GetCurrentPackageFullName(ref length, 0) != 15700;
        appId = options.AppUserModelId;
        if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.ToastNotificationManager")) throw new PlatformNotSupportedException("WinRT toast API unavailable.");
        if (!packaged && string.IsNullOrWhiteSpace(appId)) throw new PlatformNotSupportedException("Classic desktop toast requires an installed AppUserModelId shortcut.");
        if (appId is not null) SystemNotificationValidation.CheckString(appId, 128, false);
        try
        {
            if (options.ClassicActivatorId is Guid clsid)
                activator = new ClassicNotificationActivator(clsid, appId, (args, inputs) => OnActivation(args, inputs));
            // Generic desktop toast requires the COM activator for persistent/cold activation.
            // Without one retain the original desktop-template/event behavior, not broken buttons.
            legacy = !OperatingSystem.IsWindowsVersionAtLeast(10) || activator is null;
            notifier = packaged ? ToastNotificationManager.CreateToastNotifier() : ToastNotificationManager.CreateToastNotifier(appId!);
            bool updates = !legacy && ApiInformation.IsPropertyPresent("Windows.UI.Notifications.ToastNotification", "Data");
            bool history = ApiInformation.IsPropertyPresent("Windows.UI.Notifications.ToastNotificationManager", "History");
            var version = legacy ? new Version(6, 2) : Environment.OSVersion.Version;
            var probed = WindowsNotificationBackendSelector.GetToastCapabilities(version, packaged, updates, history, false, false, false,
                !legacy && activator is not null, !legacy && ApiInformation.IsPropertyPresent("Windows.UI.Notifications.ToastNotification", "Priority"));
            var features = probed.Features;
            var windows = probed.GetPlatform<WindowsSystemNotificationCapabilities>()!.Features;
            if (!legacy && ApiInformation.IsPropertyPresent("Windows.UI.Notifications.ToastNotification", "ExpiresOnReboot")) windows |= WindowsSystemNotificationFeatures.RebootExpiration;
            if (!legacy && ApiInformation.IsPropertyPresent("Windows.UI.Notifications.ToastNotification", "NotificationMirroring")) windows |= WindowsSystemNotificationFeatures.Mirroring;
            if (!legacy && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100) && ApiInformation.IsPropertyPresent("Windows.UI.Notifications.ToastNotification", "IsExpandableContentSupported") && ToastNotification.IsExpandableContentSupported)
                windows |= WindowsSystemNotificationFeatures.ExpandableContent;
            Capabilities = new("Classic WinRT" + (legacy ? " templates" : " toast"), features | SystemNotificationFeatures.DismissalEvents,
                legacy ? "Legacy template activation is available only while the process is running; install COM activation for adaptive desktop toast." : null) { Platform = new WindowsSystemNotificationCapabilities(windows), Limits = probed.Limits };
        }
        catch { activator?.DisposeAsync().GetAwaiter().GetResult(); activator = null; throw; }
    }

    /// <inheritdoc/>
    public Task<SystemNotificationAccess> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(WindowsNotificationAccess.FromSetting(notifier!.Setting.ToString()));
    }

    /// <inheritdoc/>
    public async Task<SystemNotificationResult> ShowAsync(SystemNotification notification, CancellationToken cancellationToken)
    {
        var content = await Task.Run(() => WindowsToastContent.Create(notification, Capabilities, legacy), cancellationToken).ConfigureAwait(false);
        var nativeGroup = WindowsToastContent.ToNativeGroup(content.Key.Group);
        cancellationToken.ThrowIfCancellationRequested();
        if (notifier!.Setting != NotificationSetting.Enabled) return new(SystemNotificationStatus.Disabled, content.Key, content.Warnings);
        var document = new XmlDocument(); document.LoadXml(content.Xml);
        var toast = new ToastNotification(document);
        if (Capabilities.Supports(SystemNotificationFeatures.Grouping))
        {
            toast.Tag = WindowsToastContent.ToNativeTag(content.Key.Id);
            toast.Group = nativeGroup;
        }
        if (content.Notification.ExpiresAt is { } expires) toast.ExpirationTime = expires;
        if (content.Options.SuppressPopup) toast.SuppressPopup = true;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063) && content.Options.HighPriority) toast.Priority = ToastNotificationPriority.High;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362) && content.Options.ExpiresOnReboot is bool reboot) toast.ExpiresOnReboot = reboot;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
        {
            if (content.Options.AllowMirroring is bool mirror) toast.NotificationMirroring = mirror ? NotificationMirroring.Allowed : NotificationMirroring.Disabled;
            if (content.Options.RemoteId is string remote) toast.RemoteId = remote;
        }
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063) && content.Notification.Progress is { } progress) toast.Data = Data(progress, 0);
        var subscription = new Subscription(toast);
        subscription.Dismissed = (_, args) => Complete(content.Key, subscription, args.Reason.ToString(), null);
        subscription.Failed = (_, args) => Complete(content.Key, subscription, "Failed", args.ErrorCode.HResult);
        if (legacy)
        {
            // Legacy events do not supply arguments. Copy the exact launch envelope already sent
            // to Windows so logical Group survives without reading native XML inside the callback.
            string launchArguments = document.DocumentElement.GetAttribute("launch");
            subscription.Activated = (_, _) =>
            {
                lock (gate) { if (disposed || !subscriptions.TryGetValue(content.Key, out var current) || !ReferenceEquals(current, subscription)) return; }
                OnActivation(launchArguments, new Dictionary<string, string>());
            };
        }
        toast.Dismissed += subscription.Dismissed;
        toast.Failed += subscription.Failed;
        if (subscription.Activated is not null) toast.Activated += subscription.Activated;
        lock (gate)
        {
            if (subscriptions.Remove(content.Key, out var old)) old.Detach();
            // Native history is authoritative and persists independently. Bound only the in-process
            // event subscriptions; an OS can keep history for longer than the provider lives.
            if (subscriptions.Count == 128)
            {
                var oldest = subscriptions.First(); subscriptions.Remove(oldest.Key); oldest.Value.Detach();
            }
            subscriptions.Add(content.Key, subscription);
        }
        try { notifier.Show(toast); }
        catch { lock (gate) { subscriptions.Remove(content.Key); subscription.Detach(); } throw; }
        return new(SystemNotificationStatus.Accepted, content.Key, content.Warnings);
    }

    private void Complete(SystemNotificationKey key, Subscription subscription, string reason, int? error)
    {
        lock (gate)
        {
            if (disposed || !subscriptions.TryGetValue(key, out var current) || !ReferenceEquals(current, subscription)) return;
            // Dismissed describes removal of the banner, not removal from Notification Center.
            // Keep activation alive for legacy templates; release handlers on replacement/dispose.
        }
        try { changed?.Invoke(new(key, reason, error)); }
        catch (Exception e) { System.Diagnostics.Trace.TraceWarning("Native toast callback could not be dispatched ({0:X8}).", e.HResult); }
    }

    private void OnActivation(string args, IReadOnlyDictionary<string, string> inputs)
    {
        lock (gate) { if (disposed) return; }
        try
        {
            activated?.Invoke(WindowsToastContent.DecodeActivation(args, inputs));
        }
        catch (Exception e) { System.Diagnostics.Trace.TraceWarning("Native toast activation rejected ({0:X8}).", e.HResult); }
    }

    /// <inheritdoc/>
    public Task<SystemNotificationResult> UpdateAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken cancellationToken)
    {
        WindowsToastContent.ValidateProgress(progress);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063) || !Capabilities.Supports(SystemNotificationFeatures.LiveUpdates))
            return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported, key));
        // NotificationData sequence zero means always apply. Calls are already serialized by
        // the service; no unreliable history sequence or process-local counter is needed.
        var data = Data(progress, 0);
        var status = notifier!.Update(data, WindowsToastContent.ToNativeTag(key.Id), WindowsToastContent.ToNativeGroup(key.Group));
        return Task.FromResult(new SystemNotificationResult(status switch
        {
            NotificationUpdateResult.Succeeded => SystemNotificationStatus.Accepted,
            NotificationUpdateResult.NotificationNotFound => SystemNotificationStatus.NotFound,
            _ => SystemNotificationStatus.Failed
        }, key));
    }

    /// <inheritdoc/>
    public Task<SystemNotificationResult> RemoveAsync(SystemNotificationKey? key, string? tag, string? group, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Capabilities.Supports(SystemNotificationFeatures.History))
        {
            if (key is not null) RemoveHistory(WindowsToastContent.ToNativeTag(key.Id), WindowsToastContent.ToNativeGroup(key.Group));
            else if (tag is not null)
            {
                foreach (var toast in History().Where(t => t.Tag == WindowsToastContent.ToNativeTag(tag))) RemoveHistory(toast.Tag, toast.Group);
            }
            else if (group is not null)
            {
                var nativeGroup = WindowsToastContent.ToNativeGroup(group);
                if (packaged) ToastNotificationManager.History.RemoveGroup(nativeGroup); else ToastNotificationManager.History.RemoveGroup(nativeGroup, appId!);
            }
            else if (packaged) ToastNotificationManager.History.Clear(); else ToastNotificationManager.History.Clear(appId!);
        }
        else if (key is not null)
        {
            lock (gate) if (subscriptions.TryGetValue(key, out var subscription)) notifier!.Hide(subscription.Toast);
        }
        else if (tag is not null)
        {
            lock (gate) foreach (var item in subscriptions.Where(item => item.Key.Id == tag).ToArray()) notifier!.Hide(item.Value.Toast);
        }
        else return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported));
        lock (gate)
        {
            foreach (var item in subscriptions.Where(p => key is not null ? p.Key == key : tag is not null ? p.Key.Id == tag : group is null || p.Key.Group == group).ToArray())
            { subscriptions.Remove(item.Key); item.Value.Detach(); }
        }
        return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Accepted, key));
    }
    private IReadOnlyList<ToastNotification> History() => packaged ? ToastNotificationManager.History.GetHistory() : ToastNotificationManager.History.GetHistory(appId!);
    private void RemoveHistory(string tag, string group)
    {
        if (packaged) ToastNotificationManager.History.Remove(tag, group); else ToastNotificationManager.History.Remove(tag, group, appId!);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<SystemNotificationHistoryEntry>>(History().Select(t => WindowsToastContent.ReadHistory(t.Content.GetXml(), t.Tag, t.Group, historyOwner)).ToArray());
    }

    /// <inheritdoc/>
    public Task<SystemNotificationResult> DismissHistoryAsync(SystemNotificationReference reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (reference is not WindowsSystemNotificationReference item || item.Owner != historyOwner) return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Invalid));
        // Classic history exposes no numeric ID. An untagged foreign entry cannot be removed
        // individually with History.Remove; never broaden that request to group/all removal.
        if (string.IsNullOrEmpty(item.Tag)) return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported,
            Warnings: [new("HistoryIdentity", "Classic history removal requires an entry with a native tag.")]));
        RemoveHistory(item.Tag, item.Group);
        return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Accepted));
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.15063")]
    private static NotificationData Data(SystemNotificationProgress p, uint sequence)
    {
        var data = new NotificationData { SequenceNumber = sequence };
        data.Values["progressTitle"] = p.Title;
        data.Values["progressValue"] = p.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "indeterminate";
        data.Values["progressStatus"] = p.Status;
        data.Values["progressValueString"] = p.ValueText;
        return data;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        lock (gate)
        {
            disposed = true;
            foreach (var subscription in subscriptions.Values) subscription.Detach();
            subscriptions.Clear(); activated = null; changed = null; notifier = null;
        }
        if (activator is not null) { await activator.DisposeAsync(); activator = null; }
    }

    private sealed class Subscription(ToastNotification toast)
    {
        public ToastNotification Toast { get; } = toast;
        public TypedEventHandler<ToastNotification, ToastDismissedEventArgs>? Dismissed;
        public TypedEventHandler<ToastNotification, ToastFailedEventArgs>? Failed;
        public TypedEventHandler<ToastNotification, object>? Activated;
        public void Detach()
        {
            if (Dismissed is not null) Toast.Dismissed -= Dismissed;
            if (Failed is not null) Toast.Failed -= Failed;
            if (Activated is not null) Toast.Activated -= Activated;
            Dismissed = null; Failed = null; Activated = null;
        }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentPackageFullName(ref uint length, nint name);
}
