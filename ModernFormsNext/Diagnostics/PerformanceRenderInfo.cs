namespace ModernFormsNext.Diagnostics;

/// <summary>Contains copied rendering facts; unavailable native measurements remain nullable.</summary>
/// <remarks>No property retains a surface, native handle, window or control. Dimensions are explicitly logical or device pixels.</remarks>
public readonly record struct PerformanceRenderInfo
{
    /// <summary>Creates information for an unknown shared rendering path.</summary>
    public PerformanceRenderInfo() { }
    /// <summary>Gets the backend label supplied by the host.</summary>
    public string Backend { get; init; } = "Unknown";
    /// <summary>Gets the actual renderer label, rather than an inferred compositor capability.</summary>
    public string Renderer { get; init; } = "Skia";
    /// <summary>Gets the framework acceleration capability.</summary>
    public PerformanceAcceleration Acceleration { get; init; }
    /// <summary>Gets the measured callback boundary.</summary>
    public PerformanceFrameBoundary Boundary { get; init; }
    /// <summary>Gets the logical-to-device scale when known; zero means unavailable.</summary>
    public double Scale { get; init; }
    /// <summary>Gets the logical viewport width; zero means unavailable or empty.</summary>
    public int LogicalWidth { get; init; }
    /// <summary>Gets the logical viewport height; zero means unavailable or empty.</summary>
    public int LogicalHeight { get; init; }
    /// <summary>Gets the backing width in device pixels, if reported.</summary>
    public int? PixelWidth { get; init; }
    /// <summary>Gets the backing height in device pixels, if reported.</summary>
    public int? PixelHeight { get; init; }
    /// <summary>Gets the reported pixel format.</summary>
    public string? PixelFormat { get; init; }
    /// <summary>Gets the backing stride in bytes, if reported.</summary>
    public int? RowBytes { get; init; }
    /// <summary>Gets known backing bytes; this is not total native or GPU memory.</summary>
    public long? BackingBytes { get; init; }
    /// <summary>Gets the actual native host attachment generation, if reported.</summary>
    public long? HostGeneration { get; init; }
    /// <summary>Gets the actual backing allocation generation, if reported.</summary>
    public long? BackingGeneration { get; init; }
    /// <summary>Gets whether this frame is explicitly offscreen.</summary>
    public bool IsOffscreen { get; init; }
    /// <summary>Gets actual repaint coverage independently from dirty requests.</summary>
    public PerformanceRedraw Redraw { get; init; }
    /// <summary>Gets GPU duration when a real provider supplies it. Current software paths report null.</summary>
    public TimeSpan? GpuDuration { get; init; }
    /// <summary>Gets a real presentation timestamp when supplied. Current backends report null.</summary>
    public TimeSpan? PresentationTimestamp { get; init; }
    /// <summary>Gets CPU wall time spent submitting framebuffer pixels to the native device, when measured.</summary>
    /// <remarks>
    /// The Windows software backend measures its synchronous GDI transfer while profiling is enabled.
    /// This is part of the native frame duration, not GPU time or completion of desktop composition.
    /// Other backends report null. The copied value can be read from detached snapshots on any thread.
    /// </remarks>
    public TimeSpan? PresentationCpuTime { get; init; }
    /// <summary>Gets a provider's GPU context reset count, if supported.</summary>
    public long? GpuContextResetCount { get; init; }
}
