namespace ModernFormsNext.Diagnostics;

/// <summary>Configures one opt-in UI-thread profiling session. Values are validated and copied at startup.</summary>
public sealed class PerformanceProfilerOptions
{
    /// <summary>Gets retained frame capacity, from 1 through 4096; default 256.</summary>
    public int FrameCapacity { get; init; } = 256;
    /// <summary>Gets retained slow-frame capacity, from 1 through 512; default 32.</summary>
    public int SlowFrameCapacity { get; init; } = 32;
    /// <summary>Gets the maximum detailed control identities, from 1 through 4096; default 128.</summary>
    public int DetailCapacity { get; init; } = 128;
    /// <summary>Gets the maximum source identities per session, from 1 through 1024; default 128.</summary>
    public int SourceCapacity { get; init; } = 128;
    /// <summary>Gets retained rectangles per completed frame, from 1 through 512; default 64.</summary>
    public int RegionCapacity { get; init; } = 64;
    /// <summary>Gets the maximum explicitly registered extension counters, from 1 through 256; default 64.</summary>
    public int CounterCapacity { get; init; } = 64;
    /// <summary>Gets whether bounded control identity, type and timing detail is collected; default false.</summary>
    public bool DetailedControls { get; init; }
    /// <summary>Gets whether UI-thread managed allocation deltas are read at frame boundaries; default false.</summary>
    /// <remarks>Includes user callbacks on this thread, but excludes native Skia/Java/GPU and other-thread allocation.</remarks>
    public bool TrackAllocations { get; init; }
    /// <summary>Gets whether process-wide GC collection deltas are read at frame boundaries; default false.</summary>
    public bool TrackGarbageCollections { get; init; }
    /// <summary>Gets the measured frame-duration threshold, greater than zero and at most one minute.</summary>
    /// <remarks>The default is 33.3 milliseconds. Idle gaps do not trigger this threshold.</remarks>
    public TimeSpan SlowFrameThreshold { get; init; } = TimeSpan.FromMilliseconds(33.3);
    /// <summary>Gets the optional visual configuration; collection works independently with its default visibility off.</summary>
    public PerformanceOverlayOptions Overlay { get; init; } = new();

    internal PerformanceProfilerOptions ValidateAndCopy()
    {
        Check(FrameCapacity, 4096, nameof(FrameCapacity));
        Check(SlowFrameCapacity, 512, nameof(SlowFrameCapacity));
        Check(DetailCapacity, 4096, nameof(DetailCapacity));
        Check(SourceCapacity, 1024, nameof(SourceCapacity));
        Check(RegionCapacity, 512, nameof(RegionCapacity));
        Check(CounterCapacity, 256, nameof(CounterCapacity));
        if (SlowFrameThreshold <= TimeSpan.Zero || SlowFrameThreshold > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(SlowFrameThreshold));
        ArgumentNullException.ThrowIfNull(Overlay);
        Overlay.Validate();
        return new PerformanceProfilerOptions {
            FrameCapacity = FrameCapacity, SlowFrameCapacity = SlowFrameCapacity, DetailCapacity = DetailCapacity,
            SourceCapacity = SourceCapacity, RegionCapacity = RegionCapacity, CounterCapacity = CounterCapacity,
            DetailedControls = DetailedControls, TrackAllocations = TrackAllocations,
            TrackGarbageCollections = TrackGarbageCollections, SlowFrameThreshold = SlowFrameThreshold, Overlay = Overlay
        };
    }

    private static void Check(int value, int maximum, string name)
    {
        if (value < 1 || value > maximum) throw new ArgumentOutOfRangeException(name);
    }
}
