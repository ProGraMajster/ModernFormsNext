namespace ModernFormsNext.Diagnostics;

/// <summary>Contains copied framework work. Each duration is a union within its category; different categories may overlap.</summary>
/// <remarks>Do not sum these values as total frame duration. Unframed work is kept separately from source frames.</remarks>
public readonly record struct PerformanceWorkMetrics
{
    internal PerformanceWorkMetrics(long[] durations, long[] counters, double ticksPerTimestamp)
    {
        LayoutTime = ToTime(durations[0], ticksPerTimestamp);
        PreferredSizeTime = ToTime(durations[1], ticksPerTimestamp);
        RenderTime = ToTime(durations[2], ticksPerTimestamp);
        InputTime = ToTime(durations[3], ticksPerTimestamp);
        AnimationTime = ToTime(durations[4], ticksPerTimestamp);
        ShaderCreationTime = ToTime(durations[5], ticksPerTimestamp);
        OverlayTime = ToTime(durations[6], ticksPerTimestamp);
        DesignerLayoutTime = ToTime(durations[7], ticksPerTimestamp);
        DesignerRenderTime = ToTime(durations[8], ticksPerTimestamp);
        CustomTime = ToTime(durations[9], ticksPerTimestamp);
        LayoutPasses = counters[0];
        PreferredSizeQueries = counters[1];
        PreferredSizeCacheHits = counters[2];
        PreferredSizeCoreCalls = counters[3];
        ControlsVisited = counters[4];
        ControlsRepainted = counters[5];
        ControlsComposited = counters[6];
        ControlCacheHits = counters[7];
        InvisibleControlsSkipped = counters[8];
        ZeroSizeControlsSkipped = counters[9];
        InvalidationRequests = counters[10];
        WindowInvalidationRequests = counters[11];
        CoalescedWindowInvalidations = counters[12];
        InputEvents = counters[13];
        AnimationTicks = counters[14];
        ShadersCreated = counters[15];
        ShadersDisposed = counters[16];
        SurfaceAllocations = counters[17];
        SurfaceReleases = counters[18];
    }

    internal static TimeSpan ToTime(long duration, double scale)
    {
        double ticks = Math.Max(0, duration * scale);
        // Compare before converting: long.MaxValue rounds to 2^63 as a double.
        return ticks >= long.MaxValue ? TimeSpan.MaxValue : TimeSpan.FromTicks((long)ticks);
    }

    /// <summary>Gets elapsed layout work, including preemption and waits.</summary>
    public TimeSpan LayoutTime { get; init; }
    /// <summary>Gets elapsed preferred size work, including preemption and waits.</summary>
    public TimeSpan PreferredSizeTime { get; init; }
    /// <summary>Gets elapsed render work, including preemption and waits.</summary>
    public TimeSpan RenderTime { get; init; }
    /// <summary>Gets elapsed input work, including preemption and waits.</summary>
    public TimeSpan InputTime { get; init; }
    /// <summary>Gets elapsed animation work, including preemption and waits.</summary>
    public TimeSpan AnimationTime { get; init; }
    /// <summary>Gets elapsed shader creation work, including preemption and waits.</summary>
    public TimeSpan ShaderCreationTime { get; init; }
    /// <summary>Gets elapsed overlay work, including preemption and waits.</summary>
    public TimeSpan OverlayTime { get; init; }
    /// <summary>Gets elapsed designer layout work, including preemption and waits.</summary>
    public TimeSpan DesignerLayoutTime { get; init; }
    /// <summary>Gets elapsed designer render work, including preemption and waits.</summary>
    public TimeSpan DesignerRenderTime { get; init; }
    /// <summary>Gets elapsed custom work, including preemption and waits.</summary>
    public TimeSpan CustomTime { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.LayoutPasses"/> count.</summary>
    public long LayoutPasses { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.PreferredSizeQueries"/> count.</summary>
    public long PreferredSizeQueries { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.PreferredSizeCacheHits"/> count.</summary>
    public long PreferredSizeCacheHits { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.PreferredSizeCoreCalls"/> count.</summary>
    public long PreferredSizeCoreCalls { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.ControlsVisited"/> count.</summary>
    public long ControlsVisited { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.ControlsRepainted"/> count.</summary>
    public long ControlsRepainted { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.ControlsComposited"/> count.</summary>
    public long ControlsComposited { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.ControlCacheHits"/> count.</summary>
    public long ControlCacheHits { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.InvisibleControlsSkipped"/> count.</summary>
    public long InvisibleControlsSkipped { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.ZeroSizeControlsSkipped"/> count.</summary>
    public long ZeroSizeControlsSkipped { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.InvalidationRequests"/> count.</summary>
    public long InvalidationRequests { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.WindowInvalidationRequests"/> count.</summary>
    public long WindowInvalidationRequests { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.CoalescedWindowInvalidations"/> count.</summary>
    public long CoalescedWindowInvalidations { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.InputEvents"/> count.</summary>
    public long InputEvents { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.AnimationTicks"/> count.</summary>
    public long AnimationTicks { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.ShadersCreated"/> count.</summary>
    public long ShadersCreated { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.ShadersDisposed"/> count.</summary>
    public long ShadersDisposed { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.SurfaceAllocations"/> count.</summary>
    public long SurfaceAllocations { get; init; }
    /// <summary>Gets the observed <see cref="PerformanceCounterKind.SurfaceReleases"/> count.</summary>
    public long SurfaceReleases { get; init; }
}
