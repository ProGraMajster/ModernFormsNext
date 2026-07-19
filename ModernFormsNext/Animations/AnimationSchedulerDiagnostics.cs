namespace ModernFormsNext.Animations;

/// <summary>
/// Represents a low-cost snapshot of animation scheduler diagnostics.
/// </summary>
/// <remarks>
/// Counters are intended for development diagnostics and ControlGallery. They do not constitute a
/// telemetry service and reset only when a new scheduler instance is created.
/// </remarks>
public sealed class AnimationSchedulerDiagnostics
{
    internal AnimationSchedulerDiagnostics(
        int activeAnimationCount,
        long tickCount,
        long completedCount,
        long canceledCount,
        long faultedCount,
        TimeSpan averageTickDuration,
        bool isTickSourceRunning,
        bool isPaused,
        bool isShutdown)
    {
        ActiveAnimationCount = activeAnimationCount;
        TickCount = tickCount;
        CompletedCount = completedCount;
        CanceledCount = canceledCount;
        FaultedCount = faultedCount;
        AverageTickDuration = averageTickDuration;
        IsTickSourceRunning = isTickSourceRunning;
        IsPaused = isPaused;
        IsShutdown = isShutdown;
    }

    /// <summary>Gets the number of non-terminal animations currently retained by the scheduler.</summary>
    public int ActiveAnimationCount { get; }

    /// <summary>Gets the number of UI-thread ticks that processed at least one animation.</summary>
    public long TickCount { get; }

    /// <summary>Gets the number of animations that reached successful completion.</summary>
    public long CompletedCount { get; }

    /// <summary>Gets the number of animations canceled explicitly, by replacement, or by shutdown.</summary>
    public long CanceledCount { get; }

    /// <summary>Gets the number of animations isolated after an easing, interpolation, or update fault.</summary>
    public long FaultedCount { get; }

    /// <summary>Gets the mean processing duration of recorded scheduler ticks.</summary>
    public TimeSpan AverageTickDuration { get; }

    /// <summary>Gets whether the shared periodic tick source is currently active.</summary>
    public bool IsTickSourceRunning { get; }

    /// <summary>Gets whether global scheduler time is paused.</summary>
    public bool IsPaused { get; }

    /// <summary>Gets whether the scheduler was permanently shut down.</summary>
    public bool IsShutdown { get; }
}
