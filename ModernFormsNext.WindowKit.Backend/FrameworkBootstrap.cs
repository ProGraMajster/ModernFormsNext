using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace ModernFormsNext.WindowKit.Backend;

public static class FrameworkBootstrap
{
    private static bool initialized;
    private static readonly object sync = new();

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

    private static string GetCurrentPlatformName()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsAndroid()) return "Android";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
    }
}