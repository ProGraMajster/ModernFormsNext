using ModernFormsNext.WindowKit.Backend.Android.Dispatching;
using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Backend.Android.Permissions;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Platform.Permissions;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit.Backend.Android;

/// <summary>
/// Initializes the Android-specific WindowKit platform foundation.
/// </summary>
/// <remarks>
/// The backend registers lifecycle, dispatcher, permission, animation-frame, and motion-policy
/// infrastructure and a host-managed Application/Form window backend using software Skia.
/// Android supports one main Form plus modal/popup descendants, with explicit desktop limitations.
/// SAF storage, URI launching, sharing and local notifications use the existing service registry.
/// Clipboard, camera/media feature services, WebView and drag-and-drop remain separate work.
/// </remarks>
public sealed class AndroidWindowKitBackend : IWindowKitBackend
{
    private readonly object sync = new();
    private readonly AndroidWindowKitOptions options;
    private AndroidPlatformAnimationSettings animationSettings = null!;
    private AndroidChoreographerAnimationFrameSource animationFrameSource = null!;
    internal Services.AndroidActivityResultCoordinator ServiceRequests { get; private set; } = null!;
    private Services.AndroidNotificationService notifications = null!;
    internal Windowing.AndroidWindowingPlatform Windowing { get; private set; } = null!;

