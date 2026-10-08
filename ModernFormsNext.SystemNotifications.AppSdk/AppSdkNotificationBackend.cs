using System.Runtime.InteropServices;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppLifecycle;
using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;
using ModernFormsNext.WindowKit.Backend.Windows.Notifications;
using Windows.Data.Xml.Dom;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;

namespace ModernFormsNext.SystemNotifications.AppSdk;

/// <summary>Optional stable Windows App SDK transport; loaded only on build 17763 or later.</summary>
/// <remarks>The app/installer must deploy Windows App Runtime. No installer is launched here. Stable AUMID
/// registration additionally enables WinRT NotificationData for indeterminate progress, which the App SDK
/// double-valued ProgressData API cannot represent. Both APIs address the same native notification identity.
/// Like the classic provider, it reserves native group <c>mfn.default</c> for the empty application
/// group; other logical groups use stable hashes, with original identities recovered from payloads.</remarks>
public sealed class AppSdkNotificationBackend : IWindowsNotificationBackend
{
    private AppNotificationManager? manager;
    private ToastNotifier? progressNotifier;
    private Action<SystemNotificationActivation>? activated;
    private Action<SystemNotificationChange>? changed;
    private bool packaged;
    private string? appId;
    private bool bootstrapOwned;
    private bool registered;
    private int disposed;

    private readonly Guid historyOwner = Guid.NewGuid();

    // Native IDs identify individual entries even when another library omitted Tag/Group.
    // Keep this transport detail inside the opaque, provider-session-scoped reference.
    private sealed record HistoryReference(Guid Owner, uint Id) : SystemNotificationReference;

    /// <inheritdoc/>
    public SystemNotificationCapabilities Capabilities { get; private set; } = SystemNotificationCapabilities.Unavailable;

