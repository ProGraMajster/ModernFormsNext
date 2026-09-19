using System.Drawing;

namespace ModernFormsNext.Diagnostics;

/// <summary>Represents one completed, immutable observation of a host or shared-only render callback.</summary>
public readonly record struct PerformanceFrameMetrics
{
    /// <summary>Gets the session-local completion sequence.</summary>
    public long Sequence { get; init; }
    /// <summary>Gets an ephemeral weak-source identity; it is not a native handle.</summary>
    public long SourceId { get; init; }
    /// <summary>Gets copied host facts and measurement availability.</summary>
    public PerformanceRenderInfo RenderInfo { get; init; }
    /// <summary>Gets the start offset from session start using the monotonic measurement clock.</summary>
    public TimeSpan Started { get; init; }
    /// <summary>Gets elapsed callback duration, including waits/preemption, rather than CPU execution or GPU time.</summary>
    public TimeSpan Duration { get; init; }
    /// <summary>Gets the interval from the preceding comparable source start, including idle time; null for a new generation.</summary>
    public TimeSpan? Interval { get; init; }
    /// <summary>Gets observed callback frequency from the interval, not display refresh or presented FPS.</summary>
    public double? FramesPerSecond => Interval is { Ticks: > 0 } interval ? 1d / interval.TotalSeconds : null;
    /// <summary>Gets work observed during this frame's active scope.</summary>
    public PerformanceWorkMetrics Work { get; init; }
    /// <summary>Gets work observed outside host frames since the preceding outer frame started, on this UI thread.</summary>
    /// <remarks>This is thread context, not work attributed to this particular source. Nested frames do not duplicate it.</remarks>
    public PerformanceWorkMetrics ThreadWorkSincePreviousFrame { get; init; }
    /// <summary>Gets whether the instrumented callback returned through its successful completion path.</summary>
    public bool Completed { get; init; }
    /// <summary>Gets whether measured duration exceeds the configured slow-frame threshold.</summary>
    public bool IsSlow { get; init; }
    /// <summary>Gets UI-thread managed allocation during this frame when explicitly enabled.</summary>
    public long? AllocatedBytes { get; init; }
    /// <summary>Gets process-wide generation-zero collection delta, if enabled; causality is not attributed.</summary>
    public int? Gen0Collections { get; init; }
    /// <summary>Gets process-wide generation-one collection delta, if enabled.</summary>
    public int? Gen1Collections { get; init; }
    /// <summary>Gets process-wide generation-two collection delta, if enabled.</summary>
    public int? Gen2Collections { get; init; }
}

/// <summary>Provides bounded, detached aggregate work for one control identity.</summary>
public readonly record struct PerformanceControlMetrics
{
    /// <summary>Gets the session-local control identity.</summary>
    public long ControlId { get; init; }
    /// <summary>Gets CLR type metadata; control names, text and values are not inspected.</summary>
    public string? ControlType { get; init; }
    /// <summary>Gets work observed for this control while detailed recording was enabled.</summary>
    public PerformanceWorkMetrics Work { get; init; }
}

/// <summary>Contains one approximate diagnostic rectangle with no live control reference.</summary>
public readonly record struct PerformanceRegion
{
    /// <summary>Gets the frame source identity.</summary>
    public long SourceId { get; init; }
    /// <summary>Gets the optional detailed control identity; zero means unavailable.</summary>
    public long ControlId { get; init; }
    /// <summary>Gets the observation kind and its coordinate qualification.</summary>
    public PerformanceRegionKind Kind { get; init; }
    /// <summary>Gets the approximate rectangle in root logical pixels.</summary>
    public RectangleF Bounds { get; init; }
}

/// <summary>Contains a numeric extension observation explicitly registered by an application or subsystem.</summary>
public readonly record struct PerformanceExtensionMetric
{
    /// <summary>Gets the explicitly authored metric name, never inferred from UI content.</summary>
    public string? Name { get; init; }
    /// <summary>Gets the explicitly authored unit.</summary>
    public string? Unit { get; init; }
    /// <summary>Gets counter or gauge semantics.</summary>
    public PerformanceExtensionKind Kind { get; init; }
    /// <summary>Gets the latest numeric observation; null means registered but not reported.</summary>
    public double? Value { get; init; }
}

/// <summary>Contains statistics over the retained completed frame sample for one source.</summary>
public readonly record struct PerformanceStatistics
{
    /// <summary>Gets the source represented by this sample.</summary>
    public long SourceId { get; init; }
    /// <summary>Gets the retained sample count, not the lifetime frame count.</summary>
    public int SampleCount { get; init; }
    /// <summary>Gets mean measured frame duration.</summary>
    public TimeSpan Mean { get; init; }
    /// <summary>Gets nearest-rank median measured duration.</summary>
    public TimeSpan Median { get; init; }
    /// <summary>Gets nearest-rank 95th-percentile measured duration.</summary>
    public TimeSpan P95 { get; init; }
    /// <summary>Gets nearest-rank 99th-percentile measured duration.</summary>
    public TimeSpan P99 { get; init; }
    /// <summary>Gets the worst retained measured duration.</summary>
    public TimeSpan Maximum { get; init; }
}

