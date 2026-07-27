namespace ModernFormsNext.Animations;

internal sealed class AnimationEntry
{
    private readonly TaskCompletionSource<AnimationState> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource cancellation = new();
    private int state = (int)AnimationState.Created;
    private Exception? exception;
    private object? owner;
    private Action<AnimationFrame>? update;
    private Func<float, float>? easing;
    private AnimationOptionsSnapshot options;

    public required object Owner
    {
        get => Volatile.Read(ref owner)
            ?? throw new InvalidOperationException("A terminal animation no longer has an owner.");
        init => owner = value;
    }

    public object? OwnerOrNull => Volatile.Read(ref owner);

    public required string Key { get; init; }

    public required AnimationOptionsSnapshot Options
    {
        get => options;
        init
        {
            // Keep scalar option data on the handle-facing entry, but retain a caller-provided
            // easing delegate only while the animation can still invoke it. A terminal handle may
            // legitimately live much longer than its callback target.
            options = value with { Easing = Easings.Linear };
            easing = value.Easing;
        }
    }

    public required Action<AnimationFrame> Update
    {
        get => Volatile.Read(ref update)
            ?? throw new InvalidOperationException("A terminal animation no longer has an update callback.");
        init => update = value;
    }

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

    public CancellationToken CancellationToken => cancellation.Token;

    public bool IsTerminal => State is AnimationState.Completed or AnimationState.Canceled or AnimationState.Faulted;

    public void SetState(AnimationState value) => Volatile.Write(ref state, (int)value);

    public void Invoke(AnimationFrame frame)
        => Volatile.Read(ref update)?.Invoke(frame);

    public float ApplyEasing(float progress)
        => Volatile.Read(ref easing)?.Invoke(progress) ?? progress;

    public bool TryBeginTerminal(AnimationState terminalState, Exception? fault = null)
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
            return true;
        }
    }

    public void FinishTerminal(bool signalCancellation)
    {
        AnimationState terminalState = State;
        if (terminalState is not (AnimationState.Completed or AnimationState.Canceled or AnimationState.Faulted))
            throw new InvalidOperationException("Only a terminal animation can release its retained references.");

        if (signalCancellation)
        {
            try
            {
                cancellation.Cancel(throwOnFirstException: false);
            }
            catch (AggregateException exception)
            {
                System.Diagnostics.Trace.TraceError(
                    "An animation cancellation callback faulted after terminal transition: {0}",
                    exception);
            }
        }

        Volatile.Write(ref update, null);
        Volatile.Write(ref easing, null);
        Volatile.Write(ref owner, null);
        completion.TrySetResult(terminalState);
        cancellation.Dispose();
    }
}