    /// <summary>
    /// Creates an Android backend using explicit host options.
    /// </summary>
    /// <param name="options">The Android application and lifecycle configuration.</param>
    public AndroidWindowKitBackend(AndroidWindowKitOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string PlatformName => "Android";

    /// <inheritdoc/>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the process-wide Android application context after initialization.
    /// </summary>
    public AndroidApplicationContext ApplicationContext { get; private set; } = null!;

    /// <summary>
    /// Gets the lifecycle-aware activity tracker after initialization.
    /// </summary>
    public AndroidActivityTracker ActivityTracker { get; private set; } = null!;

    /// <summary>Gets the normalized lifecycle and activation source shared with framework consumers.</summary>
    /// <remarks>Access after initialization on the Android UI thread.</remarks>
    public IPlatformApplicationLifecycleNotifications Lifecycle => ActivityTracker.Publisher;

    /// <summary>
    /// Gets the Android main-thread dispatcher after initialization.
    /// </summary>
    public AndroidMainThreadDispatcher Dispatcher { get; private set; } = null!;

    /// <summary>
    /// Gets the Android permission service after initialization.
    /// </summary>
    public AndroidPermissionService Permissions { get; private set; } = null!;

    /// <summary>
    /// Gets Android SDK information after initialization.
    /// </summary>
    public AndroidPlatformInfo PlatformInfo { get; private set; } = null!;

    /// <summary>Returns current window facts and capability policies without sensitive payloads.</summary>
    /// <remarks>Requires initialization and the Android main thread. The returned data owns no native resources.</remarks>
    public Windowing.AndroidWindowingDiagnostics GetWindowingDiagnostics()
    {
        if (!IsInitialized) throw new InvalidOperationException("Initialize the Android backend before reading window diagnostics.");
        Windowing.VerifyAccess();
        return new(!Windowing.Exited, Windowing.Generation, Windowing.Host is not null, ActivityTracker.Publisher.State,
            Array.AsReadOnly(Windowing.Windows.Select(w => new Windowing.AndroidWindowDiagnostics(
                ReferenceEquals(Windowing.MainWindow, w), w.IsPopup, w.IsDialog, w.Visible, w.Attached,
                w.Active, w.ClientSize, w.RenderScaling, w.ScaledDensity, w.CurrentInsets,
                w.PaintCount, w.ActivePointers)).ToArray()));
    }

    /// <summary>Returns detached service facts without native objects or user content.</summary>
    /// <remarks>Call on the main thread after initialization. No UI or permission request is started.</remarks>
    public Services.AndroidServiceDiagnostics GetServiceDiagnostics()
    {
        if (!IsInitialized) throw new InvalidOperationException("Initialize the backend before reading service diagnostics.");
        ServiceRequests.VerifyAccess();
        var status = ServiceRequests.Availability;
        var rows = new List<Services.AndroidServiceCapability>();
        foreach (var name in new[] { "Open file", "Save file", "Pick folder" })
        {
            bool available = status != WindowKit.Platform.Services.PlatformServiceStatus.Success || name switch
            {
                "Open file" => Windowing.StorageProvider!.CanOpen,
                "Save file" => Windowing.StorageProvider!.CanSave,
                _ => Windowing.StorageProvider!.CanPickFolder
            };
            rows.Add(new(name, true, true, false, available ? status : WindowKit.Platform.Services.PlatformServiceStatus.NoHandler,
                "SAF user-selected grants; one native UI request; HostLost on destruction."));
        }
        foreach (var name in new[] { "URI launch", "File launch", "Share text", "Share URI/file" })
            rows.Add(new(name, true, true, name is "File launch" or "Share URI/file", status,
                "Input-specific handler and read-grant checks occur at operation start."));
        rows.Add(new("Storage URI", true, false, true, WindowKit.Platform.Services.PlatformServiceStatus.Success, "Existing items remain usable independently of window shutdown."));
        rows.Add(new("Bookmark", true, false, true, WindowKit.Platform.Services.PlatformServiceStatus.Success, "Only persistable grants returned by the provider."));
        rows.Add(new("Local notifications", true, false, true, notifications.Status, "Explicit API33 permission; app-owned stable IDs."));
        rows.Add(new("Framework MessageBoxForm", true, true, false, status, "Framework-rendered modal Form."));
        rows.Add(new("Native message dialog", true, true, false, status, "AlertDialog; standard buttons/adapted icons; HostLost retirement, no replay."));
        foreach (var name in new[] { "Clipboard (#57)", "DragDrop (#57)", "WebView (#20)", "NativeViewHost (#60)", "Camera/microphone features", "Media", "Font dialog", "Print dialog", "Tray" })
            rows.Add(new(name, false, false, false, WindowKit.Platform.Services.PlatformServiceStatus.NotSupported,
                "Tracked separately or unsupported."));
        return new(ServiceRequests.Busy, ServiceRequests.IsShutdown, rows.AsReadOnly());
    }

    /// <summary>Returns a snapshot of Android frame, lifecycle, and reduced-motion integration.</summary>
    public AndroidAnimationRuntimeDiagnostics GetAnimationRuntimeDiagnostics()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException(
                "The Android backend must be initialized before animation diagnostics are available.");
        }

