namespace ModernFormsNext.WindowKit.Backend.Lifecycle;

/// <summary>
/// Provides a platform-neutral application lifecycle transition.
/// </summary>
public sealed class PlatformApplicationLifecycleChangedEventArgs : EventArgs
{
    /// <summary>Initializes lifecycle event data.</summary>
    /// <param name="previousState">The state observed before the transition.</param>
    /// <param name="currentState">The newly observed state.</param>
    public PlatformApplicationLifecycleChangedEventArgs(
        PlatformApplicationLifecycleState previousState,
        PlatformApplicationLifecycleState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }

    /// <summary>Gets the state observed before the transition.</summary>
    public PlatformApplicationLifecycleState PreviousState { get; }

    /// <summary>Gets the newly observed state.</summary>
    public PlatformApplicationLifecycleState CurrentState { get; }
}
