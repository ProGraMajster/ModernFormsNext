namespace ModernFormsNext.Animations;

/// <summary>
/// Controls and observes one animation scheduled by <see cref="AnimationScheduler"/>.
/// </summary>
/// <remarks>
/// Cancellation, pause, resume, state reads, and disposal are thread-safe. The
/// <see cref="Completion"/> task represents terminal completion and returns the terminal state;
/// cancellation is therefore distinguishable from successful completion without throwing. UI
/// update callbacks still execute exclusively on the scheduler's UI dispatcher.
/// </remarks>
public sealed class AnimationHandle : IDisposable
{
    private readonly AnimationScheduler scheduler;
    private readonly AnimationEntry entry;

    internal AnimationHandle(AnimationScheduler scheduler, AnimationEntry entry)
    {
        this.scheduler = scheduler;
        this.entry = entry;
    }

    /// <summary>Gets the current lifecycle state. This property is safe to read from any thread.</summary>
    public AnimationState State => entry.State;

    /// <summary>
    /// Gets a task that completes once with <see cref="AnimationState.Completed"/>,
    /// <see cref="AnimationState.Canceled"/>, or <see cref="AnimationState.Faulted"/>.
    /// </summary>
    public Task<AnimationState> Completion => entry.Completion;

    /// <summary>
    /// Gets the exception that faulted the animation, or <see langword="null"/> otherwise.
    /// </summary>
    public Exception? Exception => entry.Exception;

    /// <summary>
    /// Cancels the animation without applying its final value.
    /// </summary>
    /// <remarks>Calling this method repeatedly or after a terminal transition has no effect.</remarks>
    public void Cancel() => scheduler.Cancel(entry);

    /// <summary>
    /// Pauses this animation while allowing other animations to continue.
    /// </summary>
    /// <remarks>
    /// Paused time is excluded from progress. Calling this method on a paused or terminal handle
    /// has no effect.
    /// </remarks>
    public void Pause() => scheduler.Pause(entry);

    /// <summary>
    /// Resumes this animation from the progress at which it was paused.
    /// </summary>
    /// <remarks>Calling this method on a running or terminal handle has no effect.</remarks>
    public void Resume() => scheduler.Resume(entry);

    /// <summary>Cancels the animation. Disposal is idempotent and does not dispose the scheduler.</summary>
    public void Dispose() => Cancel();
}
