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
/// infrastructure. The experimental shared-control host renders one control tree through Skia;
/// Android still does not implement the complete ModernFormsNext window contract, clipboard,
/// camera, media, WebView, notifications, file pickers, sharing, or drag-and-drop services.
/// </remarks>
public sealed class AndroidWindowKitBackend : IWindowKitBackend
{
    private readonly object sync = new();
    private readonly AndroidWindowKitOptions options;
    private AndroidPlatformAnimationSettings animationSettings = null!;
    private AndroidChoreographerAnimationFrameSource animationFrameSource = null!;

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
            animationSettings = new AndroidPlatformAnimationSettings(ApplicationContext.Context);
            animationFrameSource = new AndroidChoreographerAnimationFrameSource(options.DiagnosticSink);
            Permissions = new AndroidPermissionService(
                ApplicationContext.Context,
                ActivityTracker,
                Dispatcher,
                options.PermissionRequestTimeout,
                options.DiagnosticSink);
            PlatformInfo = new AndroidPlatformInfo();

            ApplicationContext.Application.RegisterActivityLifecycleCallbacks(ActivityTracker);
            IPlatformApplicationLifecycle lifecycle = ActivityTracker;
            lifecycle.StateChanged += HandleApplicationLifecycleChanged;
            animationSettings.SetHostActive(lifecycle.State == PlatformApplicationLifecycleState.Foreground);

            // Register only services that are genuinely implemented. In particular, there is no
            // IWindowingPlatform or clipboard registration until Android UI/rendering support exists.
            PlatformServiceRegistry.Register<IPlatformDispatcher>(Dispatcher);
            PlatformServiceRegistry.Register<IPlatformApplicationLifecycle>(ActivityTracker);
            PlatformServiceRegistry.Register<IPlatformAnimationSettings>(animationSettings);
            PlatformServiceRegistry.Register<IPlatformAnimationFrameSource>(animationFrameSource);
            PlatformServiceRegistry.Register<IPermissionService>(Permissions);

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