        AndroidAnimationFrameSourceDiagnostics frame = animationFrameSource.GetDiagnostics();
        PlatformAnimationSettingsSnapshot settings = animationSettings.Current;
        return new AndroidAnimationRuntimeDiagnostics(
            ActivityTracker.State,
            frame.CallbackPending,
            frame.SchedulerDemand,
            frame.ActiveSurfaceCount,
            frame.PostedCallbackCount,
            frame.DeliveredCallbackCount,
            settings.DurationScale,
            animationSettings.IsObserverRegistered,
            animationSettings.LastObserverError);
    }

    /// <inheritdoc/>
    public void Initialize()
    {
        if (IsInitialized)
            return;

        lock (sync)
        {
            if (IsInitialized)
                return;

            if (options.PermissionRequestTimeout <= TimeSpan.Zero &&
                options.PermissionRequestTimeout != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.PermissionRequestTimeout),
                    "The permission request timeout must be positive or infinite.");
            }

            ApplicationContext = new AndroidApplicationContext(options.ApplicationContext);
            ActivityTracker = new AndroidActivityTracker(options.ActivityProvider, options.DiagnosticSink);
            Dispatcher = new AndroidMainThreadDispatcher();
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("Android initialization requires the main UI thread.");
            Windowing = new Windowing.AndroidWindowingPlatform(() =>
            {
                if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("Android windows require the main UI thread.");
            });
            animationSettings = new AndroidPlatformAnimationSettings(ApplicationContext.Context);
            animationFrameSource = new AndroidChoreographerAnimationFrameSource(options.DiagnosticSink);
            ServiceRequests = new Services.AndroidActivityResultCoordinator(ActivityTracker, Dispatcher);
            AvaloniaGlobals.AddService<ModernFormsNext.WindowKit.Platform.Services.IPlatformMessageDialogService>(
                new Services.AndroidMessageDialogService(ActivityTracker, Dispatcher, ServiceRequests));
            Windowing.StorageProvider = new Services.AndroidStorageProvider(ApplicationContext.Context, ServiceRequests);
            AvaloniaGlobals.AddService<ModernFormsNext.WindowKit.Platform.Storage.IStorageProvider>(Windowing.StorageProvider);
            notifications = new Services.AndroidNotificationService(ApplicationContext.Context);
            var external = new Services.AndroidExternalServices(ApplicationContext.Context, ActivityTracker, ServiceRequests);
            AvaloniaGlobals.AddService<ModernFormsNext.WindowKit.Platform.Services.IPlatformLauncherService>(external);
            AvaloniaGlobals.AddService<ModernFormsNext.WindowKit.Platform.Services.IPlatformShareService>(external);
            AvaloniaGlobals.AddService<ModernFormsNext.WindowKit.Platform.Services.IPlatformNotificationService>(notifications);
            Windowing.ShuttingDown += () => { ServiceRequests.Shutdown(); notifications.Shutdown(); };
            Permissions = new AndroidPermissionService(
                ApplicationContext.Context,
                ActivityTracker,
                Dispatcher,
                ServiceRequests,
                options.PermissionRequestTimeout,
                options.DiagnosticSink);
            PlatformInfo = new AndroidPlatformInfo();

            ApplicationContext.Application.RegisterActivityLifecycleCallbacks(ActivityTracker);
            IPlatformApplicationLifecycle lifecycle = ActivityTracker.Publisher;
            lifecycle.StateChanged += HandleApplicationLifecycleChanged;
            animationSettings.SetHostActive(lifecycle.State == PlatformApplicationLifecycleState.Foreground);

            AvaloniaGlobals.AddService<IDispatcherImpl>(Dispatcher);
            AvaloniaGlobals.AddService<ModernFormsNext.WindowKit.Platform.IWindowingPlatform>(Windowing);
            ActivityTracker.Publisher.LifecycleChanged += (_, e) =>
            {
                if (e.Current.Phase == PlatformApplicationPhase.Exited) Windowing.Shutdown();
            };
            PlatformServiceRegistry.Register<IPlatformDispatcher>(Dispatcher);
            PlatformServiceRegistry.Register<IPlatformApplicationLifecycle>(lifecycle);
            PlatformServiceRegistry.Register<IPlatformAnimationSettings>(animationSettings);
            PlatformServiceRegistry.Register<IPlatformAnimationFrameSource>(animationFrameSource);
            PlatformServiceRegistry.Register<IPermissionService>(Permissions);
            AvaloniaGlobals.AddService<ModernFormsNext.WindowKit.Platform.IPlatformSettings>(
                new AndroidPlatformSettings(ApplicationContext.Context, ActivityTracker, Dispatcher, options.DiagnosticSink));

            IsInitialized = true;
            AndroidLogger.Write(
                $"Android backend initialized on API {PlatformInfo.SdkVersion}.",
                options.DiagnosticSink);
        }
    }

    private void HandleApplicationLifecycleChanged(
        object? sender,
        PlatformApplicationLifecycleChangedEventArgs e)
        => animationSettings.SetHostActive(e.CurrentState == PlatformApplicationLifecycleState.Foreground);
}
