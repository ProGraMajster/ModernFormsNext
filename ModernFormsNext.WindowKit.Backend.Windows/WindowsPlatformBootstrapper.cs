using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Windows;

/// <summary>
/// Discovers and initializes the Windows WindowKit backend when the current process is running on Windows.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FrameworkBootstrap"/> creates instances of <see cref="IPlatformBootstrap"/> implementations
/// from backend assemblies and calls <see cref="CanInitializeCurrentPlatform"/> before initialization.
/// This bootstrapper keeps the Windows-specific registration path isolated in the Windows backend assembly.
/// </para>
/// </remarks>
public sealed class WindowsPlatformBootstrapper : IPlatformBootstrap
{
    /// <inheritdoc/>
    public bool CanInitializeCurrentPlatform()
        => OperatingSystem.IsWindows();

    /// <inheritdoc/>
    public void Initialize()
    {
        WindowsPlatformBootstrap.Initialize();
    }   
}
