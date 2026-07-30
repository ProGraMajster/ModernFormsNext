using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Backend.Windows.Win32Com;
using ModernFormsNext.WindowKit.Controls.Platform;
using ModernFormsNext.WindowKit.Input.Platform;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Threading;

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
        => WindowKitBackendRegistry.Register(new WindowsWindowKitBackend());

    internal static void InitializeServices()
    {
        if (initialized)
            return;

        lock (sync)
        {
            if (initialized)
                return;

            Win32Platform.Initialize();
            Win32ComRegistration.Initialize();

            AvaloniaGlobals.AddService<IWindowingPlatform>(Win32Platform.Instance);
            AvaloniaGlobals.AddService<IDispatcherImpl>(Win32Platform.Instance._dispatcher);
            AvaloniaGlobals.AddService<ICursorFactory>(CursorFactory.Instance);
            AvaloniaGlobals.AddService<IClipboard>(new ClipboardImpl());
            AvaloniaGlobals.AddService<IPlatformAccessibilityService>(new WindowsAccessibilityService());
            AvaloniaGlobals.AddService<IPlatformFontDialogService>(new WindowsFontDialogService());
            AvaloniaGlobals.AddService<IPlatformPrintDialogService>(new WindowsPrintDialogService());
            AvaloniaGlobals.AddService<IPlatformTrayManager>(new WindowsTrayManager());
            var animationSettings = new WindowsPlatformAnimationSettings();
            Win32Platform.Instance.AnimationSettings = animationSettings;
            PlatformServiceRegistry.Register<IPlatformAnimationSettings>(animationSettings);
            PlatformServiceRegistry.Register<IPlatformThemeSettings>(new WindowsPlatformThemeSettings(animationSettings));

            initialized = true;
        }
    }
}
