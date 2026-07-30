using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.Tests;

internal sealed class AnimationSchedulerTestHarness : IDisposable
{
    public AnimationSchedulerTestHarness(
        IAnimationDispatcher? dispatcher = null,
        IPlatformApplicationLifecycle? lifecycle = null,
        IPlatformAnimationSettings? animationSettings = null,
        Func<bool>? isDesignMode = null)
    {
        Clock = new ManualAnimationClock();
        Dispatcher = dispatcher ?? new ImmediateAnimationDispatcher();
        TickSource = new ManualAnimationTickSource();
        Policy = new AnimationPolicy();
        Scheduler = new AnimationScheduler(
            Clock,
            Dispatcher,
            TickSource,
            Policy,
            lifecycle,
            animationSettings,
            isDesignMode);
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

internal sealed class TestPlatformAnimationSettings : IPlatformAnimationSettings
{
    private readonly object sync = new();
    private EventHandler<PlatformAnimationSettingsChangedEventArgs>? changed;
    private PlatformAnimationSettingsSnapshot current;
    private PlatformAnimationSettingsSnapshot? nextRefresh;

    public TestPlatformAnimationSettings(
        bool reducedMotion = false,
        bool animationsEnabled = true,
        PlatformAnimationProviderState providerState = PlatformAnimationProviderState.Ready,
        bool fallbackUsed = false,
        string? lastError = null)
    {
        current = CreateSnapshot(
            reducedMotion,
            animationsEnabled,
            providerState,
            fallbackUsed,
            lastError);
    }

    public PlatformAnimationSettingsSnapshot Current
    {
        get
        {
            lock (sync)
                return current;
        }
    }

    public int SubscriberCount { get; private set; }

    public int RefreshCount { get; private set; }

    public bool IsLockHeldByCurrentThread => Monitor.IsEntered(sync);

    public event EventHandler<PlatformAnimationSettingsChangedEventArgs>? Changed
    {
        add
        {
            lock (sync)
            {
                changed += value;
                SubscriberCount++;
            }
        }
        remove
        {
            lock (sync)
            {
                changed -= value;
                SubscriberCount--;
            }
        }
    }

    public PlatformAnimationSettingsSnapshot Refresh()
    {
        PlatformAnimationSettingsSnapshot? refresh;
        lock (sync)
        {
            RefreshCount++;
            refresh = nextRefresh;
            nextRefresh = null;
        }

        if (refresh is not null)
            Publish(refresh);
        return Current;
    }

    public void Set(
        bool reducedMotion,
        bool animationsEnabled = true,
        PlatformAnimationProviderState providerState = PlatformAnimationProviderState.Ready,
        bool fallbackUsed = false,
        string? lastError = null)
    {
        PlatformAnimationSettingsSnapshot next = CreateSnapshot(
            reducedMotion,
            animationsEnabled,
            providerState,
            fallbackUsed,
            lastError);
        Publish(next);
    }

    public void SetOnNextRefresh(
        bool reducedMotion,
        bool animationsEnabled = true,
        PlatformAnimationProviderState providerState = PlatformAnimationProviderState.Ready,
        bool fallbackUsed = false,
        string? lastError = null)
    {
        lock (sync)
        {
            nextRefresh = CreateSnapshot(
                reducedMotion,
                animationsEnabled,
                providerState,
                fallbackUsed,
                lastError);
        }
    }

    private void Publish(PlatformAnimationSettingsSnapshot next)
    {
        PlatformAnimationSettingsSnapshot previous;
        EventHandler<PlatformAnimationSettingsChangedEventArgs>? handlers;
        lock (sync)
        {
            previous = current;
            current = next;
            handlers = changed;
        }

        handlers?.Invoke(this, new PlatformAnimationSettingsChangedEventArgs(previous, next));
    }

    private static PlatformAnimationSettingsSnapshot CreateSnapshot(
        bool reducedMotion,
        bool animationsEnabled,
        PlatformAnimationProviderState providerState,
        bool fallbackUsed,
        string? lastError)
        => new(
            "Deterministic test provider",
            reducedMotion,
            animationsEnabled,
            DateTimeOffset.UnixEpoch,
            fallbackUsed,
            providerState,
            lastError);
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

internal sealed class BlockingStartAnimationTickSource : IAnimationTickSource
{
    private readonly ManualResetEventSlim releaseStart = new();
    private int isRunning;
    private int isDisposed;

    public ManualResetEventSlim StartEntered { get; } = new();

    public bool IsRunning => Volatile.Read(ref isRunning) != 0;

    public void Start(Action tickRequested)
    {
        ArgumentNullException.ThrowIfNull(tickRequested);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref isDisposed) != 0, this);
        StartEntered.Set();
        if (!releaseStart.Wait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The test did not release the blocked tick-source start.");
        Volatile.Write(ref isRunning, 1);
    }

    public void Stop() => Volatile.Write(ref isRunning, 0);

    public void ReleaseStart() => releaseStart.Set();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref isDisposed, 1) != 0)
            return;
        releaseStart.Set();
        Volatile.Write(ref isRunning, 0);
        StartEntered.Dispose();
        releaseStart.Dispose();
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
