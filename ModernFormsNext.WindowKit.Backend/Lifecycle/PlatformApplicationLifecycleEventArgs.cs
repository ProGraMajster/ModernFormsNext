namespace ModernFormsNext.WindowKit.Backend.Lifecycle;

/// <summary>Describes a committed application lifecycle change.</summary>
public sealed class PlatformApplicationLifecycleEventArgs : EventArgs
{
    /// <summary>Creates immutable transition information.</summary>
    /// <param name="previous">The preceding snapshot.</param>
    /// <param name="current">The snapshot committed before notification.</param>
    /// <param name="sequence">The provider-local monotonic notification sequence.</param>
    public PlatformApplicationLifecycleEventArgs(
        PlatformApplicationLifecycleSnapshot previous, PlatformApplicationLifecycleSnapshot current, long sequence)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        Previous = previous;
        Current = current;
        Sequence = sequence;
    }
    /// <summary>Gets the preceding immutable snapshot.</summary>
    public PlatformApplicationLifecycleSnapshot Previous { get; }
    /// <summary>Gets the immutable snapshot committed before callbacks run.</summary>
    public PlatformApplicationLifecycleSnapshot Current { get; }
    /// <summary>Gets the provider-local notification sequence.</summary>
    public long Sequence { get; }
}

/// <summary>Describes an explicit activation delivered on the owning UI thread.</summary>
public sealed class PlatformApplicationActivationEventArgs : EventArgs
{
    /// <summary>Creates immutable activation notification data.</summary>
    /// <param name="activation">The bounded activation payload.</param>
    /// <param name="sequence">The provider-local notification sequence.</param>
    public PlatformApplicationActivationEventArgs(PlatformApplicationActivation activation, long sequence)
    {
        ArgumentNullException.ThrowIfNull(activation);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        Activation = activation;
        Sequence = sequence;
    }
    /// <summary>Gets the bounded activation payload; diagnostics should not expose its contents by default.</summary>
    public PlatformApplicationActivation Activation { get; }
    /// <summary>Gets the provider-local notification sequence.</summary>
    public long Sequence { get; }
}

/// <summary>Allows synchronous UI-thread observers to provide bounded application state.</summary>
/// <remarks>
/// Set <see cref="Data"/> during the callback. If several observers assign it, the last successful
/// assignment wins in subscription order. The handoff is sealed after notification and does not
/// grant an asynchronous deferral or guarantee that the operating system will persist it.
/// </remarks>
public sealed class PlatformApplicationStateSavingEventArgs : EventArgs
{
    private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
    private PlatformApplicationStateData? data;
    private bool sealedData;

    /// <summary>Creates a synchronous state-saving request.</summary>
    /// <param name="reason">Why state is being requested.</param>
    /// <param name="sequence">The provider-local notification sequence.</param>
    public PlatformApplicationStateSavingEventArgs(PlatformApplicationStateReason reason, long sequence = 0)
    {
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        Reason = reason;
        Sequence = sequence;
    }
    /// <summary>Gets why state is being requested.</summary>
    public PlatformApplicationStateReason Reason { get; }
    /// <summary>Gets the provider-local notification sequence.</summary>
    public long Sequence { get; }
    /// <summary>Gets or sets the immutable state handoff during the synchronous callback.</summary>
    /// <exception cref="InvalidOperationException">The callback has ended or a different thread attempts mutation.</exception>
    public PlatformApplicationStateData? Data
    {
        get => Volatile.Read(ref data);
        set
        {
            if (Environment.CurrentManagedThreadId != ownerThreadId || sealedData)
                throw new InvalidOperationException("Application state can be supplied only during its owning UI-thread save callback.");
            Volatile.Write(ref data, value);
        }
    }
    internal void Seal() => sealedData = true;
}

/// <summary>Provides immutable application-owned state for restoration on the UI thread.</summary>
public sealed class PlatformApplicationStateRestoringEventArgs : EventArgs
{
    /// <summary>Creates a restoration request.</summary>
    /// <param name="data">The bounded state handoff.</param>
    /// <param name="reason">Why state is being restored.</param>
    /// <param name="sequence">The provider-local notification sequence.</param>
    public PlatformApplicationStateRestoringEventArgs(
        PlatformApplicationStateData data, PlatformApplicationStateReason reason, long sequence = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        Data = data;
        Reason = reason;
        Sequence = sequence;
    }
    /// <summary>Gets the bounded immutable application state.</summary>
    public PlatformApplicationStateData Data { get; }
    /// <summary>Gets why state is being restored.</summary>
    public PlatformApplicationStateReason Reason { get; }
    /// <summary>Gets the provider-local notification sequence.</summary>
    public long Sequence { get; }
}
