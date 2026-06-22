using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Windows
{
    /// <summary>
    /// Creates Windows notification area icons for the active process.
    /// </summary>
    internal sealed class WindowsTrayManager : IPlatformTrayManager
    {
        /// <inheritdoc/>
        public IPlatformTrayIcon CreateTrayIcon () => new WindowsTrayIcon ();
    }
}
