using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.Testing;

/// <summary>
/// Provides deterministic, explicitly drained UI-dispatcher execution for a headless test host.
/// </summary>
/// <remarks>
/// The dispatcher owns no worker thread and never waits on wall-clock time. The thread that creates
/// the host is the UI thread. Posted work runs in FIFO/dispatcher-priority order only when
/// <see cref="Drain"/> or another host operation that drains pending work is called.
/// </remarks>
public sealed class UiTestDispatcher
{
    private const int DefaultDrainLimit = 4096;
    private readonly DeterministicDispatcherImpl implementation;
    private readonly Dispatcher dispatcher;
    private readonly IDisposable dispatcherScope;
    private readonly List<Exception> unhandledExceptions = [];
    private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
    private bool disposed;

    internal UiTestDispatcher()
    {
        implementation = new DeterministicDispatcherImpl(ownerThreadId);
        dispatcherScope = Dispatcher.PushUIThreadForTesting(implementation);
        dispatcher = Dispatcher.UIThread;
    }

    /// <summary>Gets whether the calling thread is the deterministic UI thread.</summary>
    public bool CheckAccess()
    {
        ThrowIfDisposed();
        return Environment.CurrentManagedThreadId == ownerThreadId;
    }

    /// <summary>Gets the number of dispatcher operations waiting for an explicit drain.</summary>
    public int PendingWorkCount
    {
        get
        {
            ThrowIfDisposed();
            return dispatcher.PendingJobCountForTesting;
        }
    }

    /// <summary>Gets a detached snapshot of exceptions raised by fire-and-forget posted work.</summary>
    public IReadOnlyList<Exception> UnhandledExceptions
    {
        get
        {
            ThrowIfDisposed();
            return unhandledExceptions.ToArray();
        }
    }

    /// <summary>Runs an action immediately on the owning UI thread.</summary>
    /// <param name="action">The UI work to execute.</param>
    /// <exception cref="InvalidOperationException">The caller is not the owning thread.</exception>
    public void Run(Action action)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(action);
        VerifyAccess();
        action();
    }

    /// <summary>Runs a function immediately on the owning UI thread and returns its result.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="function">The UI work to execute.</param>
    /// <returns>The function result.</returns>
    public T Run<T>(Func<T> function)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(function);
        VerifyAccess();
        return function();
    }

    /// <summary>Invokes an action synchronously on the owning UI thread.</summary>
    /// <param name="action">The UI work to execute.</param>
    public void Invoke(Action action) => Run(action);

    /// <summary>Posts fire-and-forget work to the production ModernFormsNext dispatcher queue.</summary>
    /// <param name="action">The work to queue.</param>
    /// <remarks>Exceptions are captured by <see cref="UnhandledExceptions"/> when the queue drains.</remarks>
    public void Post(Action action)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(action);
        dispatcher.Post(action);
    }

    /// <summary>Queues work and returns a task completed when an explicit drain executes it.</summary>
    /// <param name="action">The work to queue.</param>
    /// <returns>A task representing the queued operation.</returns>
    public Task InvokeAsync(Action action)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }

    /// <summary>Executes queued dispatcher operations in deterministic order.</summary>
    /// <param name="maximumOperations">The maximum work items allowed in this drain.</param>
    /// <returns>The number of operations processed.</returns>
    /// <exception cref="InvalidOperationException">
    /// The caller is not the owning thread, or queued work keeps replenishing the queue beyond the
    /// specified limit.
    /// </exception>
    public int Drain(int maximumOperations = DefaultDrainLimit)
    {
        ThrowIfDisposed();
        VerifyAccess();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOperations);

        var processed = 0;
        while (dispatcher.PendingJobCountForTesting > 0)
        {
            if (processed >= maximumOperations)
            {
                throw new InvalidOperationException(
                    $"The deterministic UI dispatcher exceeded {maximumOperations} operations and still has " +
                    $"{dispatcher.PendingJobCountForTesting} pending item(s). The queue may be replenishing itself indefinitely.");
            }

            try
            {
                if (!dispatcher.RunOneJobForTesting())
                    break;
            }
            catch (Exception exception)
            {
                unhandledExceptions.Add(exception);
            }
            finally
            {
                processed++;
            }
        }

        if (dispatcher.PendingJobCountForTesting > 0)
        {
            throw new InvalidOperationException(
                $"The deterministic UI dispatcher could not execute {dispatcher.PendingJobCountForTesting} pending item(s).");
        }

        return processed;
    }

    /// <summary>Drains the queue synchronously and returns a completed awaitable.</summary>
    /// <returns>A completed task after the dispatcher is idle.</returns>
    public Task WaitForIdleAsync()
    {
        Drain();
        return Task.CompletedTask;
    }

    /// <summary>Throws all captured fire-and-forget dispatcher failures as one aggregate.</summary>
    public void ThrowUnhandledExceptions()
    {
        ThrowIfDisposed();
        if (unhandledExceptions.Count > 0)
            throw new AggregateException("One or more deterministic UI dispatcher operations failed.", unhandledExceptions);
    }

    /// <summary>Verifies that the caller owns this deterministic UI dispatcher.</summary>
    /// <exception cref="InvalidOperationException">The caller is not the owning thread.</exception>
    public void VerifyAccess()
    {
        ThrowIfDisposed();
        if (!CheckAccess())
            throw new InvalidOperationException("Headless ModernFormsNext UI work must run on the thread that created the test host.");
    }

    internal void Dispose()
    {
        if (disposed)
            return;

        VerifyAccess();
        var failures = new List<Exception>();
        TryCleanup(() => Drain(), failures);
        TryCleanup(() => dispatcherScope.Dispose(), failures);
        TryCleanup(() => implementation.Dispose(), failures);
        disposed = true;

        if (failures.Count > 0)
            throw new AggregateException("The deterministic UI dispatcher reported cleanup failures.", failures);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private static void TryCleanup(Action action, ICollection<Exception> failures)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private sealed class DeterministicDispatcherImpl(int ownerThreadId) : IDispatcherImpl, IDisposable
    {
        public bool CurrentThreadIsLoopThread => Environment.CurrentManagedThreadId == ownerThreadId;

        public event Action? Signaled
        {
            add { }
            remove { }
        }

        public event Action? Timer
        {
            add { }
            remove { }
        }

        public long Now => 0;

        public void Signal()
        {
            // Work is deliberately executed only by UiTestDispatcher.Drain().
        }

        public void UpdateTimer(long? dueTimeInMs)
        {
            // Phase 1 has no wall clock or TestClock. Dispatcher timers remain dormant.
        }

        public void Dispose()
        {
        }
    }
}
