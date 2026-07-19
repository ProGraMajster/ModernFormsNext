using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ModernFormsNext.Animations;

/// <summary>
/// Schedules UI animations against one monotonic clock and one idle-aware tick source.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="Default"/> for application animations. Start, cancel, pause, resume, diagnostics,
/// and shutdown are thread-safe. Easing, interpolation, update callbacks, and final-value updates
/// run on the UI thread through the existing Windows or Android dispatcher.
/// </para>
/// <para>
/// Animations are identified by owner reference and ordinal key. The default replacement behavior
/// cancels the previous animation in that channel. Dispose controls and custom owners, or cancel
/// their handles, to bound callback lifetimes. The scheduler itself owns no platform rendering
/// resources.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// AnimationHandle handle = AnimationScheduler.Default.Animate(
///     owner: panel,
///     key: "Opacity",
///     from: panel.Opacity,
///     to: 1f,
///     interpolator: AnimationInterpolators.Float,
///     update: value => panel.Opacity = value,
///     options: new AnimationOptions
///     {
///         Duration = TimeSpan.FromMilliseconds(200),
///         Easing = Easings.EaseOut
///     });
/// </code>
/// </example>
public sealed partial class AnimationScheduler : IDisposable
{
    private static readonly Lazy<AnimationScheduler> DefaultInstance =
        new(CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly object sync = new();
    private readonly IAnimationClock clock;
    private readonly IAnimationDispatcher dispatcher;
    private readonly IAnimationTickSource tickSource;
    private readonly List<AnimationEntry> activeAnimations = [];
    private readonly List<AnimationEntry> tickBuffer = [];
    private readonly Dictionary<AnimationIdentity, AnimationEntry> keyedAnimations =
        new(AnimationIdentityComparer.Instance);
    private TimeSpan totalPausedTime;
    private TimeSpan schedulerPauseStarted;
    private bool isPaused;
    private bool isShutdown;
    private int tickPosted;
    private long tickCount;
    private long completedCount;
    private long canceledCount;
    private long faultedCount;
    private long totalTickTimestampDelta;

    private AnimationScheduler()
        : this(new StopwatchAnimationClock(), new DefaultAnimationDispatcher(), new ThreadPoolAnimationTickSource(), new AnimationPolicy())
    {
    }

    internal AnimationScheduler(
        IAnimationClock clock,
        IAnimationDispatcher dispatcher,
        IAnimationTickSource tickSource,
        AnimationPolicy policy)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.tickSource = tickSource ?? throw new ArgumentNullException(nameof(tickSource));
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        Policy.Changed += HandlePolicyChanged;
    }

    /// <summary>
    /// Gets the process-wide scheduler used by ModernFormsNext controls and shared backends.
    /// </summary>
    public static AnimationScheduler Default => DefaultInstance.Value;

    /// <summary>
    /// Gets the central reduced-motion and duration policy for this scheduler.
    /// </summary>
    public AnimationPolicy Policy { get; }

