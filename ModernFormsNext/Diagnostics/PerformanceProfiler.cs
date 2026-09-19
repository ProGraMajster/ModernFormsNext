using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Diagnostics;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.Diagnostics;

/// <summary>Collects bounded performance observations from the existing framework pipeline on one UI thread.</summary>
/// <remarks>
/// Disabled by default. Start, configure, capture and dispose on the owner UI thread. There is no
/// sampling timer or render loop. Captured values are detached and safe to examine on other threads.
/// Callback durations include preemption and waits; they do not measure GPU completion or scanout.
/// Dispose the profiler before its UI dispatcher shuts down. Disposal never owns application controls.
/// </remarks>
/// <example>
/// <code>
/// using var profiler = PerformanceProfiler.Start(new PerformanceProfilerOptions {
///     TrackAllocations = true, SlowFrameThreshold = TimeSpan.FromMilliseconds(33.3)
/// });
/// // Run the application's normal operations, with or without an overlay.
/// PerformanceSnapshot snapshot = profiler.Capture();
/// </code>
/// </example>
public sealed partial class PerformanceProfiler : IDisposable, IPlatformPerformanceSink
{
    internal const int ActivityCapacity = 128;
    internal const int FrameDepthCapacity = 64;
    private const int ActivityKinds = 10;
    private const int CounterKinds = 19;
    [ThreadStatic] private static PerformanceProfiler? current;
    private readonly int ownerThread = Environment.CurrentManagedThreadId;
    private readonly PerformanceProfilerOptions options;
    private readonly Func<long> timestamp;
    private readonly double ticksPerTimestamp;
    private readonly long started;
    private long lastTimestamp;
    private readonly ActivityState[] activities = new ActivityState[ActivityCapacity];
    private readonly int[] activeCategories = new int[ActivityKinds];
    private readonly FrameState[] frameStack = new FrameState[FrameDepthCapacity];
    private readonly PerformanceFrameMetrics[] frames;
    private readonly PerformanceFrameMetrics[] slowFrames;
    private readonly PerformanceFrameMetrics[] overlayFrames;
    private readonly PerformanceRegion[] lastRegions;
    private readonly Work unframed = new();
    private readonly Work pendingUnframed = new();
    private readonly ConditionalWeakTable<object, SourceIdentity> sources = new();
    private readonly ConditionalWeakTable<Control, DetailIdentity> detailIdentities = new();
    private readonly DetailState?[] details;
    private readonly WeakReference<Control>?[] roots;
    private readonly ConditionalWeakTable<Control, RootIdentity> rootIdentities = new();
    private readonly PerformanceExtensionMetric[] extensions;
    private readonly Dictionary<long, ResourceLease> resources = new(4096);
    private readonly WeakReference<PerformanceProfiler> weakSelf;
    private readonly PlatformPerformanceRegistration registration;
    private PerformanceOverlayOptions overlayOptions;
    private int frameDepth, frameCount, frameNext, slowCount, slowNext, sourceCount, detailCount, rootCount, extensionCount, lastRegionCount;
    private long tokenSequence, resourceSequence, totalFrames, droppedDetails, droppedSources, droppedRegions, droppedScopes, ownRecorderFailures;
    private bool disposed, renderingOverlay, requestingRepaint;
    private readonly string frameworkVersion = typeof(Control).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    private readonly string runtimeVersion = Environment.Version.ToString();

    private PerformanceProfiler(PerformanceProfilerOptions options, Func<long> timestamp, long frequency)
    {
        this.options = options;
        this.timestamp = timestamp;
        ticksPerTimestamp = (double)TimeSpan.TicksPerSecond / frequency;
        overlayOptions = options.Overlay;
        frames = new PerformanceFrameMetrics[options.FrameCapacity];
        slowFrames = new PerformanceFrameMetrics[options.SlowFrameCapacity];
        overlayFrames = new PerformanceFrameMetrics[options.FrameCapacity];
        lastRegions = new PerformanceRegion[options.RegionCapacity];
        details = new DetailState[options.DetailCapacity];
        roots = new WeakReference<Control>[options.SourceCapacity];
        extensions = new PerformanceExtensionMetric[options.CounterCapacity];
        for (int i = 0; i < frameStack.Length; i++) frameStack[i] = new(options.RegionCapacity);
        weakSelf = new(this);
        started = lastTimestamp = timestamp();
        registration = PlatformPerformanceDiagnostics.Register(this);
    }

    /// <summary>Starts the only profiling session on the current UI thread without requesting rendering or starting a timer.</summary>
    /// <param name="options">Optional bounded collection settings; the overlay is off by default.</param>
    /// <returns>The caller-owned session.</returns>
    /// <exception cref="InvalidOperationException">Another session owns this thread, or the current thread is not the configured UI thread.</exception>
    public static PerformanceProfiler Start(PerformanceProfilerOptions? options = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        return StartCore(options, Stopwatch.GetTimestamp, Stopwatch.Frequency);
    }

