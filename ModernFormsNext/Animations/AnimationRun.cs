namespace ModernFormsNext.Animations;

/// <summary>
/// Controls and observes one animation-definition run, including composed child animations.
/// </summary>
/// <remarks>
/// Cancellation is propagated to active descendants and completion is published only after those
/// descendants have released scheduler handles. Retaining a terminal run does not retain its
/// animation targets.
/// </remarks>
public sealed class AnimationRun : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly object cancellationSync = new();
    private readonly TaskCompletionSource<AnimationState> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int state = (int)AnimationState.Created;
    private Exception? exception;
    private int started;
    private int finished;
    private bool cancellationDisposed;

    internal CancellationToken CancellationToken => cancellation.Token;

    /// <summary>Gets the current lifecycle state.</summary>
    public AnimationState State => (AnimationState)Volatile.Read(ref state);

    /// <summary>Gets the terminal completion task.</summary>
    public Task<AnimationState> Completion => completion.Task;

    /// <summary>Gets the fault or deterministic parallel-fault aggregate, if any.</summary>
    public Exception? Exception => Volatile.Read(ref exception);

    /// <summary>
    /// Requests cancellation of this run and every active descendant.
    /// </summary>
    /// <remarks>
    /// Cancellation is idempotent. The completion task is released after descendant cleanup, so
    /// awaiting it is sufficient before asserting that the shared scheduler returned to idle.
    /// </remarks>
    public void Cancel()
    {
        while (true)
        {
            AnimationState current = State;
            if (current is AnimationState.Completed or AnimationState.Canceled or AnimationState.Faulted)
                return;
            if (Interlocked.CompareExchange(
                    ref state,
                    (int)AnimationState.Canceled,
                    (int)current) == (int)current)
                break;
        }

        lock (cancellationSync)
        {
            if (cancellationDisposed)
                return;

            try
            {
                cancellation.Cancel(throwOnFirstException: false);
            }
            catch (AggregateException cancellationFailure)
            {
                System.Diagnostics.Trace.TraceError(
                    "An animation-run cancellation callback faulted: {0}",
                    cancellationFailure);
            }
        }
    }

    /// <summary>Cancels the run. Disposal is idempotent.</summary>
    public void Dispose() => Cancel();

    internal void Start(Func<CancellationToken, Task<AnimationExecutionResult>> execute, CancellationToken externalToken)
    {
        ArgumentNullException.ThrowIfNull(execute);
        if (Interlocked.Exchange(ref started, 1) != 0)
            throw new InvalidOperationException("An animation run can be started only once.");

        if (externalToken.IsCancellationRequested)
        {
            Volatile.Write(ref state, (int)AnimationState.Canceled);
            Finish(AnimationExecutionResult.Canceled);
            return;
        }

        Volatile.Write(ref state, (int)AnimationState.Running);
        _ = ExecuteAsync(execute, externalToken);
    }

    private async Task ExecuteAsync(
        Func<CancellationToken, Task<AnimationExecutionResult>> execute,
        CancellationToken externalToken)
    {
        using CancellationTokenRegistration registration =
            externalToken.CanBeCanceled ? externalToken.Register(Cancel) : default;

        AnimationExecutionResult result;
        try
        {
            result = await execute(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested || externalToken.IsCancellationRequested)
        {
            result = AnimationExecutionResult.Canceled;
        }
        catch (Exception fault)
        {
            result = AnimationExecutionResult.Faulted(fault);
        }

        Finish(result);
    }

    private void Finish(AnimationExecutionResult result)
    {
        if (Interlocked.Exchange(ref finished, 1) != 0)
            return;

        AnimationState finalState;
        while (true)
        {
            AnimationState current = State;
            if (current == AnimationState.Canceled)
            {
                finalState = AnimationState.Canceled;
                break;
            }
            if (current is AnimationState.Completed or AnimationState.Faulted)
            {
                finalState = current;
                break;
            }
            if (Interlocked.CompareExchange(ref state, (int)result.State, (int)current) == (int)current)
            {
                finalState = result.State;
                break;
            }
        }

        if (finalState == AnimationState.Faulted)
            Volatile.Write(ref exception, result.Exception);

        completion.TrySetResult(finalState);
        lock (cancellationSync)
        {
            cancellationDisposed = true;
            cancellation.Dispose();
        }
    }
}

internal readonly record struct AnimationExecutionResult(
    AnimationState State,
    Exception? Exception = null,
    bool WasIgnored = false)
{
    public static AnimationExecutionResult Completed { get; } = new(AnimationState.Completed);
    public static AnimationExecutionResult Ignored { get; } =
        new(AnimationState.Completed, WasIgnored: true);
    public static AnimationExecutionResult Canceled { get; } = new(AnimationState.Canceled);

    public static AnimationExecutionResult Faulted(Exception exception)
        => new(AnimationState.Faulted, exception ?? throw new ArgumentNullException(nameof(exception)));
}
