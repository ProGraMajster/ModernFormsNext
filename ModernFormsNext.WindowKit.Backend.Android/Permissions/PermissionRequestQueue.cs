namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

/// <summary>
/// Serializes native permission dialogs while allowing an individual caller to cancel its wait.
/// </summary>
/// <remarks>
/// Android cannot cancel a dialog after it has been shown. Caller cancellation therefore completes
/// only that caller's task; the queue keeps the native operation alive and holds the gate until the
/// platform reports a result, lifecycle loss, or timeout.
/// </remarks>
internal sealed class PermissionRequestQueue
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task<T> EnqueueAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan operationTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operationTimeout <= TimeSpan.Zero && operationTimeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(operationTimeout));

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = ExecuteAsync(operation, operationTimeout, cancellationToken, completion);
        return completion.Task;
    }

    private async Task ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan operationTimeout,
        CancellationToken callerToken,
        TaskCompletionSource<T> completion)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (callerToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(callerToken);
                return;
            }

            using var registration = callerToken.Register(
                static state =>
                {
                    var tuple = ((TaskCompletionSource<T>, CancellationToken))state!;
                    tuple.Item1.TrySetCanceled(tuple.Item2);
                },
                (completion, callerToken));
            using var timeoutSource = operationTimeout == Timeout.InfiniteTimeSpan
                ? new CancellationTokenSource()
                : new CancellationTokenSource(operationTimeout);

            try
            {
                var result = await operation(timeoutSource.Token).ConfigureAwait(false);
                completion.TrySetResult(result);
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                completion.TrySetException(new TimeoutException(
                    "The Android permission request did not complete before its configured timeout."));
            }
            catch (OperationCanceledException exception)
            {
                completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
