using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit.Backend.Windows.Notifications;

/// <summary>Registered Windows OS notification service with optional native transports.</summary>
/// <remarks>Use Register on the UI thread before Application.Run. Dispose asynchronously before the dispatcher
/// stops. Never synchronously wait on DisposeAsync from the UI thread. Registration persists for cold launch;
/// disposal revokes only the running process's handlers. Uninstall identity belongs to the installer.</remarks>
public sealed class WindowsSystemNotificationService : SystemNotificationServiceBase
{
    private readonly IWindowsNotificationBackend? backend;
    private readonly SystemNotificationCapabilities capabilities;

    private WindowsSystemNotificationService(IWindowsNotificationBackend? backend, string? reason = null)
        : base(action => Dispatcher.UIThread.Post(action), activation =>
        {
            if (PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>() is IPlatformApplicationLifecycleController lifecycle)
                lifecycle.Activate(PlatformApplicationActivation.FromNotification(activation));
        })
    {
        this.backend = backend;
        capabilities = backend?.Capabilities ?? new("Unavailable", SystemNotificationFeatures.None, reason);
        if (reason is not null && backend is not null) capabilities = capabilities with { Reason = reason };
    }

    /// <summary>Explicitly registers notification activation and the canonical platform service.</summary>
    /// <param name="options">Backend/identity policy. App SDK Register writes its documented per-user registration.</param>
    /// <returns>The application-owned service; inspect Capabilities.Reason for fallback/unavailability.</returns>
    /// <remarks>Optional providers are loaded only after OS gates. Add their project/package references to the
    /// application. Merely referencing ModernFormsNext never requires Windows App SDK or changes app identity.</remarks>
    public static WindowsSystemNotificationService Register(WindowsNotificationRegistrationOptions? options = null)
    {
        options ??= new();
        WindowsPlatformBootstrap.Initialize();
        Dispatcher.UIThread.VerifyAccess();
        if (PlatformServiceRegistry.GetService<ISystemNotificationService>() is not null)
            throw new InvalidOperationException("A system notification service is already registered for this process.");
        if (!Enum.IsDefined(options.Backend)) throw new ArgumentException("Unknown backend preference.", nameof(options));
        using var identity = WindowsIdentity.GetCurrent();
        bool elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        var version = Environment.OSVersion.Version;
        var attempts = new List<string>();
        IWindowsNotificationBackend? selected = null;
        WindowsSystemNotificationService? service = null;
        foreach (var kind in new[] { WindowsNotificationBackendKind.AppSdk, WindowsNotificationBackendKind.Classic, WindowsNotificationBackendKind.Shell })
        {
            if (options.Backend != WindowsNotificationBackendKind.Automatic && options.Backend != kind) continue;
            var env = new WindowsNotificationEnvironment(version, kind == WindowsNotificationBackendKind.AppSdk, kind == WindowsNotificationBackendKind.Classic, true, elevated);
            var candidate = WindowsNotificationBackendSelector.Select(env, options with { Backend = kind });
            if (candidate == WindowsNotificationBackendKind.Unavailable || kind == WindowsNotificationBackendKind.Shell && options.Backend == WindowsNotificationBackendKind.Automatic && !options.AllowShellFallback) continue;
            try
            {
                selected = kind == WindowsNotificationBackendKind.Shell ? new ShellBalloonNotificationBackend() : Load(kind);
                var relay = new StartupRelay();
                // COM activation can arrive synchronously during Register, before the wrapper is
                // assigned. Buffer copied payloads until binding; never drop the first cold click.
                selected.Initialize(options, relay.Activate, relay.Change);
                service = new(selected, attempts.Count == 0 ? null : string.Join(" ", attempts));
                relay.Bind(service);
                return PlatformServiceRegistry.Register<ISystemNotificationService>(service) as WindowsSystemNotificationService ?? throw new InvalidOperationException();
            }
            catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeLoadException or TargetInvocationException or COMException or Win32Exception or PlatformNotSupportedException)
            {
                // Optional provider initialization must unwind its own partially-created native state.
                System.Diagnostics.Trace.TraceWarning("Notification provider {0}: {1}", kind, exception);
                attempts.Add($"{kind} unavailable (0x{exception.HResult:X8})." +
                    (exception is PlatformNotSupportedException ? " " + exception.Message : ""));
                selected = null;
            }
        }
        service = new(null, elevated ? "Elevated processes cannot use toast activation. " + string.Join(" ", attempts) : string.Join(" ", attempts));
        PlatformServiceRegistry.Register<ISystemNotificationService>(service);
        return service;
    }

    private static IWindowsNotificationBackend Load(WindowsNotificationBackendKind kind)
    {
        string suffix = kind == WindowsNotificationBackendKind.AppSdk ? "AppSdk" : "WinRT";
        string assemblyName = "ModernFormsNext.SystemNotifications." + suffix;
        var assembly = Assembly.Load(new AssemblyName(assemblyName));
        var type = assembly.GetType(assemblyName + "." + suffix + "NotificationBackend", throwOnError: true)!;
        return (IWindowsNotificationBackend)Activator.CreateInstance(type)!;
    }

    private sealed class StartupRelay
    {
        private readonly object gate = new();
        private readonly Queue<SystemNotificationActivation> pending = new();
        private WindowsSystemNotificationService? target;
        internal void Activate(SystemNotificationActivation activation)
        {
            lock (gate)
            {
                if (target is not null) target.PublishActivation(activation);
                else if (pending.Count < 32) pending.Enqueue(SystemNotificationValidation.CopyActivation(activation));
                else System.Diagnostics.Trace.TraceWarning("Notification startup activation queue is full (32 entries).");
            }
        }
        internal void Change(SystemNotificationChange change) { lock (gate) target?.PublishChange(change); }
        internal void Bind(WindowsSystemNotificationService service)
        {
            lock (gate)
            {
                target = service;
                while (pending.TryDequeue(out var activation)) service.PublishActivation(activation);
            }
        }
    }

    /// <inheritdoc/>
    public override SystemNotificationCapabilities Capabilities => capabilities;
    /// <inheritdoc/>
    protected override Task<SystemNotificationAccess> GetStatusCoreAsync(CancellationToken cancellationToken)
        => backend?.GetStatusAsync(cancellationToken) ?? Task.FromResult(SystemNotificationAccess.Unavailable);
    /// <inheritdoc/>
    protected override Task<SystemNotificationResult> DismissHistoryCoreAsync(SystemNotificationReference reference, CancellationToken cancellationToken)
        => backend is null ? Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported)) : Guard(() => backend.DismissHistoryAsync(reference, cancellationToken));
    /// <inheritdoc/>
    protected override Task<SystemNotificationResult> ShowCoreAsync(SystemNotification notification, CancellationToken cancellationToken)
        => Guard(() => backend!.ShowAsync(notification, cancellationToken));
    /// <inheritdoc/>
    protected override Task<SystemNotificationResult> UpdateCoreAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken cancellationToken)
        => Guard(() => backend!.UpdateAsync(key, progress, cancellationToken));
    /// <inheritdoc/>
    protected override Task<SystemNotificationResult> RemoveCoreAsync(SystemNotificationKey? key, string? id, string? group, CancellationToken cancellationToken)
        => backend is null ? Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported)) : Guard(() => backend.RemoveAsync(key, id, group, cancellationToken));
    /// <inheritdoc/>
    protected override Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryCoreAsync(CancellationToken cancellationToken)
        => backend!.GetHistoryAsync(cancellationToken);
    /// <inheritdoc/>
    protected override ValueTask DisposeCoreAsync() => backend?.DisposeAsync() ?? ValueTask.CompletedTask;

    private static async Task<SystemNotificationResult> Guard(Func<Task<SystemNotificationResult>> operation)
    {
        try { return await operation().ConfigureAwait(false); }
        catch (ArgumentException) { return new(SystemNotificationStatus.Invalid); }
        catch (System.Xml.XmlException) { return new(SystemNotificationStatus.Invalid); }
        catch (WindowsNotificationContentException e) { return new(SystemNotificationStatus.Unsupported, Warnings: e.Warnings); }
        catch (NotSupportedException) { return new(SystemNotificationStatus.Unsupported); }
        catch (Exception exception) when (exception is COMException or Win32Exception or IOException or UnauthorizedAccessException)
        { return new(SystemNotificationStatus.Failed, ErrorCode: exception.HResult); }
    }
}
