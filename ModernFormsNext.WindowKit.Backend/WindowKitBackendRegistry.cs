namespace ModernFormsNext.WindowKit.Backend;

/// <summary>
/// Coordinates process-wide activation of the WindowKit platform backend.
/// </summary>
/// <remarks>
/// A process can have only one active platform backend. This prevents a second backend from
/// replacing dispatcher or windowing services after framework objects have started using them.
/// </remarks>
public static class WindowKitBackendRegistry
{
    private static readonly object Sync = new();
    private static IWindowKitBackend? current;

    /// <summary>
    /// Gets the active backend, or <see langword="null"/> before platform initialization.
    /// </summary>
    public static IWindowKitBackend? Current
    {
        get
        {
            lock (Sync)
                return current;
        }
    }

    /// <summary>
    /// Initializes and registers a platform backend.
    /// </summary>
    /// <typeparam name="TBackend">The concrete backend type.</typeparam>
    /// <param name="backend">The backend instance to activate.</param>
    /// <returns>The active backend instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="backend"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a different backend has already been registered.
    /// </exception>
    public static TBackend Register<TBackend>(TBackend backend)
        where TBackend : class, IWindowKitBackend
    {
        ArgumentNullException.ThrowIfNull(backend);

        lock (Sync)
        {
            if (current is TBackend existing)
                return existing;

            if (current is not null)
            {
                throw new InvalidOperationException(
                    $"WindowKit backend '{current.PlatformName}' is already active; " +
                    $"'{backend.PlatformName}' cannot be registered in the same process.");
            }

            // Publish only after successful initialization so callers never observe a partially
            // configured service graph.
            backend.Initialize();
            current = backend;
            return backend;
        }
    }
}
