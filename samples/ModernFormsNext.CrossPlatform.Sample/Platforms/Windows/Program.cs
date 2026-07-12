using ModernFormsNext.WindowKit.Backend.Windows;

namespace ModernFormsNext.CrossPlatform.Sample;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Make backend selection explicit in this cross-platform sample. Application.Run remains
        // responsible for the normal message loop and safely observes the existing registration.
        WindowsPlatformBootstrap.Initialize();
        var app = new App(new WindowsPlatformServices());
        Application.Run(new WindowsAppHost(app));
    }
}
