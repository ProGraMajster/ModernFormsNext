using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

namespace ModernFormsNext.WindowKit.Backend.Android;

/// <summary>
/// Represents a low-cost snapshot of Android animation-runtime integration state.
/// </summary>
/// <remarks>
/// This diagnostic surface contains no Activity, View, or callback references. It is intended for
/// manual smoke screens and targeted logging rather than per-frame telemetry.
/// </remarks>
public sealed class AndroidAnimationRuntimeDiagnostics
{
    internal AndroidAnimationRuntimeDiagnostics(
        AndroidApplicationLifecycleState lifecycleState,
        bool frameCallbackPending,
        bool schedulerDemand,
        int activeSurfaceCount,
        long postedFrameCallbackCount,
        long deliveredFrameCallbackCount,
        double durationScale,
        bool contentObserverRegistered,
        string? observerError)
    {
        LifecycleState = lifecycleState;
        FrameCallbackPending = frameCallbackPending;
        SchedulerDemand = schedulerDemand;
        ActiveSurfaceCount = activeSurfaceCount;
        PostedFrameCallbackCount = postedFrameCallbackCount;
        DeliveredFrameCallbackCount = deliveredFrameCallbackCount;
        DurationScale = durationScale;
        ContentObserverRegistered = contentObserverRegistered;
        ObserverError = observerError;
    }

    /// <summary>Gets the latest application lifecycle state.</summary>
    public AndroidApplicationLifecycleState LifecycleState { get; }

    /// <summary>Gets whether one Choreographer callback is currently pending.</summary>
    public bool FrameCallbackPending { get; }

    /// <summary>Gets whether the shared scheduler currently requests frames.</summary>
    public bool SchedulerDemand { get; }

    /// <summary>Gets the number of attached and resumed Android Skia surfaces.</summary>
    public int ActiveSurfaceCount { get; }

    /// <summary>Gets the number of native frame callbacks posted by this backend.</summary>
    public long PostedFrameCallbackCount { get; }

    /// <summary>Gets the number of native frame callbacks delivered to the shared scheduler.</summary>
    public long DeliveredFrameCallbackCount { get; }

    /// <summary>Gets the current Android animator-duration scale.</summary>
    public double DurationScale { get; }

    /// <summary>Gets whether the lifecycle-aware animator-scale observer is registered.</summary>
    public bool ContentObserverRegistered { get; }

    /// <summary>Gets the last ContentObserver registration error, if any.</summary>
    public string? ObserverError { get; }
}
