using System;
using System.Diagnostics;
using System.Threading;
using ModernFormsNext.WindowKit.Metadata;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit.Controls.Platform;

/// <summary>
/// Provides a managed dispatcher implementation that can drive a dispatcher loop without a native message pump.
/// </summary>
/// <remarks>
/// Platform backends can use this implementation for tests, headless hosts, or platforms where
/// input is supplied through <see cref="IManagedDispatcherInputProvider"/> rather than a Win32-style
/// message loop. The dispatcher loop runs on the thread that creates this instance.
/// </remarks>
[Unstable]
public class ManagedDispatcherImpl : IControlledDispatcherImpl
{
    private readonly IManagedDispatcherInputProvider? _inputProvider;
    private readonly AutoResetEvent _wakeup = new(false);
    private bool _signaled;
    private readonly object _lock = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan? _nextTimer; 
    private readonly Thread _loopThread = Thread.CurrentThread;

    /// <summary>
    /// Supplies pending input events to <see cref="ManagedDispatcherImpl"/>.
    /// </summary>
    public interface IManagedDispatcherInputProvider
    {
        /// <summary>
        /// Gets a value indicating whether an input event is waiting to be dispatched.
        /// </summary>
        bool HasInput { get; }

        /// <summary>
        /// Dispatches the next queued input event.
        /// </summary>
        void DispatchNextInputEvent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedDispatcherImpl"/> class.
    /// </summary>
    /// <param name="inputProvider">
    /// The optional input provider used to integrate platform input with the dispatcher loop.
    /// </param>
    public ManagedDispatcherImpl(IManagedDispatcherInputProvider? inputProvider)
    {
        _inputProvider = inputProvider;
    }

    /// <inheritdoc />
    public bool CurrentThreadIsLoopThread => _loopThread == Thread.CurrentThread;

    /// <inheritdoc />
    public void Signal()
    {
        lock (_lock)
        {
            _signaled = true;
            _wakeup.Set();
        }
    }

    /// <inheritdoc />
    public event Action? Signaled;

    /// <inheritdoc />
    public event Action? Timer;

    /// <inheritdoc />
    public long Now => _clock.ElapsedMilliseconds;

    /// <inheritdoc />
    public void UpdateTimer(long? dueTimeInMs)
    {
        lock (_lock)
        {
            _nextTimer = dueTimeInMs == null
                ? null
                : TimeSpan.FromMilliseconds(dueTimeInMs.Value);
            if (!CurrentThreadIsLoopThread)
                _wakeup.Set();
        }
    }

    /// <inheritdoc />
    public bool CanQueryPendingInput => _inputProvider != null;

    /// <inheritdoc />
    public bool HasPendingInput => _inputProvider?.HasInput ?? false;
    
    /// <inheritdoc />
    public void RunLoop(CancellationToken token)
    {
        CancellationTokenRegistration registration = default;
        if (token.CanBeCanceled) 
            registration = token.Register(() => _wakeup.Set());

        while (!token.IsCancellationRequested)
        {
            bool signaled;
            lock (_lock)
            {
                signaled = _signaled;
                _signaled = false;
            }

            if (signaled)
            {
                Signaled?.Invoke();
                continue;
            }

            bool fireTimer = false;
            lock (_lock)
            {
                if (_nextTimer < _clock.Elapsed)
                {
                    fireTimer = true;
                    _nextTimer = null;
                }
            }

            if (fireTimer)
            {
                Timer?.Invoke();
                continue;
            }

            if (_inputProvider?.HasInput == true)
            {
                _inputProvider.DispatchNextInputEvent();
                continue;
            }

            TimeSpan? nextTimer;
            lock (_lock)
            {
                nextTimer = _nextTimer;
            }

            if (nextTimer != null)
            {
                var waitFor = nextTimer.Value - _clock.Elapsed;
                if (waitFor.TotalMilliseconds < 1)
                    continue;
                _wakeup.WaitOne(waitFor);
            }
            else
                _wakeup.WaitOne();
        }

        registration.Dispose();
    }
}
