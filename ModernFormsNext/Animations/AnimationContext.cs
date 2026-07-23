namespace ModernFormsNext.Animations;

/// <summary>
/// Describes the target and timing values for one custom-animation update.
/// </summary>
/// <remarks>
/// One context instance is reused for every frame in a run. The target is available only while an
/// update callback is executing; the framework clears it before completion becomes observable so
/// a retained context cannot keep a completed target alive. Updates run on the scheduler UI
/// dispatcher and must not block.
/// </remarks>
public sealed class AnimationContext
{
    private Control? target;

    internal AnimationContext(Control target, CancellationToken cancellationToken)
    {
        this.target = target;
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets the control targeted by the current animation update.</summary>
    /// <exception cref="ObjectDisposedException">The run has already released its target.</exception>
    public Control Target
        => target ?? throw new ObjectDisposedException(
            nameof(AnimationContext),
            "The completed animation context no longer retains its target.");

    /// <summary>Gets direction-aware linear progress in the inclusive range 0 through 1.</summary>
    public float Progress { get; private set; }

    /// <summary>Gets direction-aware eased progress. Overshoot curves may exceed 0 through 1.</summary>
    public float EasedProgress { get; private set; }

    /// <summary>Gets monotonic elapsed time within the current playback leg.</summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Gets the scaled duration of the current playback leg.</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>
    /// Gets a token signaled when the run is canceled explicitly, by replacement, or by owner
    /// lifetime cleanup.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    internal void SetFrame(AnimationFrame frame, bool reverse)
    {
        Progress = reverse ? 1f - frame.Progress : frame.Progress;
        EasedProgress = reverse ? 1f - frame.EasedProgress : frame.EasedProgress;
        Elapsed = frame.Elapsed;
        Duration = frame.Duration;
    }

    internal void ReleaseTarget() => target = null;
}
