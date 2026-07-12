using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Windows;

/// <summary>
/// Adapts Windows service initialization to the common WindowKit backend lifecycle.
/// </summary>
public sealed class WindowsWindowKitBackend : IWindowKitBackend
{
    /// <inheritdoc/>
    public string PlatformName => "Windows";

    /// <inheritdoc/>
    public bool IsInitialized { get; private set; }

    /// <inheritdoc/>
    public void Initialize()
    {
        if (IsInitialized)
            return;

        WindowsPlatformBootstrap.InitializeServices();
        IsInitialized = true;
    }
}
