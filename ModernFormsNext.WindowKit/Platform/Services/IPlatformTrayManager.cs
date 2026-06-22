using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform.Services
{
    /// <summary>
    /// Creates platform tray icon instances for the active backend.
    /// </summary>
    /// <remarks>
    /// This service is registered by a platform backend when the operating system supports
    /// tray icons. If no implementation is registered, the public framework component
    /// should report that tray icons are not supported on the current platform.
    /// </remarks>
    [Unstable, PrivateApi]
    public interface IPlatformTrayManager
    {
        /// <summary>
        /// Creates a new backend-owned tray icon.
        /// </summary>
        /// <returns>A platform tray icon instance.</returns>
        IPlatformTrayIcon CreateTrayIcon ();
    }
}
