using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.Testing;

/// <summary>Controls monotonic animation and dispatcher-timer time for a headless test host.</summary>
/// <remarks>
/// <para>
/// Obtain this clock from <see cref="ModernFormsTestHost.Clock"/>. It starts at zero and owns no
/// thread, timer, or wall-clock wait. Production controls and animation helpers keep using the real
/// <see cref="AnimationScheduler.Default"/> with this clock as its time source. One host may own
/// multiple windows; it owns one scheduler and one clock for all of them.
/// </para>
/// <para>
/// The host disposes this clock, cancels all its scheduler work, and restores the ordinary default
/// even when scheduler cleanup reports a failure. Do not retain the host scheduler after disposal.
/// Application code using unrelated wall clocks, Task.Delay, native timers, or another scheduler
/// is not controlled by this clock.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var host = ModernFormsTestHost.Create();
/// var button = new Button();
/// host.Show(button);
/// AnimationScheduler.Default.Start(button, "example", progress => button.Opacity = progress,
///     new AnimationOptions { Duration = TimeSpan.FromSeconds(1) });
/// host.Clock.Advance(TimeSpan.FromMilliseconds(500));
/// </code>
/// </example>
public sealed class TestClock
{
    private readonly UiTestDispatcher dispatcher;
    private readonly ManualTickSource tickSource = new();
    private readonly AnimationScheduler scheduler;
    private readonly IDisposable defaultScope;
    private long currentTicks;
    private bool advancing;
    private volatile bool disposed;

    internal TestClock(UiTestDispatcher dispatcher)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        dispatcher.VerifyAccess();
        scheduler = new AnimationScheduler(
            new ClockAdapter(this), new DispatcherAdapter(dispatcher), tickSource, new AnimationPolicy(),
            PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>(),
            PlatformServiceRegistry.GetService<IPlatformAnimationSettings>());
        try
        {
            defaultScope = AnimationScheduler.PushDefaultForTesting(scheduler);
        }
        catch
        {
            scheduler.Dispose();
            throw;
        }
    }

    /// <summary>Gets monotonic time elapsed since the host was created, initially zero.</summary>
    /// <remarks>This property may be read from any thread. It is not a wall-clock date or UTC time.</remarks>
    /// <exception cref="ObjectDisposedException">The owning host has been disposed.</exception>
    public TimeSpan CurrentTime
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return TimeSpan.FromTicks(Interlocked.Read(ref currentTicks));
        }
    }

    /// <summary>Advances monotonic time and processes one animation frame and ready dispatcher work.</summary>
    /// <param name="elapsed">The non-negative amount of time to add, including zero for an explicit frame.</param>
    /// <remarks>
    /// <para>
    /// Call on the host UI thread. Ready work is drained at the old time first. The new time is then
    /// committed, due production DispatcherTimer operations are promoted, one shared scheduler tick
    /// runs at that time, and ready work drains in production priority/FIFO order. Animation callbacks
    /// execute on the UI thread. Background intervals are excluded by the scheduler's normal lifecycle
    /// policy, while this clock itself remains monotonic.
    /// </para>
    /// <para>
    /// Framework-owned composition legs and theme completion bookkeeping proceed through their normal
    /// scheduler/dispatcher paths before this call returns. Public completion tasks still release
    /// application continuations asynchronously; advancing time does not join arbitrary application work.
    /// </para>
    /// <para>
    /// Advancing by a large interval simulates a delayed frame, not every intermediate frame. A periodic
    /// dispatcher timer ticks at the target time and schedules its next interval from there; missed
    /// intervals are not replayed. Dispatcher timer resolution is whole milliseconds. Each drain is
    /// bounded to 4096 operations; zero-interval/self-replenishing work fails instead of waiting forever.
    /// Advance cannot be called recursively from an animation, timer or dispatcher callback.
    /// </para>
    /// <para>
    /// Invalid/overflowing advances leave time and queued work unchanged. If a drain fails after time
    /// commits, time stays committed. Posted callback failures follow the dispatcher's captured-exception
    /// contract; animation callback failures follow the production scheduler's faulted-handle contract.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="elapsed"/> is negative.</exception>
    /// <exception cref="OverflowException">The resulting time exceeds <see cref="TimeSpan.MaxValue"/>.</exception>
    /// <exception cref="InvalidOperationException">The caller is not the UI thread, an advance is reentrant, or a drain exceeds its limit.</exception>
    /// <exception cref="ObjectDisposedException">The owning host has been disposed.</exception>
    public void Advance(TimeSpan elapsed)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        dispatcher.VerifyAccess();
        if (advancing)
            throw new InvalidOperationException("A deterministic clock advance cannot run recursively.");
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        long targetTicks = checked(Interlocked.Read(ref currentTicks) + elapsed.Ticks);

        advancing = true;
        try
        {
            dispatcher.Drain();
            ObjectDisposedException.ThrowIf(disposed, this);
            Interlocked.Exchange(ref currentTicks, targetTicks);
            dispatcher.SetCurrentTime(TimeSpan.FromTicks(targetTicks));
            tickSource.Fire();
            if (!disposed)
                dispatcher.Drain();
        }
        finally
        {
            advancing = false;
        }
    }

    internal void Dispose()
    {
        if (disposed)
            return;
        dispatcher.VerifyAccess();
        List<Exception> failures = [];
        try
        {
            TryCleanup(scheduler.Dispose, failures);
            TryCleanup(tickSource.Dispose, failures);
        }
        finally
        {
            TryCleanup(defaultScope.Dispose, failures);
            disposed = true;
        }

        if (failures.Count > 0)
            throw new AggregateException("The deterministic clock reported scheduler cleanup failures.", failures);
    }

    private static void TryCleanup(Action action, ICollection<Exception> failures)
    {
        try { action(); }
        catch (Exception exception) { failures.Add(exception); }
    }

    private sealed class ClockAdapter(TestClock owner) : IAnimationClock
    {
        public TimeSpan CurrentTime => TimeSpan.FromTicks(Interlocked.Read(ref owner.currentTicks));
    }

    private sealed class DispatcherAdapter(UiTestDispatcher owner) : IAnimationDispatcher
    {
        public bool CheckAccess() => owner.CheckAccess();
        public void Post(Action action) => owner.Post(action);
    }

    private sealed class ManualTickSource : IAnimationTickSource
    {
        private readonly object sync = new();
        private Action? callback;
        private bool disposed;

        public bool IsRunning { get { lock (sync) return callback is not null; } }

        public void Start(Action tickRequested)
        {
            ArgumentNullException.ThrowIfNull(tickRequested);
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                callback = tickRequested;
            }
        }

        public void Stop() { lock (sync) callback = null; }

        public void Fire()
        {
            Action? tick;
            lock (sync) tick = callback;
            tick?.Invoke();
        }

        public void Dispose()
        {
            lock (sync)
            {
                callback = null;
                disposed = true;
            }
        }
    }
}
