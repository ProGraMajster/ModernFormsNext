using Android.OS;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit.Backend.Android.Dispatching;

/// <summary>
/// Dispatches work through Android's main <see cref="Looper"/> and <see cref="Handler"/>.
/// </summary>
/// <remarks>
/// Calls made from the main thread execute inline for <c>InvokeAsync</c>, avoiding a self-wait
/// deadlock. Posted work is always asynchronous. Delegates must remain short and UI-focused.
/// </remarks>
public sealed class AndroidMainThreadDispatcher : IPlatformDispatcher
{
    private readonly Handler handler;

    /// <summary>
    /// Creates a dispatcher bound to Android's process main looper.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when Android has no main looper.</exception>
    public AndroidMainThreadDispatcher()
    {
        var mainLooper = Looper.MainLooper
            ?? throw new InvalidOperationException("Android did not provide a main Looper.");
        handler = new Handler(mainLooper);
    }

    /// <inheritdoc/>
    public bool CheckAccess() => ReferenceEquals(Looper.MyLooper(), Looper.MainLooper);

    /// <inheritdoc/>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!handler.Post(action))
            throw new InvalidOperationException("Android rejected work posted to the main Looper.");
    }

    /// <inheritdoc/>
    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return InvokeAsync(
            () =>
            {
                action();
                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        if (CheckAccess())
        {
            try
            {
                return Task.FromResult(function());
            }
            catch (Exception exception)
            {
                return Task.FromException<T>(exception);
            }
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(
            static state =>
            {
                var tuple = ((TaskCompletionSource<T>, CancellationToken))state!;
                tuple.Item1.TrySetCanceled(tuple.Item2);
            },
            (completion, cancellationToken));

        try
        {
            Post(() =>
            {
                try
                {
                    if (!completion.Task.IsCompleted)
                        completion.TrySetResult(function());
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    registration.Dispose();
                }
            });
        }
        catch
        {
            registration.Dispose();
            throw;
        }

        return completion.Task;
    }

}
