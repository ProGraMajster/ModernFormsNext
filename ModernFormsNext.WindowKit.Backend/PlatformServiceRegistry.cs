namespace ModernFormsNext.WindowKit.Backend;

/// <summary>
/// Stores lightweight platform services that can be shared without loading the full UI framework.
/// </summary>
/// <remarks>
/// This registry is intended for backend-foundation services such as permissions and native UI
/// thread dispatch. Full WindowKit implementations continue to use their established service graph.
/// A service can be registered only once so initialization cannot replace a live implementation.
/// </remarks>
public static class PlatformServiceRegistry
{
    private static readonly object Sync = new();
    private static readonly Dictionary<Type, object> Services = new();

    /// <summary>
    /// Registers one implementation for a platform-neutral service contract.
    /// </summary>
    /// <typeparam name="TService">The service contract type.</typeparam>
    /// <param name="service">The implementation to register.</param>
    /// <returns>The registered implementation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the contract is already registered.</exception>
    public static TService Register<TService>(TService service)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(service);

        lock (Sync)
        {
            if (!Services.TryAdd(typeof(TService), service))
            {
                throw new InvalidOperationException(
                    $"Platform service '{typeof(TService).FullName}' is already registered.");
            }

            return service;
        }
    }

    /// <summary>
    /// Gets an optional platform service implementation.
    /// </summary>
    /// <typeparam name="TService">The service contract type.</typeparam>
    /// <returns>The implementation, or <see langword="null"/> when none is registered.</returns>
    public static TService? GetService<TService>()
        where TService : class
    {
        lock (Sync)
            return Services.TryGetValue(typeof(TService), out var service) ? (TService)service : null;
    }

    /// <summary>
    /// Gets a required platform service implementation.
    /// </summary>
    /// <typeparam name="TService">The service contract type.</typeparam>
    /// <returns>The registered implementation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no implementation is registered.</exception>
    public static TService GetRequiredService<TService>()
        where TService : class
        => GetService<TService>() ?? throw new InvalidOperationException(
            $"Platform service '{typeof(TService).FullName}' is not registered.");
}
