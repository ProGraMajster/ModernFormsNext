using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform
    {
    [Unstable, PrivateApi]
    public interface IWindowingPlatform
    {
        IWindowImpl CreateWindow();

        //IWindowImpl CreateEmbeddableWindow();

        //ITrayIconImpl? CreateTrayIcon();
    }
}
