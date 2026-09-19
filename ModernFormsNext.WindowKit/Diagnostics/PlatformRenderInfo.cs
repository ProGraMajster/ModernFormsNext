namespace ModernFormsNext.WindowKit.Diagnostics;

/// <summary>Identifies the measured callback boundary, without promising presentation completion.</summary>
internal enum PlatformRenderBoundary { WindowPaint, AndroidViewDraw, OffscreenCapture, SharedRender }

/// <summary>Identifies the framework host rather than an operating-system compositor.</summary>
internal enum PlatformRenderBackend { Unknown, Windows, Android, Headless }

/// <summary>Describes the known framework raster path; unknown never implies hardware acceleration.</summary>
internal enum PlatformRenderMode { Unknown, Software, Hardware }

/// <summary>Contains content-free facts captured at a render boundary on its owning UI thread.</summary>
/// <remarks>
/// Dimensions distinguish logical units from actual backing pixels. Missing values are unavailable,
/// not zero. Generations identify a host attachment and software backing independently; neither is
/// a GPU context-reset count. This value must never contain a canvas, native handle or control.
/// </remarks>
internal readonly record struct PlatformRenderInfo(
    PlatformRenderBoundary Boundary,
    PlatformRenderBackend Backend = PlatformRenderBackend.Unknown,
    PlatformRenderMode RenderMode = PlatformRenderMode.Unknown,
    double? LogicalWidth = null,
    double? LogicalHeight = null,
    int? PixelWidth = null,
    int? PixelHeight = null,
    int? RowBytes = null,
    string? Format = null,
    double? Scale = null,
    long? BackingBytes = null,
    long? HostGeneration = null,
    long? BackingGeneration = null,
    bool? FullRedraw = null,
    TimeSpan? PresentationCpuTime = null);
