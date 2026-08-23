namespace ModernFormsNext.Designer.Services;

/// <summary>
/// Schedules single callbacks against an observable UTC clock.
/// </summary>
/// <remarks>
/// The abstraction is intentionally internal and one-shot. Autosave owns debounce and periodic
/// policy; the scheduler only supplies deterministic delay and cancellation semantics.
/// </remarks>
internal interface IDesignerOneShotScheduler
{
    DateTimeOffset UtcNow { get; }

    IDesignerScheduledHandle Schedule(TimeSpan delay, Action callback);
}

/// <summary>
/// Represents a pending one-shot Designer callback.
/// </summary>
internal interface IDesignerScheduledHandle : IDisposable
{
    /// <summary>
    /// Cancels the callback if it has not started.
    /// </summary>
    /// <returns><see langword="true"/> when this call cancelled the pending callback.</returns>
    bool Cancel();
}

/// <summary>
/// Uses <see cref="System.Threading.Timer"/> for production one-shot scheduling.
/// </summary>
internal sealed class SystemDesignerOneShotScheduler : IDesignerOneShotScheduler
{
    public static SystemDesignerOneShotScheduler Instance { get; } = new();

    private SystemDesignerOneShotScheduler()
    {
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public IDesignerScheduledHandle Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), "Designer callback delay cannot be negative.");

        return new TimerScheduledHandle(delay, callback);
    }

    private sealed class TimerScheduledHandle : IDesignerScheduledHandle
    {
        private const int Pending = 0;
        private const int Completed = 1;
        private const int Cancelled = 2;

        private Action? callback;
        private System.Threading.Timer? timer;
        private int state;

        public TimerScheduledHandle(TimeSpan delay, Action callback)
        {
            this.callback = callback;
            timer = new System.Threading.Timer(
                static scheduled => ((TimerScheduledHandle)scheduled!).Invoke(),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);

            try
            {
                timer.Change(delay, Timeout.InfiniteTimeSpan);
            }
            catch
            {
                Interlocked.Exchange(ref state, Cancelled);
                Interlocked.Exchange(ref this.callback, null);
                Interlocked.Exchange(ref timer, null)?.Dispose();
                throw;
            }
        }

        public bool Cancel()
        {
            if (Interlocked.CompareExchange(ref state, Cancelled, Pending) != Pending)
                return false;

            Interlocked.Exchange(ref callback, null);
            Interlocked.Exchange(ref timer, null)?.Dispose();
            return true;
        }

        public void Dispose()
            => Cancel();

        private void Invoke()
        {
            if (Interlocked.CompareExchange(ref state, Completed, Pending) != Pending)
                return;

            var action = Interlocked.Exchange(ref callback, null);
            Interlocked.Exchange(ref timer, null)?.Dispose();
            action?.Invoke();
        }
    }
}

/// <summary>
/// Marshals Designer model and UI notifications to the ModernFormsNext dispatcher.
/// </summary>
internal interface IDesignerUiDispatcher
{
    void Post(Action callback);
}

/// <summary>
/// Runs persistence work away from the Designer UI thread.
/// </summary>
/// <remarks>
/// Keeping this boundary internal permits deterministic ordering tests for write-gate and stale
/// completion races without exposing task scheduling as framework API.
/// </remarks>
internal interface IDesignerBackgroundWorkQueue
{
    Task<T> Run<T>(Func<T> callback);
}

/// <summary>
/// Uses the default .NET thread pool for Designer persistence work.
/// </summary>
internal sealed class DesignerBackgroundWorkQueue : IDesignerBackgroundWorkQueue
{
    public static DesignerBackgroundWorkQueue Instance { get; } = new();

    private DesignerBackgroundWorkQueue()
    {
    }

    public Task<T> Run<T>(Func<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return Task.Run(callback);
    }
}

/// <summary>
/// Production Designer dispatcher backed by <see cref="Application.RunOnUIThread(Action)"/>.
/// </summary>
internal sealed class DesignerUiDispatcher : IDesignerUiDispatcher
{
    public static DesignerUiDispatcher Instance { get; } = new();

    private DesignerUiDispatcher()
    {
    }

    public void Post(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        Application.RunOnUIThread(callback);
    }
}
