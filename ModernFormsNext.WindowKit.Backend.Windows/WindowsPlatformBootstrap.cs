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
        System.Diagnostics.Trace.WriteLine("WINDOWS_PLATFORM_INITIALIZE_BEGIN", "ModernFormsNext.Startup");
        WindowKitBackendRegistry.Register(new WindowsWindowKitBackend());
        System.Diagnostics.Trace.WriteLine("WINDOWS_PLATFORM_INITIALIZE_OK", "ModernFormsNext.Startup");
    }

    internal static void InitializeServices()
    {
        if (initialized)
            return;

        System.Diagnostics.Trace.WriteLine("WINDOWS_SERVICES_LOCK_BEGIN", "ModernFormsNext.Startup");
        lock (sync)
        {
            System.Diagnostics.Trace.WriteLine("WINDOWS_SERVICES_LOCK_OK", "ModernFormsNext.Startup");
            if (initialized)
                return;

            // The platform constructor creates a native message window immediately. Register the
            // settings service first because Windows can synchronously deliver WM_SETTINGCHANGE to
            // that WndProc before the remainder of backend initialization has completed.
            System.Diagnostics.Trace.WriteLine("WINDOWS_SETTINGS_BEGIN", "ModernFormsNext.Startup");
            AvaloniaGlobals.AddService<IPlatformSettings>(new Win32PlatformSettings());
            System.Diagnostics.Trace.WriteLine("WINDOWS_SETTINGS_OK", "ModernFormsNext.Startup");
            System.Diagnostics.Trace.WriteLine("WIN32_PLATFORM_INITIALIZE_BEGIN", "ModernFormsNext.Startup");
            Win32Platform.Initialize();
            System.Diagnostics.Trace.WriteLine("WIN32_PLATFORM_INITIALIZE_OK", "ModernFormsNext.Startup");
            System.Diagnostics.Trace.WriteLine("WIN32_COM_INITIALIZE_BEGIN", "ModernFormsNext.Startup");
            Win32ComRegistration.Initialize();
            System.Diagnostics.Trace.WriteLine("WIN32_COM_INITIALIZE_OK", "ModernFormsNext.Startup");
            System.Diagnostics.Trace.WriteLine("WINDOWS_SERVICE_REGISTRATION_BEGIN", "ModernFormsNext.Startup");

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
            System.Diagnostics.Trace.WriteLine("WINDOWS_SERVICE_REGISTRATION_OK", "ModernFormsNext.Startup");
        }
    }
}
