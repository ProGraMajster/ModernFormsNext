namespace ModernFormsNext.Animations;

/// <summary>
/// Describes the lifecycle state of one scheduled animation.
/// </summary>
/// <remarks>
/// State can be read from any thread. Updates and terminal transitions are serialized by the
/// owning <see cref="AnimationScheduler"/>. Terminal states are <see cref="Completed"/>,
/// <see cref="Canceled"/>, and <see cref="Faulted"/>.
/// </remarks>
public enum AnimationState
{
    /// <summary>The animation has been created but has not begun producing values.</summary>
    Created,

    /// <summary>The animation is waiting for its configured start delay.</summary>
    Delayed,

    /// <summary>The animation is actively producing values on scheduler ticks.</summary>
    Running,

    /// <summary>The animation or its scheduler is paused and its elapsed time is frozen.</summary>
    Paused,

    /// <summary>The final value was applied successfully.</summary>
    Completed,

    /// <summary>The animation was canceled without applying a successful completion.</summary>
    Canceled,

    /// <summary>An easing, interpolation, or update callback threw an exception.</summary>
    Faulted
}
