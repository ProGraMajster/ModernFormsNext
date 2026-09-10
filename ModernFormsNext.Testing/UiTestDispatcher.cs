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

    /// <summary>Gets the number of queued dispatcher operations, including dormant timer operations.</summary>
    /// <remarks>A future timer is queued at inactive priority and cannot run until the host clock advances.</remarks>
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

    /// <summary>Executes ready dispatcher operations in deterministic order without advancing time.</summary>
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
        while (dispatcher.HasReadyJobsForTesting)
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

        // Production DispatcherTimer queues its next tick at inactive priority. Reaching such an
        // operation means ready work is idle; it must not be executed early or treated as a failure.
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
        TryCleanup(StopTimers, failures);
        TryCleanup(() => Drain(), failures);
        // Cleanup callbacks may have created new dormant timers during the final drain. Stop those
        // as well, including after a bounded-drain failure, before restoring the prior dispatcher.
        TryCleanup(StopTimers, failures);
        TryCleanup(() => dispatcherScope.Dispose(), failures);
        TryCleanup(() => implementation.Dispose(), failures);
        disposed = true;

        if (failures.Count > 0)
            throw new AggregateException("The deterministic UI dispatcher reported cleanup failures.", failures);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private static void StopTimers()
    {
        // Future timer ticks belong to this host's dispatcher and must not remain armed on a
        // caller-retained timer after scope restoration. Stopping uses production cancellation.
        foreach (DispatcherTimer timer in Dispatcher.SnapshotTimersForUnitTests())
            timer.Stop();
    }

    internal bool HasReadyWork
    {
        get { ThrowIfDisposed(); return dispatcher.HasReadyJobsForTesting; }
    }

    internal void SetCurrentTime(TimeSpan currentTime)
    {
        VerifyAccess();
        implementation.CurrentTime = currentTime;
        // Promote through the actual dispatcher timer implementation, not through a separate test
        // timer queue. Bypass the OS wake path because its background-processing branch can run an
        // unbounded queue; Drain remains the single bounded execution boundary for headless work.
        dispatcher.PromoteTimers();
    }

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
        private long currentTicks;

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

        public TimeSpan CurrentTime
        {
            get => TimeSpan.FromTicks(Interlocked.Read(ref currentTicks));
            set => Interlocked.Exchange(ref currentTicks, value.Ticks);
        }

        public long Now => Interlocked.Read(ref currentTicks) / TimeSpan.TicksPerMillisecond;

        public void Signal()
        {
            // Work is deliberately executed only by UiTestDispatcher.Drain().
        }

        public void UpdateTimer(long? dueTimeInMs)
        {
            // There is no native wake source. TestClock explicitly promotes production timers at
            // the committed monotonic time; an ordinary Drain never advances the clock.
        }

        public void Dispose()
        {
        }
    }
}
