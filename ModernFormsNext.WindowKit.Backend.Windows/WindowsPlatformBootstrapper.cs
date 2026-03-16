using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Windows;

public sealed class WindowsPlatformBootstrapper : IPlatformBootstrap
{
    public bool CanInitializeCurrentPlatform()
        => OperatingSystem.IsWindows();

    public void Initialize()
    {
        WindowsPlatformBootstrap.Initialize();
    }   
}