    /// <summary>
    /// Starts a progress animation whose callback receives eased progress.
    /// </summary>
    /// <param name="owner">The non-null lifetime owner used with <paramref name="key"/>.</param>
    /// <param name="key">The non-empty ordinal channel key.</param>
    /// <param name="update">The UI-thread callback receiving finite eased progress.</param>
    /// <param name="options">Optional settings copied at start.</param>
    /// <returns>A handle for state, cancellation, pause/resume, and completion.</returns>
    /// <remarks>
    /// This method is safe to call from a background thread. The callback is never invoked under
    /// the scheduler lock. Render or layout invalidation remains the responsibility of the
    /// property changed by <paramref name="update"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="update"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">The scheduler has been shut down.</exception>
    public AnimationHandle Start(
        object owner,
        string key,
        Action<float> update,
        AnimationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(update);

        BindPlatformLifecycleIfAvailable();
        AnimationOptionsSnapshot snapshot = (options ?? new AnimationOptions()).CreateSnapshot(Policy);
        if (owner is IComponent { Site.DesignMode: true })
            snapshot = snapshot with { CompleteImmediately = true };

        AnimationEntry? replaced = null;
        AnimationEntry entry;
        bool shouldStartTickSource = false;
        bool shouldCompleteImmediately;

        lock (sync)
        {
            ObjectDisposedException.ThrowIf(isShutdown, this);
            var identity = new AnimationIdentity(owner, key);
            if (keyedAnimations.TryGetValue(identity, out AnimationEntry? existing))
            {
                if (snapshot.ReplacementMode == AnimationReplacementMode.IgnoreNew)
                    return existing.Handle;

                RemoveEntryLocked(existing);
                if (existing.TrySetTerminal(AnimationState.Canceled))
                {
                    canceledCount++;
                    replaced = existing;
                }
            }

            TimeSpan effectiveNow = GetEffectiveTimeLocked(clock.CurrentTime);
            entry = new AnimationEntry
            {
                Owner = owner,
                Key = key,
                Options = snapshot,
                Update = update,
                Scheduler = this,
                StartTime = effectiveNow
            };
            entry.Handle = new AnimationHandle(this, entry);
            shouldCompleteImmediately = snapshot.CompleteImmediately || snapshot.Duration == TimeSpan.Zero;

            if (!shouldCompleteImmediately)
            {
                AnimationState initialState = snapshot.Delay > TimeSpan.Zero
                    ? AnimationState.Delayed
                    : AnimationState.Running;
                if (isPaused)
                {
                    entry.ResumeState = initialState;
                    entry.IsPausedByScheduler = true;
                    entry.SetState(AnimationState.Paused);
                }
                else
                {
                    entry.SetState(initialState);
                }

                activeAnimations.Add(entry);
                keyedAnimations.Add(identity, entry);
                shouldStartTickSource = !isPaused && HasRunnableAnimationsLocked();
            }
            else
            {
                entry.SetState(AnimationState.Running);
            }
        }

        if (replaced is not null)
            StopTickSourceIfIdle();

        if (shouldCompleteImmediately)
            PostImmediateCompletion(entry);
        else if (shouldStartTickSource)
            tickSource.Start(RequestTick);

        return entry.Handle;
    }

    /// <summary>
    /// Starts a typed animation using an explicit interpolator.
    /// </summary>
    /// <typeparam name="T">The platform-neutral value type.</typeparam>
    /// <param name="owner">The lifetime owner used for replacement and cancellation.</param>
    /// <param name="key">The owner-local animation channel.</param>
    /// <param name="from">The captured start value.</param>
    /// <param name="to">The target value.</param>
    /// <param name="interpolator">The stateless or animation-local value interpolator.</param>
    /// <param name="update">The UI-thread callback that applies the interpolated value.</param>
    /// <param name="options">Optional duration, delay, easing, and replacement settings.</param>
    /// <returns>A handle controlling the animation.</returns>
    /// <remarks>
    /// The interpolator runs on the UI thread and must remain fast. Exceptions fault this animation
    /// only. Use a property setter that performs the correct render or layout invalidation.
    /// </remarks>
    public AnimationHandle Animate<T>(
        object owner,
        string key,
        T from,
        T to,
        IAnimationInterpolator<T> interpolator,
        Action<T> update,
        AnimationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(interpolator);
        ArgumentNullException.ThrowIfNull(update);
        return Start(owner, key, progress => update(interpolator.Interpolate(from, to, progress)), options);
    }