    /// <inheritdoc/>
    public void Initialize(WindowsNotificationRegistrationOptions options, Action<SystemNotificationActivation> activated, Action<SystemNotificationChange> changed)
    {
        this.activated = activated;
        this.changed = changed;
        uint length = 0;
        packaged = GetCurrentPackageFullName(ref length, 0) != 15700;
        appId = options.AppUserModelId;
        try
        {
            if (!packaged)
            {
                if (!Bootstrap.TryInitialize(0x00020000, "", new PackageVersion(0), Bootstrap.InitializeOptions.None, out int hr))
                    Marshal.ThrowExceptionForHR(hr);
                bootstrapOwned = true;
                if (options.AppUserModelId is string appId)
                {
                    SystemNotificationValidation.CheckString(appId, 128, false);
                    Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(appId));
                }
            }
            // DeploymentManager requires package identity. For an ordinary EXE, Register itself
            // probes the Singleton COM server; never call Initialize or install packages here.
            if (packaged && DeploymentManager.GetStatus().Status != DeploymentStatus.Ok)
                throw new PlatformNotSupportedException("Windows App Runtime deployment is incomplete; the installer must provision the Framework, Main and Singleton packages.");
            if (!AppNotificationManager.IsSupported()) throw new PlatformNotSupportedException("App notifications are unavailable for this process.");
            manager = AppNotificationManager.Default;
            manager.NotificationInvoked += OnInvoked;
            if (options.DisplayName is not null || options.DisplayIcon is not null)
            {
                if (options.DisplayName is null || options.DisplayIcon is null || packaged)
                    throw new ArgumentException("Custom branding requires both DisplayName and DisplayIcon on an unpackaged app.");
                manager.Register(options.DisplayName, options.DisplayIcon);
            }
            else
            {
                try { manager.Register(); }
                catch (COMException e) when (e.HResult == unchecked((int)0x80040154))
                { throw new PlatformNotSupportedException("Windows App Runtime Singleton registration is unavailable; install the complete Windows App Runtime redistributable.", e); }
            }
            registered = true;
            // With the SDK's COM launch marker, its first native callback stores activation in
            // AppInstance instead of raising NotificationInvoked. Read it only after Register;
            // the service's startup relay keeps the copied payload until the UI begins dispatching.
            var startup = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (startup.Kind == ExtendedActivationKind.AppNotification && startup.Data is AppNotificationActivatedEventArgs notificationArgs)
                OnInvoked(manager, notificationArgs);
            if (packaged) progressNotifier = ToastNotificationManager.CreateToastNotifier();
            else if (options.AppUserModelId is string appId) progressNotifier = ToastNotificationManager.CreateToastNotifier(appId);
            var probed = WindowsNotificationBackendSelector.GetToastCapabilities(Environment.OSVersion.Version, packaged, true, true,
                AppNotificationBuilder.IsUrgentScenarioSupported(), AppNotificationButton.IsButtonStyleSupported(),
                AppNotificationButton.IsToolTipSupported(), true, true);
            var features = probed.Features | SystemNotificationFeatures.RemoteImages;
            var windows = probed.GetPlatform<WindowsSystemNotificationCapabilities>()!.Features;
            if (progressNotifier is null) features &= ~SystemNotificationFeatures.IndeterminateProgress;
            if (ApiInformation.IsPropertyPresent("Windows.UI.Notifications.ToastNotification", "ExpiresOnReboot"))
                windows |= WindowsSystemNotificationFeatures.RebootExpiration;
            if (progressNotifier is not null) windows |= WindowsSystemNotificationFeatures.Mirroring;
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100) && ApiInformation.IsPropertyPresent("Windows.UI.Notifications.ToastNotification", "IsExpandableContentSupported") && ToastNotification.IsExpandableContentSupported)
                windows |= WindowsSystemNotificationFeatures.ExpandableContent;
            Capabilities = new("Windows App SDK", features, progressNotifier is null
                ? "Set an explicit AppUserModelId at registration to enable indeterminate progress through native NotificationData." : null) { Platform = new WindowsSystemNotificationCapabilities(windows), Limits = probed.Limits };
        }
        catch { Cleanup(); throw; }
    }

    /// <inheritdoc/>
    public Task<SystemNotificationAccess> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(WindowsNotificationAccess.FromSetting(manager!.Setting.ToString()));
    }

    /// <inheritdoc/>
    public async Task<SystemNotificationResult> ShowAsync(SystemNotification notification, CancellationToken cancellationToken)
    {
        var content = await Task.Run(() => WindowsToastContent.Create(notification, Capabilities), cancellationToken).ConfigureAwait(false);
        var nativeGroup = WindowsToastContent.ToNativeGroup(content.Key.Group);
        cancellationToken.ThrowIfCancellationRequested();
        if (manager!.Setting != AppNotificationSetting.Enabled) return new(SystemNotificationStatus.Disabled, content.Key, content.Warnings);
        if (progressNotifier is not null && (content.Notification.Progress is not null || content.Options.AllowMirroring is not null || content.Options.RemoteId is not null))
        {
            // NotificationData is the OS-native path for string-valued "indeterminate" as well as
            // numeric values. App SDK registration/activation remains the owner of this AUMID.
            var document = new XmlDocument();
            document.LoadXml(content.Xml);
            var toast = new ToastNotification(document)
            {
                Tag = WindowsToastContent.ToNativeTag(content.Key.Id), Group = nativeGroup, SuppressPopup = content.Options.SuppressPopup
            };
            if (content.Notification.Progress is { } progress) toast.Data = Data(progress, 0);
            if (content.Options.AllowMirroring is bool mirror) toast.NotificationMirroring = mirror ? NotificationMirroring.Allowed : NotificationMirroring.Disabled;
            if (content.Options.RemoteId is string remote) toast.RemoteId = remote;
            if (content.Notification.ExpiresAt is { } expires) toast.ExpirationTime = expires;
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362) && content.Options.ExpiresOnReboot is bool reboot) toast.ExpiresOnReboot = reboot;
            if (content.Options.HighPriority) toast.Priority = ToastNotificationPriority.High;
            progressNotifier.Show(toast);
        }
        else
        {
            if (content.Notification.Progress is { Value: null })
                return new(SystemNotificationStatus.Unsupported, content.Key, [new("IndeterminateIdentity", "Indeterminate progress requires explicit AppUserModelId registration or package identity.")]);
            var native = new AppNotification(content.Xml)
            {
                Tag = WindowsToastContent.ToNativeTag(content.Key.Id), Group = nativeGroup, SuppressDisplay = content.Options.SuppressPopup,
                Priority = content.Options.HighPriority ? AppNotificationPriority.High : AppNotificationPriority.Default
            };
            if (content.Notification.ExpiresAt is { } expires) native.Expiration = expires;
            if (content.Options.ExpiresOnReboot is bool reboot) native.ExpiresOnReboot = reboot;
            if (content.Notification.Progress is { } p) native.Progress = Progress(p, await AppSdkProgressSequence.NextAsync(cancellationToken).ConfigureAwait(false));
            cancellationToken.ThrowIfCancellationRequested();
            manager.Show(native);
            if (native.Id == 0) return new(SystemNotificationStatus.Failed, content.Key, content.Warnings);
        }
        return new(SystemNotificationStatus.Accepted, content.Key, content.Warnings);
    }

    /// <inheritdoc/>
    public async Task<SystemNotificationResult> UpdateAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken cancellationToken)
    {
        WindowsToastContent.ValidateProgress(progress);
        cancellationToken.ThrowIfCancellationRequested();
        if (manager!.Setting != AppNotificationSetting.Enabled) return new(SystemNotificationStatus.Disabled, key);
        var nativeGroup = WindowsToastContent.ToNativeGroup(key.Group);
        if (progressNotifier is not null)
        {
            // History does not round-trip the update sequence. Zero is the documented WinRT
            // always-update mode; the canonical service serializes submissions, also after restart.
            var data = Data(progress, 0);
            var result = progressNotifier.Update(data, WindowsToastContent.ToNativeTag(key.Id), nativeGroup);
            return new(result switch
            {
                NotificationUpdateResult.Succeeded => SystemNotificationStatus.Accepted,
                NotificationUpdateResult.NotificationNotFound => SystemNotificationStatus.NotFound,
                _ => SystemNotificationStatus.Failed
            }, key);
        }
        if (progress.Value is null) return new(SystemNotificationStatus.Unsupported, key);
        // App SDK disallows zero, unlike WinRT. Reserve a durable, content-free counter before
        // submission so a process restart cannot accidentally send an older sequence.
        var existing = (await manager.GetAllAsync()).FirstOrDefault(n => n.Tag == WindowsToastContent.ToNativeTag(key.Id) && n.Group == nativeGroup);
        if (existing is null) return new(SystemNotificationStatus.NotFound, key);
        // Windows may initialize the stream from its native notification sequence/ID instead of
        // the supplied initial progress counter. Reserve above both that floor and our last value.
        uint sequence = await AppSdkProgressSequence.NextAsync(cancellationToken, existing.Id).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var status = await manager.UpdateAsync(Progress(progress, sequence), WindowsToastContent.ToNativeTag(key.Id), nativeGroup);
        return new(status switch
        {
            AppNotificationProgressResult.Succeeded => SystemNotificationStatus.Accepted,
            AppNotificationProgressResult.AppNotificationNotFound => SystemNotificationStatus.NotFound,
            _ => SystemNotificationStatus.Unsupported
        }, key);
    }

    /// <inheritdoc/>
    public async Task<SystemNotificationResult> RemoveAsync(SystemNotificationKey? key, string? tag, string? group, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (key is not null) await manager!.RemoveByTagAndGroupAsync(WindowsToastContent.ToNativeTag(key.Id), WindowsToastContent.ToNativeGroup(key.Group));
        else if (tag is not null) await manager!.RemoveByTagAsync(WindowsToastContent.ToNativeTag(tag));
        else if (group is not null) await manager!.RemoveByGroupAsync(WindowsToastContent.ToNativeGroup(group));
        else await manager!.RemoveAllAsync();
        return new(SystemNotificationStatus.Accepted, key);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var native = await manager!.GetAllAsync();
        return native.Select(n => WindowsToastContent.ReadHistory(n.Payload, n.Tag, n.Group, historyOwner) with
        { Reference = new HistoryReference(historyOwner, n.Id) }).ToArray();
    }

    /// <inheritdoc/>
    public async Task<SystemNotificationResult> DismissHistoryAsync(SystemNotificationReference reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (reference is not HistoryReference item || item.Owner != historyOwner) return new(SystemNotificationStatus.Invalid);
        await manager!.RemoveByIdAsync(item.Id);
        return new(SystemNotificationStatus.Accepted);
    }

    private void OnInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        try
        {
            if (args.UserInput.Count > 5) throw new ArgumentException("Too many activation inputs.");
            activated?.Invoke(WindowsToastContent.DecodeActivation(args.Argument, args.UserInput.ToDictionary(p => p.Key, p => p.Value)));
        }
        catch (Exception e)
        {
            // The native callback must not unwind into COM, even during dispatcher shutdown.
            System.Diagnostics.Trace.TraceWarning("Notification activation rejected ({0:X8}).", e.HResult);
        }
    }

    private static AppNotificationProgressData Progress(SystemNotificationProgress progress, uint sequence) => new(sequence)
    { Title = progress.Title, Value = progress.Value!.Value, Status = progress.Status, ValueStringOverride = progress.ValueText };

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
    public ValueTask DisposeAsync() { Interlocked.Exchange(ref disposed, 1); Cleanup(); return ValueTask.CompletedTask; }
    private void Cleanup()
    {
        try
        {
            if (manager is not null)
            {
                manager.NotificationInvoked -= OnInvoked;
                if (registered) manager.Unregister();
            }
        }
        finally
        {
            registered = false; manager = null; progressNotifier = null; activated = null; changed = null;
            if (bootstrapOwned) { bootstrapOwned = false; Bootstrap.Shutdown(); }
        }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentPackageFullName(ref uint length, nint name);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