    internal static PerformanceProfiler StartForTesting(PerformanceProfilerOptions? options, Func<long> timestamp, long frequency)
        => StartCore(options, timestamp, frequency);

    private static PerformanceProfiler StartCore(PerformanceProfilerOptions? options, Func<long> timestamp, long frequency)
    {
        if (current is { disposed: false }) throw new InvalidOperationException("A performance profiler already owns this UI thread.");
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequency);
        var validated = (options ?? new()).ValidateAndCopy();
        var profiler = new PerformanceProfiler(validated, timestamp, frequency);
        current = profiler;
        return profiler;
    }

    internal static PerformanceProfiler? Current => current;
    internal bool ShouldRecordRegions => !disposed && !renderingOverlay &&
        (overlayOptions.ShowRepaintRegions || overlayOptions.ShowControlBounds || overlayOptions.ShowClipBounds);

    /// <summary>Gets or replaces the immutable visual options independently from metric collection.</summary>
    /// <remarks>Use on the owner thread. A valid change requests one repaint of known live roots; it does not create an idle refresh loop.</remarks>
    public PerformanceOverlayOptions OverlayOptions
    {
        get { VerifyAccess(); return overlayOptions; }
        set {
            VerifyAccess(); ArgumentNullException.ThrowIfNull(value); value.Validate();
            if (value == overlayOptions) return;
            overlayOptions = value;
            RequestRootPaints();
        }
    }

    /// <summary>Begins explicitly instrumented work using the same bounded recorder as framework scopes.</summary>
    /// <param name="activity">The work category; durations within each category are unioned.</param>
    /// <returns>A nonallocating scope to dispose on the owner UI thread.</returns>
    public PerformanceScope Measure(PerformanceActivityKind activity = PerformanceActivityKind.Custom)
    {
        VerifyAccess();
        if ((uint)activity >= ActivityKinds) throw new ArgumentOutOfRangeException(nameof(activity));
        return new(this, BeginActivity(activity, null));
    }

    /// <summary>Copies the current bounded data, without enumerating controls or invoking application callbacks.</summary>
    /// <returns>An immutable snapshot safe to read on another thread.</returns>
    public PerformanceSnapshot Capture()
    {
        VerifyAccess(); Advance();
        var controlData = new PerformanceControlMetrics[detailCount];
        for (int i = 0; i < detailCount; i++) {
            DetailState detail = details[i]!;
            controlData[i] = new() { ControlId = i + 1, ControlType = detail.Type, Work = Copy(detail.Work) };
        }
        return new(CopyRing(frames, frameCount, frameNext), CopyRing(slowFrames, slowCount, slowNext),
            controlData, lastRegions.AsSpan(0, lastRegionCount).ToArray(), extensions.AsSpan(0, extensionCount).ToArray(),
            Copy(unframed), totalFrames, Math.Max(0, totalFrames - frameCount), droppedDetails,
            droppedSources, droppedRegions, droppedScopes + registration.OmittedScopeCount,
            ownRecorderFailures + registration.RecorderFailureCount, ToTime(lastTimestamp - started), frameworkVersion, runtimeVersion);
    }

    /// <summary>Writes an explicit detached JSON capture to a caller-owned stream, leaving it open.</summary>
    /// <param name="destination">A writable stream; no automatic file path or network destination is chosen.</param>
    /// <remarks>This synchronous operation allocates and performs IO; call outside painting and use detached captures for background export.</remarks>
    public void WriteJson(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        JsonSerializer.Serialize(destination, Capture(), new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Registers an explicitly authored numeric metric without coupling its producer to the overlay.</summary>
    /// <param name="name">A unique nonempty name of at most 64 characters; do not use sensitive application content.</param>
    /// <param name="kind">Counter or gauge semantics.</param>
    /// <param name="unit">An authored unit label of at most 24 characters.</param>
    /// <returns>A revocable session handle; the value remains unavailable until first reported.</returns>
    public PerformanceExtensionCounter RegisterCounter(string name, PerformanceExtensionKind kind, string unit = "count")
    {
        VerifyAccess(); ValidateLabel(name, 64, nameof(name)); ValidateLabel(unit, 24, nameof(unit));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        for (int i = 0; i < extensionCount; i++)
            if (string.Equals(extensions[i].Name, name, StringComparison.Ordinal)) throw new ArgumentException("Metric name is already registered.", nameof(name));
        if (extensionCount == extensions.Length) throw new InvalidOperationException("The extension metric capacity is exhausted.");
        int index = extensionCount++;
        extensions[index] = new() { Name = name, Unit = unit, Kind = kind };
        return new(weakSelf, index);
    }

    internal void ReportExtension(int index, double value, bool add)
    {
        VerifyAccess();
        if ((uint)index >= extensionCount) throw new InvalidOperationException("The metric does not belong to this session.");
        if (!double.IsFinite(value) || (add && value < 0)) throw new ArgumentOutOfRangeException(nameof(value));
        var metric = extensions[index];
        if (add != (metric.Kind == PerformanceExtensionKind.Counter)) throw new InvalidOperationException("Use Add for counters and Set for gauges.");
        double result = add ? (metric.Value ?? 0) + value : value;
        if (!double.IsFinite(result)) throw new ArgumentOutOfRangeException(nameof(value), "The resulting metric must remain finite.");
        extensions[index] = metric with { Value = result };
    }

    /// <summary>Revokes native ingress and all live scope handles without disposing application UI.</summary>
    /// <remarks>Use on the owner thread before dispatcher shutdown. Known roots receive one repaint to remove a visible HUD. Repeated disposal is harmless.</remarks>
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != ownerThread) throw new InvalidOperationException("Dispose the performance profiler on its owning UI thread.");
        if (disposed) return;
        bool repaint = overlayOptions.Visible || ShouldRecordRegions;
        if (ReferenceEquals(current, this)) current = null;
        registration.Dispose();
        while (frameDepth > 0) EndFrame(frameStack[frameDepth - 1].Token, false);
        Advance();
        disposed = true;
        Array.Clear(activities); Array.Clear(activeCategories); resources.Clear(); sources.Clear(); detailIdentities.Clear();
        // No user callback runs until ingress is revoked and recording is terminal.
        try { if (repaint) RequestRootPaints(); }
        finally {
            Array.Clear(roots); rootIdentities.Clear(); Array.Clear(details);
            Array.Clear(frames); Array.Clear(slowFrames); Array.Clear(overlayFrames);
            Array.Clear(lastRegions); Array.Clear(extensions);
        }
    }

    private void RequestRootPaints()
    {
        // Invalidated handlers may configure the HUD again. One repaint is already pending;
        // do not recursively call application handlers for that reentrant configuration.
        if (requestingRepaint) return;
        requestingRepaint = true;
        try {
            List<Exception>? failures = null;
            for (int i = 0; i < rootCount; i++) {
                if (roots[i] is not { } weakRoot || !weakRoot.TryGetTarget(out var root) || root.IsDisposed) continue;
                try { root.Invalidate(); }
                catch (Exception exception) { (failures ??= []).Add(exception); }
            }
            if (failures is { Count: > 0 }) throw new AggregateException("One or more diagnostic repaint requests failed.", failures);
        }
        finally { requestingRepaint = false; }
    }

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != ownerThread) throw new InvalidOperationException("Performance profiling is owned by its UI thread.");
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static void ValidateLabel(string value, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl)) throw new ArgumentException("Use a bounded nonempty label without control characters.", name);
    }
    private static PerformanceFrameMetrics[] CopyRing(PerformanceFrameMetrics[] ring, int count, int next)
    {
        var result = new PerformanceFrameMetrics[count];
        int start = (next - count + ring.Length) % ring.Length;
        for (int i = 0; i < count; i++) result[i] = ring[(start + i) % ring.Length];
        return result;
    }
    private TimeSpan ToTime(long delta) => PerformanceWorkMetrics.ToTime(delta, ticksPerTimestamp);
    private PerformanceWorkMetrics Copy(Work work) => new(work.Durations, work.Counters, ticksPerTimestamp);
    private sealed class Work
    {
        internal readonly long[] Durations = new long[ActivityKinds];
        internal readonly long[] Counters = new long[CounterKinds];
        internal void Clear() { Array.Clear(Durations); Array.Clear(Counters); }
    }
    private struct ActivityState { internal long Token; internal int Category, Detail; internal bool Completed; }
    private sealed class DetailState(string type)
    { internal readonly string Type = type; internal readonly Work Work = new(); internal readonly int[] Active = new int[ActivityKinds]; }
    private sealed class DetailIdentity(int index) { internal readonly int Index = index; }
    private sealed class RootIdentity(int index) { internal readonly int Index = index; internal long SourceId; }
    private sealed class SourceIdentity(long id)
    { internal readonly long Id = id; internal long? LastStart; internal long? Generation; }
    private readonly record struct ResourceLease(PerformanceCounterKind Disposed, int Detail);
    private sealed class FrameState(int regionCapacity)
    {
        internal long Token, SourceId, Start, Allocated;
        internal int Gen0, Gen1, Gen2;
        internal TimeSpan? Interval;
        internal PerformanceRenderInfo Info;
        internal readonly Work Work = new();
        internal PerformanceWorkMetrics ThreadWork;
        internal readonly PerformanceRegion[] Regions = new PerformanceRegion[regionCapacity];
        internal int RegionCount;
        internal bool EndRequested, Completed, RenderCompleted;
    }
}
