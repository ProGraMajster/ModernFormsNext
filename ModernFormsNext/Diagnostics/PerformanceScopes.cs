namespace ModernFormsNext.Diagnostics;

/// <summary>Measures an explicit activity in the current profiling session without allocating a scope object.</summary>
/// <remarks>Dispose on the owner UI thread. Copies close the same activity at most once; a default or retired scope is inert.</remarks>
public readonly struct PerformanceScope : IDisposable
{
    private readonly PerformanceProfiler? owner;
    private readonly long token;
    internal PerformanceScope(PerformanceProfiler owner, long token) { this.owner = owner; this.token = token; }
    /// <summary>Marks successful activity completion. Timing continues until disposal.</summary>
    public void Complete() => owner?.CompleteActivity(token);
    /// <summary>Ends this activity exactly once; it does not own the instrumented control or session.</summary>
    public void Dispose() => owner?.EndActivity(token);
}

// Complete ends content timing, while Dispose ends an owned shared-only frame after HUD rendering.
internal readonly struct PerformanceRenderScope : IDisposable
{
    private readonly PerformanceProfiler? owner;
    private readonly long frame;
    private readonly long activity;
    internal PerformanceRenderScope(PerformanceProfiler owner, long frame, long activity)
    { this.owner = owner; this.frame = frame; this.activity = activity; }
    internal void Complete() => owner?.CompleteRender(frame, activity);
    public void Dispose() => owner?.EndRender(frame, activity);
}

// One weak reference is shared by a session's leases. Resources never keep retired buffers,
// native owners or a disposed profiler alive; finalizer-thread disposal never posts UI work.
internal readonly struct PerformanceResourceToken : IDisposable
{
    private readonly WeakReference<PerformanceProfiler>? owner;
    private readonly long token;
    internal PerformanceResourceToken(WeakReference<PerformanceProfiler> owner, long token)
    { this.owner = owner; this.token = token; }
    public void Dispose()
    {
        if (owner is not null && owner.TryGetTarget(out var profiler)) profiler.ReleaseResource(token);
    }
}

/// <summary>A revocable numeric extension handle owned by one performance session.</summary>
/// <remarks>Use on the owner UI thread. A handle never retains a control; reports after session disposal are rejected.</remarks>
public readonly struct PerformanceExtensionCounter
{
    private readonly WeakReference<PerformanceProfiler>? owner;
    private readonly int index;
    internal PerformanceExtensionCounter(WeakReference<PerformanceProfiler> owner, int index)
    { this.owner = owner; this.index = index; }
    /// <summary>Adds a finite nonnegative delta to a counter.</summary>
    /// <param name="delta">The increment in the registered unit.</param>
    public void Add(double delta) => GetOwner().ReportExtension(index, delta, add: true);
    /// <summary>Replaces a gauge with a finite value.</summary>
    /// <param name="value">The current measurement in the registered unit.</param>
    public void Set(double value) => GetOwner().ReportExtension(index, value, add: false);
    private PerformanceProfiler GetOwner()
    {
        if (owner is not null && owner.TryGetTarget(out var profiler)) return profiler;
        throw new ObjectDisposedException(nameof(PerformanceProfiler));
    }
}
