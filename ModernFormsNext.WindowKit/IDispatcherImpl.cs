using System;
using System.Diagnostics;
using System.Threading;
using ModernFormsNext.WindowKit.Metadata;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Threading;

/// <summary>
/// Defines the backend contract used by <see cref="Dispatcher"/> to schedule work and timers.
/// </summary>
/// <remarks>
/// This is a platform-facing API. Implementations must signal the dispatcher thread without
/// blocking it and must report time through the same monotonic clock used for timer scheduling.
/// </remarks>
[PrivateApi]
public interface IDispatcherImpl
{
    /// <summary>
    /// Gets a value indicating whether the current thread owns the dispatcher loop.
    /// </summary>
    bool CurrentThreadIsLoopThread { get; }

    // Asynchronously triggers Signaled callback
    /// <summary>
    /// Asynchronously signals the dispatcher that queued work is available.
    /// </summary>
    void Signal();

    /// <summary>
    /// Raised after <see cref="Signal"/> wakes the dispatcher loop.
    /// </summary>
    event Action Signaled;

    /// <summary>
    /// Raised when the active dispatcher timer reaches its due time.
    /// </summary>
    event Action Timer;

    /// <summary>
    /// Gets the current dispatcher clock value in milliseconds.
    /// </summary>
    long Now { get; }

    /// <summary>
    /// Updates the dispatcher timer due time.
    /// </summary>
    /// <param name="dueTimeInMs">
    /// The absolute due time in milliseconds on the dispatcher clock, or <see langword="null"/>
    /// to clear the active timer.
    /// </param>
    void UpdateTimer(long? dueTimeInMs);
}

/// <summary>
/// Extends a dispatcher implementation with the ability to report pending input.
/// </summary>
[PrivateApi]
public interface IDispatcherImplWithPendingInput : IDispatcherImpl
{
    // Checks if dispatcher implementation can 
    /// <summary>
    /// Gets a value indicating whether pending input can be queried.
    /// </summary>
    bool CanQueryPendingInput { get; }
    // Checks if there is pending user input
    /// <summary>
    /// Gets a value indicating whether user input is waiting to be processed.
    /// </summary>
    bool HasPendingInput { get; }
}

/// <summary>
/// Extends a dispatcher implementation with explicit background-processing notifications.
/// </summary>
[PrivateApi]
public interface IDispatcherImplWithExplicitBackgroundProcessing : IDispatcherImpl
{
    /// <summary>
    /// Raised when the dispatcher is ready to process background-priority work.
    /// </summary>
    event Action ReadyForBackgroundProcessing;

    /// <summary>
    /// Requests a future background-processing notification.
    /// </summary>
    void RequestBackgroundProcessing();
}

/// <summary>
/// Extends a dispatcher implementation with a controllable event loop.
/// </summary>
[PrivateApi]
public interface IControlledDispatcherImpl : IDispatcherImplWithPendingInput
{
    // Runs the event loop
    /// <summary>
    /// Runs the dispatcher event loop until cancellation is requested.
    /// </summary>
    /// <param name="token">The cancellation token used to stop the loop.</param>
    void RunLoop(CancellationToken token);
}

internal class LegacyDispatcherImpl : IDispatcherImpl
{
    private readonly IPlatformThreadingInterface _platformThreading;
    private IDisposable? _timer;
    private Stopwatch _clock = Stopwatch.StartNew();

    public LegacyDispatcherImpl(IPlatformThreadingInterface platformThreading)
    {
        _platformThreading = platformThreading;
        _platformThreading.Signaled += delegate { Signaled?.Invoke(); };
    }

    public bool CurrentThreadIsLoopThread => _platformThreading.CurrentThreadIsLoopThread;
    public void Signal() => _platformThreading.Signal(DispatcherPriority.Send);

    public event Action? Signaled;
    public event Action? Timer;
    public long Now => _clock.ElapsedMilliseconds;
    public void UpdateTimer(long? dueTimeInMs)
    {
        _timer?.Dispose();
        _timer = null;

        if (dueTimeInMs.HasValue)
        {
            var interval = Math.Max(1, dueTimeInMs.Value - _clock.ElapsedMilliseconds);
            _timer = _platformThreading.StartTimer(DispatcherPriority.Send,
                TimeSpan.FromMilliseconds(interval),
                OnTick);
        }
    }

    private void OnTick()
    {
        _timer?.Dispose();
        _timer = null;
        Timer?.Invoke();
    }
}

class NullDispatcherImpl : IDispatcherImpl
{
    public bool CurrentThreadIsLoopThread => true;

    public void Signal()
    {
        
    }
    
    public event Action? Signaled;
    public event Action? Timer;

    public long Now => 0;

    public void UpdateTimer(long? dueTimeInMs)
    {
        
    }
}