    /// <summary>Cancels the active animation with a matching owner and key, if present.</summary>
    /// <param name="owner">The owner reference used at start.</param>
    /// <param name="key">The ordinal channel key.</param>
    public void Cancel(object owner, string key)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        AnimationEntry? entry;
        lock (sync)
            keyedAnimations.TryGetValue(new AnimationIdentity(owner, key), out entry);
        if (entry is not null)
            Cancel(entry);
    }

    /// <summary>Cancels every animation owned by the specified object.</summary>
    /// <param name="owner">The exact owner reference.</param>
    public void CancelAll(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        List<AnimationEntry>? canceled = null;
        lock (sync)
        {
            for (int index = activeAnimations.Count - 1; index >= 0; index--)
            {
                AnimationEntry entry = activeAnimations[index];
                if (!ReferenceEquals(entry.Owner, owner))
                    continue;

                RemoveEntryLocked(entry);
                if (entry.TrySetTerminal(AnimationState.Canceled))
                {
                    canceledCount++;
                    (canceled ??= []).Add(entry);
                }
            }
        }

        if (canceled is not null)
            StopTickSourceIfIdle();
    }

    /// <summary>
    /// Pauses monotonic scheduler time and stops the tick source while retaining active entries.
    /// </summary>
    /// <remarks>
    /// This operation is thread-safe and idempotent. It is used when Android enters the background.
    /// Resuming excludes the paused interval so animations do not jump forward.
    /// </remarks>
    public void Pause()
    {
        lock (sync)
        {
            if (isPaused || isShutdown)
                return;

            isPaused = true;
            schedulerPauseStarted = clock.CurrentTime;
            foreach (AnimationEntry entry in activeAnimations)
            {
                if (entry.State == AnimationState.Paused)
                    continue;
                entry.ResumeState = entry.State;
                entry.IsPausedByScheduler = true;
                entry.SetState(AnimationState.Paused);
            }
        }

        tickSource.Stop();
    }

    /// <summary>Resumes globally paused time without including the background interval.</summary>
    public void Resume()
    {
        bool shouldStart;
        lock (sync)
        {
            if (!isPaused || isShutdown)
                return;

            TimeSpan now = clock.CurrentTime;
            totalPausedTime += now - schedulerPauseStarted;
            isPaused = false;
            foreach (AnimationEntry entry in activeAnimations)
            {
                if (!entry.IsPausedByScheduler)
                    continue;
                entry.IsPausedByScheduler = false;
                if (!entry.IsIndividuallyPaused)
                    entry.SetState(entry.ResumeState);
            }

            shouldStart = HasRunnableAnimationsLocked();
        }

        if (shouldStart)
            tickSource.Start(RequestTick);
    }

    /// <summary>Returns a thread-safe snapshot of active counts, outcomes, and tick timing.</summary>
    public AnimationSchedulerDiagnostics GetDiagnostics()
    {
        int activeCount;
        bool paused;
        bool shutdown;
        lock (sync)
        {
            activeCount = activeAnimations.Count;
            paused = isPaused;
            shutdown = isShutdown;
        }

        long ticks = Interlocked.Read(ref tickCount);
        long timestampDelta = Interlocked.Read(ref totalTickTimestampDelta);
        TimeSpan average = ticks == 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds((double)timestampDelta / ticks / Stopwatch.Frequency);
        return new AnimationSchedulerDiagnostics(
            activeCount,
            ticks,
            Interlocked.Read(ref completedCount),
            Interlocked.Read(ref canceledCount),
            Interlocked.Read(ref faultedCount),
            average,
            tickSource.IsRunning,
            paused,
            shutdown);
    }

    /// <summary>
    /// Permanently stops this scheduler and cancels every remaining animation.
    /// </summary>
    /// <remarks>
    /// Shutdown is thread-safe and idempotent. A shut-down instance cannot be restarted. The
    /// process-wide scheduler is shut down by application exit; tests and custom hosting may call
    /// this method directly.
    /// </remarks>
    public void Shutdown()
    {
        lock (sync)
        {
            if (isShutdown)
                return;

            isShutdown = true;
            for (int index = activeAnimations.Count - 1; index >= 0; index--)
            {
                AnimationEntry entry = activeAnimations[index];
                RemoveEntryLocked(entry);
                if (entry.TrySetTerminal(AnimationState.Canceled))
                    canceledCount++;
            }
            activeAnimations.Clear();
            keyedAnimations.Clear();
        }

        Policy.Changed -= HandlePolicyChanged;
        UnbindPlatformLifecycle();
        tickSource.Stop();
        tickSource.Dispose();
    }

    /// <summary>Shuts down this scheduler. Equivalent to <see cref="Shutdown"/>.</summary>
    public void Dispose() => Shutdown();

    internal static void CancelOwnedIfInitialized(object owner)
    {
        if (DefaultInstance.IsValueCreated)
            DefaultInstance.Value.CancelAll(owner);
    }

    internal static void ShutdownDefaultIfInitialized()
    {
        if (DefaultInstance.IsValueCreated)
            DefaultInstance.Value.Shutdown();
    }

    internal void Cancel(AnimationEntry entry)
    {
        bool canceled;
        lock (sync)
        {
            RemoveEntryLocked(entry);
            canceled = entry.TrySetTerminal(AnimationState.Canceled);
            if (canceled)
                canceledCount++;
        }

        if (canceled)
            StopTickSourceIfIdle();
    }

    internal void Pause(AnimationEntry entry)
    {
        bool shouldStop = false;
        lock (sync)
        {
            if (entry.IsTerminal || entry.IsIndividuallyPaused)
                return;

            entry.IsIndividuallyPaused = true;
            entry.IndividualPauseTime = GetEffectiveTimeLocked(clock.CurrentTime);
            if (!entry.IsPausedByScheduler)
                entry.ResumeState = entry.State;
            entry.SetState(AnimationState.Paused);
            shouldStop = !HasRunnableAnimationsLocked();
        }

        if (shouldStop)
            tickSource.Stop();
    }

    internal void Resume(AnimationEntry entry)
    {
        bool shouldStart = false;
        lock (sync)
        {
            if (entry.IsTerminal || !entry.IsIndividuallyPaused)
                return;

            TimeSpan effectiveNow = GetEffectiveTimeLocked(clock.CurrentTime);
            entry.StartTime += effectiveNow - entry.IndividualPauseTime;
            entry.IsIndividuallyPaused = false;
            if (!entry.IsPausedByScheduler)
                entry.SetState(entry.ResumeState);
            shouldStart = !isPaused && HasRunnableAnimationsLocked();
        }

        if (shouldStart)
            tickSource.Start(RequestTick);
    }

    private static AnimationScheduler CreateDefault() => new();

    private void RequestTick()
    {
        if (Interlocked.Exchange(ref tickPosted, 1) != 0)
            return;

        try
        {
            dispatcher.Post(ProcessTick);
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref tickPosted, 0);
            FaultAllAfterDispatcherFailure(exception);
        }
    }

    private void ProcessTick()
    {
        Interlocked.Exchange(ref tickPosted, 0);
        long tickStarted = Stopwatch.GetTimestamp();
        bool processedAny = false;

        lock (sync)
        {
            if (isShutdown || isPaused || activeAnimations.Count == 0)
                return;

            tickBuffer.Clear();
            tickBuffer.AddRange(activeAnimations);
        }

        TimeSpan now;
        lock (sync)
            now = GetEffectiveTimeLocked(clock.CurrentTime);

        try
        {
            foreach (AnimationEntry entry in tickBuffer)
            {
                AnimationState state = entry.State;
                if (entry.IsTerminal || state == AnimationState.Paused)
                    continue;

                TimeSpan elapsed = now - entry.StartTime;
                if (elapsed < entry.Options.Delay)
                {
                    entry.SetState(AnimationState.Delayed);
                    continue;
                }

                processedAny = true;
                entry.SetState(AnimationState.Running);
                double durationTicks = entry.Options.Duration.Ticks;
                float rawProgress = durationTicks <= 0d
                    ? 1f
                    : Math.Clamp((float)((elapsed - entry.Options.Delay).Ticks / durationTicks), 0f, 1f);

                try
                {
                    float easedProgress;
                    if (rawProgress <= 0f)
                        easedProgress = 0f;
                    else if (rawProgress >= 1f)
                        easedProgress = 1f;
                    else
                        easedProgress = entry.Options.Easing(rawProgress);

                    if (!float.IsFinite(easedProgress))
                        throw new InvalidOperationException("The animation easing function returned NaN or infinity.");

                    if (entry.State == AnimationState.Canceled)
                        continue;
                    entry.Update(easedProgress);

                    if (rawProgress >= 1f)
                        Complete(entry);
                }
                catch (Exception exception)
                {
                    Fault(entry, exception);
                }
            }
        }
        finally
        {
            tickBuffer.Clear();
            if (processedAny)
            {
                Interlocked.Increment(ref tickCount);
                Interlocked.Add(ref totalTickTimestampDelta, Stopwatch.GetTimestamp() - tickStarted);
            }
            StopTickSourceIfIdle();
        }
    }

    private void Complete(AnimationEntry entry)
    {
        lock (sync)
        {
            RemoveEntryLocked(entry);
            if (entry.TrySetTerminal(AnimationState.Completed))
                completedCount++;
        }
    }

    private void Fault(AnimationEntry entry, Exception exception)
    {
        lock (sync)
        {
            RemoveEntryLocked(entry);
            if (!entry.TrySetTerminal(AnimationState.Faulted, exception))
                return;
            faultedCount++;
        }

        Trace.TraceError("Animation '{0}' owned by '{1}' faulted: {2}", entry.Key, entry.Owner.GetType().FullName, exception);
    }

    private void PostImmediateCompletion(AnimationEntry entry)
    {
        try
        {
            dispatcher.Post(() => CompleteImmediately(entry));
        }
        catch (Exception exception)
        {
            Fault(entry, exception);
        }
    }

    private void CompleteImmediately(AnimationEntry entry)
    {
        if (entry.IsTerminal)
            return;

        try
        {
            entry.Update(1f);
            Complete(entry);
        }
        catch (Exception exception)
        {
            Fault(entry, exception);
        }
    }

    private void StopTickSourceIfIdle()
    {
        bool shouldStop;
        lock (sync)
            shouldStop = isShutdown || isPaused || !HasRunnableAnimationsLocked();
        if (shouldStop)
            tickSource.Stop();
    }

    private bool HasRunnableAnimationsLocked()
    {
        foreach (AnimationEntry entry in activeAnimations)
        {
            if (!entry.IsTerminal && !entry.IsIndividuallyPaused && !entry.IsPausedByScheduler)
                return true;
        }
        return false;
    }

    private TimeSpan GetEffectiveTimeLocked(TimeSpan currentClockTime)
    {
        TimeSpan paused = totalPausedTime;
        if (isPaused)
            paused += currentClockTime - schedulerPauseStarted;
        return currentClockTime - paused;
    }

    private void RemoveEntryLocked(AnimationEntry entry)
    {
        activeAnimations.Remove(entry);
        var identity = new AnimationIdentity(entry.Owner, entry.Key);
        if (keyedAnimations.TryGetValue(identity, out AnimationEntry? current) && ReferenceEquals(current, entry))
            keyedAnimations.Remove(identity);
    }

    private void HandlePolicyChanged(object? sender, EventArgs e)
    {
        if (!Policy.ShouldCompleteImmediately)
            return;

        List<AnimationEntry>? completing = null;
        lock (sync)
        {
            if (isShutdown)
                return;

            for (int index = activeAnimations.Count - 1; index >= 0; index--)
            {
                AnimationEntry entry = activeAnimations[index];
                RemoveEntryLocked(entry);
                (completing ??= []).Add(entry);
            }
        }

        tickSource.Stop();
        if (completing is null)
            return;
        foreach (AnimationEntry entry in completing)
            PostImmediateCompletion(entry);
    }

    private void FaultAllAfterDispatcherFailure(Exception exception)
    {
        List<AnimationEntry> entries;
        lock (sync)
        {
            entries = [.. activeAnimations];
            activeAnimations.Clear();
            keyedAnimations.Clear();
        }

        tickSource.Stop();
        foreach (AnimationEntry entry in entries)
            Fault(entry, exception);
    }

    private void BindPlatformLifecycleIfAvailable()
    {
        // Implemented in the lifecycle partial to keep the platform service boundary isolated.
        BindPlatformLifecycleCore();
    }

    partial void BindPlatformLifecycleCore();

    partial void UnbindPlatformLifecycle();

    private readonly record struct AnimationIdentity(object Owner, string Key);

    private sealed class AnimationIdentityComparer : IEqualityComparer<AnimationIdentity>
    {
        public static AnimationIdentityComparer Instance { get; } = new();

        public bool Equals(AnimationIdentity x, AnimationIdentity y)
            => ReferenceEquals(x.Owner, y.Owner) && string.Equals(x.Key, y.Key, StringComparison.Ordinal);

        public int GetHashCode(AnimationIdentity obj)
            => HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Owner), StringComparer.Ordinal.GetHashCode(obj.Key));
    }
}
