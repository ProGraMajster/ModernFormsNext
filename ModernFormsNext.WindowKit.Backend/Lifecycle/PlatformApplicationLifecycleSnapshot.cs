namespace ModernFormsNext.WindowKit.Backend.Lifecycle;

/// <summary>Describes the application lifetime independently of an individual native host.</summary>
public enum PlatformApplicationPhase
{
    /// <summary>No application startup has been reported.</summary>
    NotStarted,
    /// <summary>Application startup or initial restoration is in progress.</summary>
    Starting,
    /// <summary>The application is running, either in the foreground or background.</summary>
    Running,
    /// <summary>The platform has explicitly suspended application work.</summary>
    Suspended,
    /// <summary>Application shutdown has begun; later host activity cannot restart it.</summary>
    Exiting,
    /// <summary>Application shutdown has completed.</summary>
    Exited
}

/// <summary>Provides an immutable, platform-neutral application and host lifecycle snapshot.</summary>
/// <remarks>
/// Window activation is distinct from foreground availability: a desktop application can remain
/// foreground-capable while another process has keyboard focus. A new host generation identifies
/// recreation of native hosting resources; it does not imply a new application lifetime.
/// </remarks>
public sealed record PlatformApplicationLifecycleSnapshot
{
    /// <summary>Creates a validated lifecycle snapshot.</summary>
    /// <param name="phase">The application lifetime phase.</param>
    /// <param name="state">The existing foreground/background host state.</param>
    /// <param name="isActive">Whether the application currently owns active foreground interaction.</param>
    /// <param name="hostCount">The non-negative number of available application UI hosts.</param>
    /// <param name="hostGeneration">The non-negative, monotonically increasing native-host generation.</param>
    /// <exception cref="ArgumentException">The activation, phase, or host values are inconsistent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum or numeric value is invalid.</exception>
    public PlatformApplicationLifecycleSnapshot(
        PlatformApplicationPhase phase,
        PlatformApplicationLifecycleState state,
        bool isActive = false,
        int hostCount = 0,
        long hostGeneration = 0)
    {
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        ArgumentOutOfRangeException.ThrowIfNegative(hostCount);
        ArgumentOutOfRangeException.ThrowIfNegative(hostGeneration);
        if (isActive && (state != PlatformApplicationLifecycleState.Foreground || hostCount == 0 ||
            phase is PlatformApplicationPhase.Suspended or PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited))
            throw new ArgumentException("An active application requires a foreground host and an interactive phase.", nameof(isActive));
        if (state == PlatformApplicationLifecycleState.NoHost && hostCount != 0)
            throw new ArgumentException("NoHost cannot report available hosts.", nameof(hostCount));
        if (state == PlatformApplicationLifecycleState.Foreground && hostCount == 0)
            throw new ArgumentException("Foreground availability requires at least one UI host.", nameof(hostCount));
        if (phase == PlatformApplicationPhase.Suspended &&
            state is not (PlatformApplicationLifecycleState.Background or PlatformApplicationLifecycleState.NoHost))
            throw new ArgumentException("A suspended application must report Background or NoHost.", nameof(state));
        if (phase == PlatformApplicationPhase.Exited && (hostCount != 0 || state != PlatformApplicationLifecycleState.NoHost))
            throw new ArgumentException("An exited application must report NoHost and zero hosts.", nameof(phase));

        Phase = phase;
        State = state;
        IsActive = isActive;
        HostCount = hostCount;
        HostGeneration = hostGeneration;
    }

    /// <summary>Gets the application lifetime phase.</summary>
    public PlatformApplicationPhase Phase { get; }
    /// <summary>Gets the existing foreground/background host state.</summary>
    public PlatformApplicationLifecycleState State { get; }
    /// <summary>Gets whether the application owns active foreground interaction.</summary>
    public bool IsActive { get; }
    /// <summary>Gets the number of available UI hosts, independently of focused-window count.</summary>
    public int HostCount { get; }
    /// <summary>Gets the native-host generation used to reject stale recreation notifications.</summary>
    public long HostGeneration { get; }
}
