namespace ModernFormsNext.Diagnostics;

// A synchronous borrowed view over the recorder's preallocated source-filtered scratch data.
// It cannot escape to a field, callback or async continuation. Capture() remains the explicit
// detached public snapshot API; drawing a HUD must not clone its controls and frame history.
internal readonly ref struct PerformanceOverlayData
{
    internal PerformanceFrameMetrics? LatestFrame { get; init; }
    internal ReadOnlySpan<PerformanceFrameMetrics> Frames { get; init; }
    internal ReadOnlySpan<PerformanceRegion> Regions { get; init; }
    internal PerformanceWorkMetrics UnframedWork { get; init; }
}
