namespace ModernFormsNext.WindowKit.Backend;

public interface IPlatformBootstrap
{
    bool CanInitializeCurrentPlatform();

    void Initialize();
}