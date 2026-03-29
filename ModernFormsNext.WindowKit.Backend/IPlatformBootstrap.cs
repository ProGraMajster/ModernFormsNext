namespace ModernFormsNext.WindowKit.Backend;

/// <summary>
/// Defines the contract for platform-specific backend initialization.
/// </summary>
/// <remarks>
/// <para>
/// Implementations of this interface are discovered dynamically by <see cref="FrameworkBootstrap"/>
/// after backend assemblies are loaded.
/// </para>
/// <para>
/// A platform bootstrapper should determine whether it supports the current runtime environment
/// and, if so, register or initialize all required platform services.
/// </para>
/// </remarks>
/// <example>
/// Example implementation:
/// <code>
/// public sealed class WindowsPlatformBootstrap : IPlatformBootstrap
/// {
///     public bool CanInitializeCurrentPlatform()
///     {
///         return OperatingSystem.IsWindows();
///     }
///
///     public void Initialize()
///     {
///         // Register Windows-specific services here.
///     }
/// }
/// </code>
/// </example>
public interface IPlatformBootstrap
{
    /// <summary>
    /// Determines whether the current bootstrapper can initialize the current platform.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the bootstrapper supports the current platform; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    bool CanInitializeCurrentPlatform();

    /// <summary>
    /// Performs platform-specific backend initialization.
    /// </summary>
    /// <remarks>
    /// This method is called only after <see cref="CanInitializeCurrentPlatform"/> returns
    /// <see langword="true"/>.
    /// </remarks>
    void Initialize();
}