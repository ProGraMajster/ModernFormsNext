namespace ModernFormsNext.WindowKit.Backend;

/// <summary>
/// Defines the lifecycle shared by platform-specific WindowKit backends.
/// </summary>
/// <remarks>
/// Implementations live in platform backend assemblies. The contract deliberately contains no
/// operating-system types so hosts can identify the active backend without depending on its
/// native API surface.
/// </remarks>
public interface IWindowKitBackend
{
    /// <summary>
    /// Gets the stable, human-readable platform name exposed by this backend.
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Gets a value indicating whether backend initialization completed successfully.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes the backend and registers its platform services.
    /// </summary>
    /// <remarks>
    /// Implementations must be idempotent for the same backend instance. Initialization is a
    /// UI-host operation and should normally be performed on the platform UI thread.
    /// </remarks>
    void Initialize();
}
