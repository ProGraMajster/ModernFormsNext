namespace ModernFormsNext.WindowKit.Backend.Lifecycle;

/// <summary>
/// Exposes foreground and background application lifecycle without backend-specific types.
/// </summary>
/// <remarks>
/// Backends raise <see cref="StateChanged"/> on their UI thread. Consumers must keep handlers short
/// and must not assume that every platform produces all intermediate states.
/// </remarks>
public interface IPlatformApplicationLifecycle
{
    /// <summary>Gets the last state observed by the platform backend.</summary>
    PlatformApplicationLifecycleState State { get; }

    /// <summary>Occurs when the platform enters a different lifecycle state.</summary>
    event EventHandler<PlatformApplicationLifecycleChangedEventArgs>? StateChanged;
}
