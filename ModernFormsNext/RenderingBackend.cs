namespace ModernFormsNext;

/// <summary>Specifies the application-wide rendering selection policy.</summary>
public enum RenderingBackend
{
    /// <summary>Selects a supported renderer automatically. Currently selects Software.</summary>
    /// <remarks>Applications must not assume Auto will always select the same backend in future versions.</remarks>
    Auto,
    /// <summary>Forces CPU raster rendering, useful for diagnostics, CI and visual comparisons.</summary>
    Software
}
