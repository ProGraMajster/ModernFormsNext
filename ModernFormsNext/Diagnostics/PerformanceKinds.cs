namespace ModernFormsNext.Diagnostics;

/// <summary>Identifies measured framework work; category durations can overlap and must not be summed as frame time.</summary>
public enum PerformanceActivityKind
{
    /// <summary>An executed control layout pass, including its normal callbacks.</summary>
    Layout,
    /// <summary>A preferred-size query, including nested measurement where applicable.</summary>
    PreferredSize,
    /// <summary>Shared framework content rendering.</summary>
    Render,
    /// <summary>An outer framework input route.</summary>
    Input,
    /// <summary>Processing by the existing animation scheduler.</summary>
    Animation,
    /// <summary>Creating a framework-owned Skia shader.</summary>
    ShaderCreation,
    /// <summary>Optional diagnostic adornment rendering.</summary>
    Overlay,
    /// <summary>Layout of the existing safe Designer preview.</summary>
    DesignerLayout,
    /// <summary>Rendering of the existing safe Designer preview.</summary>
    DesignerRender,
    /// <summary>Explicitly instrumented application or extension work.</summary>
    Custom
}

/// <summary>Identifies observed work at framework boundaries, rather than estimated native draw calls.</summary>
public enum PerformanceCounterKind
{
    /// <summary>Layout passes actually executed after suspension checks.</summary>
    LayoutPasses,
    /// <summary>Preferred-size queries.</summary>
    PreferredSizeQueries,
    /// <summary>Preferred-size queries served from the existing cache.</summary>
    PreferredSizeCacheHits,
    /// <summary>Calls to the preferred-size core implementation.</summary>
    PreferredSizeCoreCalls,
    /// <summary>Controls visited by the instrumented paint traversal.</summary>
    ControlsVisited,
    /// <summary>Control backbuffers repainted.</summary>
    ControlsRepainted,
    /// <summary>Control backbuffers composited, including cached buffers.</summary>
    ControlsComposited,
    /// <summary>Composites that reuse a clean control backbuffer.</summary>
    ControlCacheHits,
    /// <summary>Invisible controls excluded from the paint traversal.</summary>
    InvisibleControlsSkipped,
    /// <summary>Controls skipped because their size is not positive.</summary>
    ZeroSizeControlsSkipped,
    /// <summary>Accepted control invalidation requests; not the number of OS paint messages.</summary>
    InvalidationRequests,
    /// <summary>Window invalidation requests delivered to the existing backend.</summary>
    WindowInvalidationRequests,
    /// <summary>Window invalidation requests coalesced by the existing batch.</summary>
    CoalescedWindowInvalidations,
    /// <summary>Outer framework input routes.</summary>
    InputEvents,
    /// <summary>Observed shared animation scheduler processing ticks.</summary>
    AnimationTicks,
    /// <summary>Framework-owned shaders created, including transformed replacements.</summary>
    ShadersCreated,
    /// <summary>Framework-owned shaders disposed at their actual ownership boundary.</summary>
    ShadersDisposed,
    /// <summary>Observed software surface/backing allocations.</summary>
    SurfaceAllocations,
    /// <summary>Observed software surface/backing releases.</summary>
    SurfaceReleases
}

/// <summary>Describes a bounded diagnostic rectangle in logical root coordinates.</summary>
public enum PerformanceRegionKind
{
    /// <summary>The rectangular extent of an actual control repaint.</summary>
    Repaint,
    /// <summary>An approximate transformed control bounding box.</summary>
    ControlBounds,
    /// <summary>An approximate rectangular clipping extent, not exact curved geometry.</summary>
    ClipBounds,
    /// <summary>An invalidation request whose coordinate space has been verified.</summary>
    InvalidationRequest
}

/// <summary>Describes the framework rendering path, independently from the operating-system compositor.</summary>
public enum PerformanceAcceleration
{
    /// <summary>The host does not report an acceleration capability.</summary>
    Unknown,
    /// <summary>The observed framework path rasterizes into CPU-accessible memory.</summary>
    Software,
    /// <summary>A host explicitly reports a hardware rendering path.</summary>
    Hardware
}

/// <summary>Identifies the exact timed frame boundary.</summary>
public enum PerformanceFrameBoundary
{
    /// <summary>Only the borrowed shared Skia surface's rendering call is timed.</summary>
    SharedRender,
    /// <summary>The native paint/view-draw callback is timed; this is not presentation completion.</summary>
    NativePaint,
    /// <summary>An explicit offscreen paint capture is timed, with no display FPS claim.</summary>
    OffscreenCapture
}

/// <summary>Describes the actual host repaint policy, separately from requested dirty regions and control cache reuse.</summary>
public enum PerformanceRedraw
{
    /// <summary>The host does not establish its repaint coverage.</summary>
    Unknown,
    /// <summary>The current host paints the full surface.</summary>
    FullSurface
}

/// <summary>Defines whether an extension metric accumulates deltas or reports a current value.</summary>
public enum PerformanceExtensionKind
{
    /// <summary>A nonnegative cumulative count updated by adding deltas.</summary>
    Counter,
    /// <summary>A current finite numeric value replaced by each report.</summary>
    Gauge
}
