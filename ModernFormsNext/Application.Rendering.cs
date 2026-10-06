using ModernFormsNext.Rendering;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext;

public static partial class Application
{
    private static readonly object renderingSync = new();
    private static RenderingBackend requestedRenderingBackend;
    private static IRenderingBackend? renderingBackend;

    /// <summary>Gets the copied application-wide rendering policy. The default is Auto.</summary>
    /// <remarks>This read is thread-safe and does not initialize a renderer.</remarks>
    public static RenderingBackend RequestedRenderingBackend
    {
        get { lock (renderingSync) return requestedRenderingBackend; }
    }

    /// <summary>Gets the resolved renderer, or null before renderer initialization. Never returns Auto.</summary>
    /// <remarks>This thread-safe diagnostic read does not create a window or initialize rendering.</remarks>
    public static RenderingBackend? ActiveRenderingBackend
    {
        get { lock (renderingSync) return renderingBackend?.ActiveBackend; }
    }

    /// <summary>Copies application-wide rendering options before backend initialization.</summary>
    /// <param name="options">The options to copy. Auto is the default and currently selects Software.</param>
    /// <remarks>
    /// Call on the startup thread before constructing a Form, calling Run, or initializing a
    /// Windows/Android platform backend. The choice freezes at renderer initialization or platform
    /// backend initialization, whichever occurs first. Later option mutations have no effect.
    /// Closing all windows does not unfreeze the choice. No render thread or GPU probing is started.
    /// The supported TestHost scopes and restores this state together with its application runtime.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Options is null.</exception>
    /// <exception cref="InvalidOperationException">A renderer or platform backend is already initialized.</exception>
    /// <example><code>
    /// Application.ConfigureRendering(new RenderingOptions { Backend = RenderingBackend.Software });
    /// Application.Run(new MainForm());
    /// </code></example>
    public static void ConfigureRendering(RenderingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (renderingSync) {
            // Public backend.Initialize can install windowing services without registering
            // Current. Honor that existing service boundary too; TestHost scopes both paths.
            if (renderingBackend is not null ||
                (!TestWindowFactoryScope.HasActiveFactory &&
                    (WindowKitBackendRegistry.Current?.IsInitialized == true ||
                     AvaloniaGlobals.GetService<IWindowingPlatform>() is not null)))
                throw new InvalidOperationException("Rendering must be configured before backend initialization or the first window.");
            requestedRenderingBackend = options.Backend;
        }
    }

    internal static IRenderingBackend GetRenderingBackend()
    {
        lock (renderingSync)
            // Auto is a policy, never an active backend. No GPU backend is attempted and no
            // fallback is reported. Future capability selection belongs at this boundary.
            return renderingBackend ??= new SoftwareRenderingBackend(requestedRenderingBackend);
    }
}
