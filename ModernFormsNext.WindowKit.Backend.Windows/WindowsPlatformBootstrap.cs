using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Backend.Windows.Win32Com;
using ModernFormsNext.WindowKit.Controls.Platform;
using ModernFormsNext.WindowKit.Input.Platform;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Threading;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.WindowKit.Backend.Windows;

/// <summary>
/// Registers Windows backend services in the shared service container.
/// </summary>
public static class WindowsPlatformBootstrap
{
    private static bool initialized;
    private static readonly object sync = new();

    /// <summary>
    /// Initializes Windows backend services once.
    /// </summary>
    public static void Initialize()
    {
        StartupDiagnostics.Log.Stage("WINDOWS_PLATFORM_INITIALIZE_BEGIN");
        WindowKitBackendRegistry.Register(new WindowsWindowKitBackend());
        StartupDiagnostics.Log.Stage("WINDOWS_PLATFORM_INITIALIZE_OK");
    }

    internal static void InitializeServices()
    {
        if (initialized)
            return;

        StartupDiagnostics.Log.Stage("WINDOWS_SERVICES_LOCK_BEGIN");
        lock (sync)
        {
            StartupDiagnostics.Log.Stage("WINDOWS_SERVICES_LOCK_OK");
            if (initialized)
                return;

            // The platform constructor creates a native message window immediately. Register the
            // settings service first because Windows can synchronously deliver WM_SETTINGCHANGE to
            // that WndProc before the remainder of backend initialization has completed.
            StartupDiagnostics.Log.Stage("WINDOWS_SETTINGS_BEGIN");
            AvaloniaGlobals.AddService<IPlatformSettings>(new Win32PlatformSettings());
            StartupDiagnostics.Log.Stage("WINDOWS_SETTINGS_OK");
            StartupDiagnostics.Log.Stage("WIN32_PLATFORM_INITIALIZE_BEGIN");
            Win32Platform.Initialize();
            StartupDiagnostics.Log.Stage("WIN32_PLATFORM_INITIALIZE_OK");
            StartupDiagnostics.Log.Stage("WIN32_COM_INITIALIZE_BEGIN");
            Win32ComRegistration.Initialize();
            StartupDiagnostics.Log.Stage("WIN32_COM_INITIALIZE_OK");
            StartupDiagnostics.Log.Stage("WINDOWS_SERVICE_REGISTRATION_BEGIN");

            int lifecycleThread = Environment.CurrentManagedThreadId;
            var lifecycle = new WindowsApplicationLifecycle(
                () =>
                {
                    if (Environment.CurrentManagedThreadId != lifecycleThread)
                        throw new InvalidOperationException("Windows application lifecycle requires its UI thread.");
                },
                action =>
                {
                    if (Environment.CurrentManagedThreadId == lifecycleThread) action();
                    else Dispatcher.UIThread.Post(action);
                });
            Win32Platform.Instance.ApplicationLifecycle = lifecycle;
            PlatformServiceRegistry.Register<IPlatformApplicationLifecycle>(lifecycle.Publisher);

            AvaloniaGlobals.AddService<IWindowingPlatform>(Win32Platform.Instance);
            AvaloniaGlobals.AddService<IDispatcherImpl>(Win32Platform.Instance._dispatcher);
            AvaloniaGlobals.AddService<ICursorFactory>(CursorFactory.Instance);
            AvaloniaGlobals.AddService<IClipboard>(new ClipboardImpl());
            AvaloniaGlobals.AddService<IPlatformAccessibilityService>(new WindowsAccessibilityService());
            AvaloniaGlobals.AddService<IPlatformFontDialogService>(new WindowsFontDialogService());
            AvaloniaGlobals.AddService<IPlatformPrintDialogService>(new WindowsPrintDialogService());
            AvaloniaGlobals.AddService<IPlatformTrayManager>(new WindowsTrayManager());
            AvaloniaGlobals.AddService<IPlatformLauncherService>(new WindowsLauncherService());
            AvaloniaGlobals.AddService<IPlatformMessageDialogService>(new WindowsMessageDialogService(lifecycle.Publisher));
            var unavailable = new UnsupportedPlatformServices();
            AvaloniaGlobals.AddService<IPlatformShareService>(unavailable);
            AvaloniaGlobals.AddService<IPlatformNotificationService>(unavailable);
            var animationSettings = new WindowsPlatformAnimationSettings();
            Win32Platform.Instance.AnimationSettings = animationSettings;
            PlatformServiceRegistry.Register<IPlatformAnimationSettings>(animationSettings);
            PlatformServiceRegistry.Register<IPlatformThemeSettings>(new WindowsPlatformThemeSettings(animationSettings));

            initialized = true;
            StartupDiagnostics.Log.Stage("WINDOWS_SERVICE_REGISTRATION_OK");
        }
    }
}
