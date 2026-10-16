using System.ComponentModel;
using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;
using ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit.Backend.Windows.Notifications;

// Uses only Shell/Win32 and the existing tray host. An eventual legacy-runtime package can
// extract this adapter without referencing either Windows SDK projection or Windows App SDK.
internal sealed class ShellBalloonNotificationBackend : IWindowsNotificationBackend
{
    private readonly ShellNotificationLeases<WindowsTrayIcon> icons = new(icon => icon.Dispose());
    private ITimer? leaseTimer;
    private Action<SystemNotificationActivation>? activated;
    private Action<SystemNotificationChange>? changed;
    private volatile bool disposed;
    public SystemNotificationCapabilities Capabilities { get; } = new("Shell balloon",
        SystemNotificationFeatures.Basic | SystemNotificationFeatures.Removal | SystemNotificationFeatures.Replacement | SystemNotificationFeatures.DismissalEvents,
        "Shell balloons are transient and require a running process; this does not certify Windows 7/.NET 10 support.");

    public Task<SystemNotificationAccess> GetStatusAsync(CancellationToken cancellationToken)
        => Task.FromResult(new SystemNotificationAccess(SystemNotificationAvailability.Unknown, Reason: "Shell has no reliable per-application notification-permission query."));
    public Task<SystemNotificationResult> DismissHistoryAsync(SystemNotificationReference reference, CancellationToken cancellationToken)
        => Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported));

    public void Initialize(WindowsNotificationRegistrationOptions options, Action<SystemNotificationActivation> activated, Action<SystemNotificationChange> changed)
    {
        this.activated = activated; this.changed = changed;
        leaseTimer = TimeProvider.System.CreateTimer(_ =>
        {
            if (disposed) return;
            Dispatcher.UIThread.Post(() => { if (!disposed) icons.Expire(); });
        }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    public Task<SystemNotificationResult> ShowAsync(SystemNotification notification, CancellationToken cancellationToken)
        => OnUi(() =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var key = SystemNotificationValidation.GetKey(notification);
            var warnings = new List<SystemNotificationWarning>();
            var options = notification.PlatformOptions.Get<WindowsSystemNotificationOptions>() ?? new();
            var balloon = options.Balloon ?? new();
            if (!Enum.IsDefined(balloon.Icon) || balloon.Icon == WindowsBalloonIcon.Custom && balloon.CustomIcon == 0)
                return new(SystemNotificationStatus.Invalid, key);
            if (options.RawXml is not null) return new(SystemNotificationStatus.Unsupported, key);
            if (options.Audio is not null || options.LoopAudio || options.Scenario != WindowsNotificationScenario.Default || options.Header is not null ||
                options.Attribution is not null || options.SuppressPopup || options.HighPriority ||
                options.Duration != WindowsNotificationDuration.Default || options.Language is not null || options.ExpiresOnReboot is not null ||
                options.AllowMirroring is not null || options.RemoteId is not null)
                warnings.Add(new("ShellOptions", "Toast-only delivery/content options were removed; Shell controls balloon timing."));
            var title = notification.Title;
            var message = string.Join("\n", new[] { notification.Message }.Concat(notification.Text.Select(t => t.Content)).Where(t => t.Length > 0));
            if (message.Length == 0) message = title;
            if (title.Length > 63 || message.Length > 255)
                warnings.Add(new("ShellTextTruncated", "Shell title/body were truncated to 63/255 UTF-16 characters."));
            if (warnings.Count > 0 && !notification.AllowDegradation) return new(SystemNotificationStatus.Unsupported, key, warnings.AsReadOnly());
            if (!icons.CanAllocate(key))
                return new(SystemNotificationStatus.Failed, key, [new("ShellCapacity", "At most 32 concurrent Shell notification identities are retained.")]);
            // Replacement removes the old icon before installing the new one; callback correlation
            // is per HWND, so an old timeout cannot dismiss a newer balloon with the same tag.
            icons.Remove(key);
            var tray = new WindowsTrayIcon();
            try
            {
                var icon = CopyIcon(LoadIconW(0, (nint)32512));
                if (icon == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                tray.AdoptNotificationIcon(icon);
                tray.Text = title;
                tray.BalloonMessage += nativeMessage =>
                {
                    // Post cleanup outside WndProc; destroying this HWND while dispatching its
                    // callback would make native lifetime ordering unnecessarily reentrant.
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (disposed || !icons.IsCurrent(key, tray)) return;
                        if (nativeMessage == 0x405) activated?.Invoke(new(notification.Id, notification.ActivationData, null, new Dictionary<string, string>()) { Group = notification.Group });
                        if (nativeMessage is 0x403 or 0x404 or 0x405)
                        {
                            icons.Remove(key, tray);
                            changed?.Invoke(new(key, nativeMessage == 0x404 ? "TimedOut" : nativeMessage == 0x405 ? "Activated" : "Hidden"));
                        }
                    });
                };
                tray.Visible = true;
                icons.Replace(key, tray);
                var flags = (NIIF)(int)balloon.Icon;
                if (options.Silent) flags |= NIIF.NOSOUND;
                if (balloon.LargeIcon) flags |= NIIF.LARGE_ICON;
                if (balloon.RespectQuietTime) flags |= NIIF.RESPECT_QUIET_TIME;
                tray.ShowSystemBalloon(Truncate(title, 63), Truncate(message, 255), flags, balloon.CustomIcon, balloon.Realtime);
                return new(SystemNotificationStatus.Accepted, key, warnings.AsReadOnly());
            }
            catch { if (!icons.Remove(key, tray)) tray.Dispose(); throw; }
        }, cancellationToken);

    public Task<SystemNotificationResult> UpdateAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken cancellationToken)
        => Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported, key));
    public Task<SystemNotificationResult> RemoveAsync(SystemNotificationKey? key, string? tag, string? group, CancellationToken cancellationToken)
        => OnUi(() =>
        {
            if (key is null && tag is not null)
            {
                foreach (var identity in icons.Keys.Where(identity => identity.Id == tag)) icons.Remove(identity);
                return new(SystemNotificationStatus.Accepted);
            }
            if (key is null) return new(SystemNotificationStatus.Unsupported);
            if (!icons.Remove(key)) return new(SystemNotificationStatus.NotFound, key);
            return new(SystemNotificationStatus.Accepted, key);
        }, cancellationToken);
    public Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SystemNotificationHistoryEntry>>([]);
    public async ValueTask DisposeAsync() => await OnUi(() =>
    {
        disposed = true;
        leaseTimer?.Dispose(); leaseTimer = null;
        icons.Dispose(); activated = null; changed = null;
        return new(SystemNotificationStatus.Accepted);
    }, CancellationToken.None).ConfigureAwait(false);

    private static string Truncate(string value, int limit)
        => value.Length <= limit ? value : value[..(char.IsHighSurrogate(value[limit - 1]) ? limit - 1 : limit)];
    private static Task<SystemNotificationResult> OnUi(Func<SystemNotificationResult> action, CancellationToken token)
    {
        if (Dispatcher.UIThread.CheckAccess()) { token.ThrowIfCancellationRequested(); return Task.FromResult(action()); }
        var completion = new TaskCompletionSource<SystemNotificationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            if (token.IsCancellationRequested) { completion.TrySetCanceled(token); return; }
            try { completion.TrySetResult(action()); }
            catch (Exception e) { completion.TrySetException(e); }
        });
        return completion.Task;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern nint LoadIconW(nint instance, nint name);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CopyIcon(nint icon);
}
