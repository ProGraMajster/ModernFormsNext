namespace ModernFormsNext.WindowKit.Backend.Lifecycle;

/// <summary>Adds application lifecycle, activation and restoration notifications to the existing provider.</summary>
/// <remarks>
/// This is an optional capability of the service already registered as
/// <see cref="IPlatformApplicationLifecycle"/>. Older providers remain valid. All callbacks execute
/// on the owning UI thread; snapshot and activation data are immutable. Applications subscribe
/// through their framework facade instead of creating another platform lifecycle runtime.
/// </remarks>
public interface IPlatformApplicationLifecycleNotifications : IPlatformApplicationLifecycle
{
    /// <summary>Gets the last committed application and host snapshot.</summary>
    PlatformApplicationLifecycleSnapshot Snapshot { get; }
    /// <summary>Gets the last explicit activation, or <see langword="null"/> before any activation.</summary>
    PlatformApplicationActivation? LastActivation { get; }
    /// <summary>Occurs after snapshot commit and the corresponding legacy state notification.</summary>
    event EventHandler<PlatformApplicationLifecycleEventArgs>? LifecycleChanged;
    /// <summary>Occurs when an explicit activation is delivered; repeated requests remain distinct.</summary>
    event EventHandler<PlatformApplicationActivationEventArgs>? ActivationReceived;
    /// <summary>Occurs synchronously when the platform or application requests a bounded state handoff.</summary>
    event EventHandler<PlatformApplicationStateSavingEventArgs>? StateSaving;
    /// <summary>Occurs when application-owned state should be restored before the host resumes interaction.</summary>
    event EventHandler<PlatformApplicationStateRestoringEventArgs>? StateRestoring;
}

/// <summary>Provides optional lifecycle ingress on the same registered platform lifecycle provider.</summary>
/// <remarks>
/// Mutations require the owning UI thread. Backends and the Application startup/shutdown path use
/// this interface; it does not create a second registry, dispatcher or application lifetime.
/// Native adapters must publish restoration before their resumed/foreground snapshot when a new
/// host requires restoration. No intermediate operating-system states are invented by this contract.
/// </remarks>
public interface IPlatformApplicationLifecycleController : IPlatformApplicationLifecycleNotifications
{
    /// <summary>Gets whether the owning UI thread is currently delivering or draining a notification.</summary>
    /// <remarks>Application exit can use this flag to defer its complete transaction until callbacks unwind.</remarks>
    bool IsPublishing { get; }
    /// <summary>Publishes a validated snapshot, suppressing duplicates and stale host generations.</summary>
    /// <param name="snapshot">The immutable application and host state.</param>
    void Publish(PlatformApplicationLifecycleSnapshot snapshot);
    /// <summary>Delivers explicit activation without interpreting or executing its payload.</summary>
    /// <param name="activation">The bounded activation data.</param>
    void Activate(PlatformApplicationActivation activation);
    /// <summary>Synchronously asks UI-thread observers for application-owned state.</summary>
    /// <param name="reason">Why state is requested.</param>
    /// <returns>The supplied state, or <see langword="null"/> when none is supplied or the application exited.</returns>
    /// <exception cref="InvalidOperationException">A synchronous save is requested reentrantly from a notification.</exception>
    PlatformApplicationStateData? RequestSaveState(PlatformApplicationStateReason reason);
    /// <summary>Delivers application-owned state for restoration on the UI thread.</summary>
    /// <param name="data">The bounded immutable state.</param>
    /// <param name="reason">Why state is restored.</param>
    void RestoreState(PlatformApplicationStateData data, PlatformApplicationStateReason reason);
}
