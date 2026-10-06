namespace ModernFormsNext;

/// <summary>Configures rendering before platform initialization or construction of the first window.</summary>
/// <remarks>
/// Pass these options to <see cref="Application.ConfigureRendering"/> on the startup thread.
/// That method copies the values; later mutations do not affect existing or future windows.
/// Rendering selection is application-wide, not per window or per control.
/// </remarks>
public sealed class RenderingOptions
{
    private RenderingBackend backend;

    /// <summary>Initializes options with automatic renderer selection.</summary>
    public RenderingOptions() { }

    /// <summary>Gets or sets the requested policy. Auto currently resolves directly to Software.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined rendering backend.</exception>
    public RenderingBackend Backend
    {
        get => backend;
        set {
            if (value is not RenderingBackend.Auto and not RenderingBackend.Software)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported rendering backend.");
            backend = value;
        }
    }
}
