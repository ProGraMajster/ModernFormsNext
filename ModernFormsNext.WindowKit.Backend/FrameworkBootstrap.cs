using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace ModernFormsNext.WindowKit.Backend;

/// <summary>
/// Provides one-time initialization for the WindowKit backend layer.
/// </summary>
/// <remarks>
/// <para>
/// This class is responsible for discovering and loading platform-specific backend assemblies
/// located next to the application binaries, finding an implementation of
/// <see cref="IPlatformBootstrap"/>, and invoking it for the current runtime platform.
/// </para>
/// <para>
/// The initialization process is thread-safe and guaranteed to run only once for the current
/// application domain.
/// </para>
/// <para>
/// Backend assemblies are discovered using the file pattern
/// <c>ModernFormsNext.WindowKit.Backend.*.dll</c>. The base backend assembly itself is excluded
/// from this scan.
/// </para>
/// </remarks>
/// <example>
/// The following example shows a typical startup call:
/// <code>
/// FrameworkBootstrap.EnsureInitialized();
/// </code>
/// This should usually be called during application startup before using any platform-dependent
/// WindowKit services.
/// </example>
public static class FrameworkBootstrap
{
    private static bool initialized;
    private static readonly object sync = new();

    /// <summary>
    /// Ensures that the framework backend is initialized for the current platform.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If initialization has already completed, this method returns immediately.
    /// </para>
    /// <para>
    /// During initialization, the method attempts to load backend assemblies, discover a valid
    /// <see cref="IPlatformBootstrap"/> implementation, and invoke its <see cref="IPlatformBootstrap.Initialize"/>
    /// method.
    /// </para>
    /// <para>
    /// If no compatible backend is found for the current operating system, a
    /// <see cref="PlatformNotSupportedException"/> is thrown.
    /// </para>
    /// </remarks>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when no backend capable of initializing the current platform can be found.
    /// </exception>
    public static void EnsureInitialized()
    {
        if (initialized)
            return;

        lock (sync)
        {
            if (initialized)
                return;

            LoadBackendAssemblies();

            var bootstrapper = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(t =>
                    typeof(IPlatformBootstrap).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    !t.IsInterface)
                .Select(Create)
                .FirstOrDefault(x => x != null && x.CanInitializeCurrentPlatform());

            if (bootstrapper == null)
                throw new PlatformNotSupportedException(
                    $"No backend available for this platform ({GetCurrentPlatformName()}).");

            bootstrapper.Initialize();
            initialized = true;
        }
    }

    /// <summary>
    /// Loads backend assemblies from the application base directory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Assemblies are loaded only if they match the backend naming convention and are not already
    /// loaded into the current application domain.
    /// </para>
    /// <para>
    /// Load failures are intentionally ignored so that invalid or unsupported backend assemblies
    /// do not prevent discovery of other valid backends.
    /// </para>
    /// </remarks>
    private static void LoadBackendAssemblies()
    {
        var baseDirectory = AppContext.BaseDirectory;

        var loadedAssemblyPaths = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => Path.GetFullPath(a.Location))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var backendAssemblyFiles = Directory
            .EnumerateFiles(baseDirectory, "ModernFormsNext.WindowKit.Backend.*.dll", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith("ModernFormsNext.WindowKit.Backend.dll", StringComparison.OrdinalIgnoreCase))
            .Where(path => !loadedAssemblyPaths.Contains(Path.GetFullPath(path)));

        foreach (var assemblyFile in backendAssemblyFiles)
        {
            try
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyFile));
            }
            catch
            {
                // Ignore load failures here; unsupported or broken backends
                // should not block discovery of other valid backends.
            }
        }
    }

    /// <summary>
    /// Creates an instance of the specified platform bootstrap type.
    /// </summary>
    /// <param name="t">The type to instantiate.</param>
    /// <returns>
    /// An <see cref="IPlatformBootstrap"/> instance if creation succeeds; otherwise, <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// Exceptions during activation are suppressed so that one invalid type does not break
    /// bootstrap discovery.
    /// </remarks>
    private static IPlatformBootstrap? Create(Type t)
    {
        try
        {
            return Activator.CreateInstance(t) as IPlatformBootstrap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely returns all loadable types from the specified assembly.
    /// </summary>
    /// <param name="a">The assembly to inspect.</param>
    /// <returns>
    /// An array of all successfully loaded types from the assembly.
    /// </returns>
    /// <remarks>
    /// If the assembly cannot fully load all of its types, the successfully loaded subset is
    /// returned instead.
    /// </remarks>
    private static Type[] SafeGetTypes(Assembly a)
    {
        try
        {
            return a.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(x => x != null).ToArray()!;
        }
    }

    /// <summary>
    /// Gets a human-readable name for the current operating system platform.
    /// </summary>
    /// <returns>
    /// A platform name such as <c>Windows</c>, <c>Android</c>, <c>Linux</c>, <c>macOS</c>,
    /// or <c>Unknown</c>.
    /// </returns>
    private static string GetCurrentPlatformName()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsAndroid()) return "Android";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
    }
}