/// <summary>Contains detached profiling data safe to inspect on any thread after capture.</summary>
/// <remarks>All lists are read-only copies. Capture and JSON export allocate explicitly outside normal metric recording.</remarks>
public sealed class PerformanceSnapshot
{
    internal PerformanceSnapshot(PerformanceFrameMetrics[] frames, PerformanceFrameMetrics[] slowFrames,
        PerformanceControlMetrics[] controls, PerformanceRegion[] regions, PerformanceExtensionMetric[] extensions,
        PerformanceWorkMetrics unframedWork, long totalFrames, long droppedFrames, long droppedDetails,
        long droppedSources, long droppedRegions, long droppedScopes, long recorderFailures,
        TimeSpan elapsed, string frameworkVersion, string runtimeVersion)
    {
        Frames = Array.AsReadOnly(frames); SlowFrames = Array.AsReadOnly(slowFrames);
        Controls = Array.AsReadOnly(controls); Regions = Array.AsReadOnly(regions);
        Extensions = Array.AsReadOnly(extensions); UnframedWork = unframedWork;
        TotalFrames = totalFrames; DroppedFrames = droppedFrames; DroppedDetails = droppedDetails;
        DroppedSources = droppedSources; DroppedRegions = droppedRegions;
        DroppedScopes = droppedScopes; RecorderFailures = recorderFailures; Elapsed = elapsed;
        FrameworkVersion = frameworkVersion; RuntimeVersion = runtimeVersion;
    }

    /// <summary>Gets retained frames in completion order.</summary>
    public IReadOnlyList<PerformanceFrameMetrics> Frames { get; }
    /// <summary>Gets separately bounded slow-frame samples in completion order.</summary>
    public IReadOnlyList<PerformanceFrameMetrics> SlowFrames { get; }
    /// <summary>Gets the most recently completed retained frame.</summary>
    public PerformanceFrameMetrics? LatestFrame => Frames.Count == 0 ? null : Frames[^1];
    /// <summary>Gets cumulative UI-thread work observed while no frame was active.</summary>
    public PerformanceWorkMetrics UnframedWork { get; }
    /// <summary>Gets optional bounded control details.</summary>
    public IReadOnlyList<PerformanceControlMetrics> Controls { get; }
    /// <summary>Gets bounded regions from the most recently completed frame.</summary>
    public IReadOnlyList<PerformanceRegion> Regions { get; }
    /// <summary>Gets explicitly registered numeric extension metrics.</summary>
    public IReadOnlyList<PerformanceExtensionMetric> Extensions { get; }
    /// <summary>Gets total completed frame observations, including failed callbacks.</summary>
    public long TotalFrames { get; }
    /// <summary>Gets older frames overwritten by the history capacity.</summary>
    public long DroppedFrames { get; }
    /// <summary>Gets control-detail observations omitted after capacity was reached.</summary>
    public long DroppedDetails { get; }
    /// <summary>Gets source observations omitted after identity capacity was reached.</summary>
    public long DroppedSources { get; }
    /// <summary>Gets rectangle observations omitted by the bounded region buffer.</summary>
    public long DroppedRegions { get; }
    /// <summary>Gets activity/frame/resource observations omitted by scope capacity.</summary>
    public long DroppedScopes { get; }
    /// <summary>Gets contained native recorder callback failures.</summary>
    public long RecorderFailures { get; }
    /// <summary>Gets monotonic elapsed time since session start.</summary>
    public TimeSpan Elapsed { get; }
    /// <summary>Gets the framework informational version captured once at session start.</summary>
    public string FrameworkVersion { get; }
    /// <summary>Gets the runtime version captured once at session start.</summary>
    public string RuntimeVersion { get; }

    /// <summary>Computes duration statistics for one source from this detached retained sample.</summary>
    /// <param name="sourceId">The source identity to examine.</param>
    /// <returns>Copied statistics, with zero sample count when the source is absent.</returns>
    public PerformanceStatistics GetStatistics(long sourceId)
    {
        long[] ticks = Frames.Where(frame => frame.SourceId == sourceId).Select(frame => frame.Duration.Ticks).Order().ToArray();
        if (ticks.Length == 0) return new() { SourceId = sourceId };
        double mean = ticks.Average(value => (double)value);
        return new() { SourceId = sourceId, SampleCount = ticks.Length,
            Mean = mean >= long.MaxValue ? TimeSpan.MaxValue : TimeSpan.FromTicks((long)mean),
            Median = At(.5), P95 = At(.95), P99 = At(.99), Maximum = TimeSpan.FromTicks(ticks[^1]) };
        TimeSpan At(double fraction) => TimeSpan.FromTicks(ticks[Math.Max(0, (int)Math.Ceiling(ticks.Length * fraction) - 1)]);
    }
}
