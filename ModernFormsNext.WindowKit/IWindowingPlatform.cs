using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform
    {
    /// <summary>
    /// Creates platform window implementations for the active backend.
    /// </summary>
    [Unstable, PrivateApi]
    public interface IWindowingPlatform
    {
        /// <summary>
        /// Creates a new top-level window implementation.
        /// </summary>
        /// <returns>The created platform window implementation.</returns>
        IWindowImpl CreateWindow();

        //IWindowImpl CreateEmbeddableWindow();

        //ITrayIconImpl? CreateTrayIcon();
    }
}
