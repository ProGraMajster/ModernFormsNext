namespace ModernFormsNext.Animations;

internal sealed class AnimationEntry
{
    private readonly TaskCompletionSource<AnimationState> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int state = (int)AnimationState.Created;
    private Exception? exception;

    public required object Owner { get; init; }

    public required string Key { get; init; }

    public required AnimationOptionsSnapshot Options { get; init; }

    public required Action<float> Update { get; init; }

    public required AnimationScheduler Scheduler { get; init; }

    public required TimeSpan StartTime { get; set; }

    public TimeSpan IndividualPauseTime { get; set; }

    public AnimationState ResumeState { get; set; }

    public bool IsIndividuallyPaused { get; set; }

    public bool IsPausedByScheduler { get; set; }

    public AnimationHandle Handle { get; set; } = null!;

    public AnimationState State => (AnimationState)Volatile.Read(ref state);

    public Task<AnimationState> Completion => completion.Task;

    public Exception? Exception => Volatile.Read(ref exception);

    public bool IsTerminal => State is AnimationState.Completed or AnimationState.Canceled or AnimationState.Faulted;

    public void SetState(AnimationState value) => Volatile.Write(ref state, (int)value);

    public bool TrySetTerminal(AnimationState terminalState, Exception? fault = null)
    {
        while (true)
        {
            AnimationState current = State;
            if (current is AnimationState.Completed or AnimationState.Canceled or AnimationState.Faulted)
                return false;

            if (Interlocked.CompareExchange(ref state, (int)terminalState, (int)current) != (int)current)
                continue;

            if (fault is not null)
                Volatile.Write(ref exception, fault);
            completion.TrySetResult(terminalState);
            return true;
        }
    }
}
