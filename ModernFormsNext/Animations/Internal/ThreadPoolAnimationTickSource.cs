namespace ModernFormsNext.Animations;

internal sealed class ThreadPoolAnimationTickSource : IAnimationTickSource
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(16);
    private readonly object sync = new();
    private readonly TimeSpan interval;
    private System.Threading.Timer? timer;
    private Action? tickRequested;
    private bool isRunning;
    private bool disposed;

    public ThreadPoolAnimationTickSource()
        : this(DefaultInterval)
    {
    }

    internal ThreadPoolAnimationTickSource(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Tick interval must be positive.");
        this.interval = interval;
    }

    public bool IsRunning
    {
        get
        {
            lock (sync)
                return isRunning;
        }
    }

    public void Start(Action tickRequested)
    {
        ArgumentNullException.ThrowIfNull(tickRequested);

        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            this.tickRequested = tickRequested;
            if (isRunning)
                return;

            timer ??= new System.Threading.Timer(static state => ((ThreadPoolAnimationTickSource)state!).RequestTick(), this,
                Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            isRunning = true;
            timer.Change(TimeSpan.Zero, interval);
        }
    }

    public void Stop()
    {
        lock (sync)
        {
            if (!isRunning)
                return;

            isRunning = false;
            tickRequested = null;
            timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;

            disposed = true;
            isRunning = false;
            tickRequested = null;
            timer?.Dispose();
            timer = null;
        }
    }

    private void RequestTick()
    {
        Action? callback;
        lock (sync)
            callback = isRunning ? tickRequested : null;
        callback?.Invoke();
    }
}
