using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.Tests;

internal sealed class AnimationSchedulerTestHarness : IDisposable
{
    public AnimationSchedulerTestHarness(
        IAnimationDispatcher? dispatcher = null,
        IPlatformApplicationLifecycle? lifecycle = null)
    {
        Clock = new ManualAnimationClock();
        Dispatcher = dispatcher ?? new ImmediateAnimationDispatcher();
        TickSource = new ManualAnimationTickSource();
        Policy = new AnimationPolicy();
        Scheduler = new AnimationScheduler(Clock, Dispatcher, TickSource, Policy, lifecycle);
    }

    public ManualAnimationClock Clock { get; }

    public IAnimationDispatcher Dispatcher { get; }

    public ManualAnimationTickSource TickSource { get; }

    public AnimationPolicy Policy { get; }

    public AnimationScheduler Scheduler { get; }

    public void AdvanceAndTick(TimeSpan elapsed)
    {
        Clock.Advance(elapsed);
        TickSource.Fire();
    }

    public void Dispose() => Scheduler.Dispose();
}

internal sealed class TestPlatformApplicationLifecycle : IPlatformApplicationLifecycle
{
    private EventHandler<PlatformApplicationLifecycleChangedEventArgs>? stateChanged;

    public TestPlatformApplicationLifecycle(PlatformApplicationLifecycleState initialState)
    {
        State = initialState;
    }

    public PlatformApplicationLifecycleState State { get; private set; }

    public int SubscriberCount { get; private set; }

    public event EventHandler<PlatformApplicationLifecycleChangedEventArgs>? StateChanged
    {
        add
        {
            stateChanged += value;
            SubscriberCount++;
        }
        remove
        {
            stateChanged -= value;
            SubscriberCount--;
        }
    }

    public void SetState(PlatformApplicationLifecycleState state)
    {
        PlatformApplicationLifecycleState previous = State;
        if (previous == state)
            return;

        State = state;
        stateChanged?.Invoke(this, new PlatformApplicationLifecycleChangedEventArgs(previous, state));
    }
}

internal sealed class ManualAnimationClock : IAnimationClock
{
    public TimeSpan CurrentTime { get; private set; }

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        CurrentTime += elapsed;
    }
}

internal sealed class ManualAnimationTickSource : IAnimationTickSource
{
    private Action? callback;

    public bool IsRunning { get; private set; }

    public bool IsDisposed { get; private set; }

    public int StartTransitions { get; private set; }

    public int StopTransitions { get; private set; }

    public int FireCount { get; private set; }

    public void Start(Action tickRequested)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        callback = tickRequested ?? throw new ArgumentNullException(nameof(tickRequested));
        if (IsRunning)
            return;

        IsRunning = true;
        StartTransitions++;
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        StopTransitions++;
    }

    public void Fire()
    {
        if (!IsRunning)
            return;

        FireCount++;
        callback?.Invoke();
    }

    public void Dispose()
    {
        Stop();
        IsDisposed = true;
        callback = null;
    }
}

internal sealed class ImmediateAnimationDispatcher : IAnimationDispatcher
{
    public int PostCount { get; private set; }

    public int ThreadId { get; } = Environment.CurrentManagedThreadId;

    public bool CheckAccess() => Environment.CurrentManagedThreadId == ThreadId;

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        PostCount++;
        action();
    }
}

internal sealed class QueuedAnimationDispatcher : IAnimationDispatcher
{
    private readonly Queue<Action> pending = new();

    public int ThreadId { get; private set; }

    public int PendingCount
    {
        get
        {
            lock (pending)
                return pending.Count;
        }
    }

    public bool CheckAccess() => ThreadId != 0 && Environment.CurrentManagedThreadId == ThreadId;

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (pending)
            pending.Enqueue(action);
    }

    public void Drain()
    {
        ThreadId = Environment.CurrentManagedThreadId;

        while (true)
        {
            Action? action;
            lock (pending)
                action = pending.Count > 0 ? pending.Dequeue() : null;
            if (action is null)
                return;
            action();
        }
    }
}
