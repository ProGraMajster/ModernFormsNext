using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

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
        : this(new StopwatchAnimationClock(), new DefaultAnimationDispatcher(), CreateDefaultTickSource(), new AnimationPolicy())
    {
    }

    internal AnimationScheduler(
        IAnimationClock clock,
        IAnimationDispatcher dispatcher,
        IAnimationTickSource tickSource,
        AnimationPolicy policy,
        IPlatformApplicationLifecycle? platformLifecycle = null,
        IPlatformAnimationSettings? platformAnimationSettings = null,
        Func<bool>? isDesignMode = null)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.tickSource = tickSource ?? throw new ArgumentNullException(nameof(tickSource));
        this.isDesignMode = isDesignMode ?? IsProcessInDesignMode;
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        Policy.Changed += HandlePolicyChanged;
        if (platformAnimationSettings is not null)
            BindPlatformAnimationSettings(platformAnimationSettings);
        if (platformLifecycle is not null)
            BindPlatformLifecycle(platformLifecycle);
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
        ArgumentNullException.ThrowIfNull(update);
        return StartFrames(owner, key, frame => update(frame.EasedProgress), options);
    }

    internal AnimationHandle StartFrames(
        object owner,
        string key,
        Action<AnimationFrame> update,
        AnimationOptions? options = null)
        => StartFrames(owner, key, update, options, out _);

    internal AnimationHandle StartFrames(
        object owner,
        string key,
        Action<AnimationFrame> update,
        AnimationOptions? options,
        out bool scheduled)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(update);

        BindPlatformLifecycleIfAvailable();
        BindPlatformAnimationSettingsIfAvailable();
        AnimationOptionsSnapshot snapshot = (options ?? new AnimationOptions()).CreateSnapshot(Policy);
        if (owner is IComponent { Site.DesignMode: true } ||
            owner is InteractionEffect { Target.Site.DesignMode: true })
            snapshot = snapshot with { CompleteImmediately = true };

        AnimationEntry? replaced = null;
        AnimationEntry entry;
        bool shouldCompleteImmediately;

        lock (sync)
        {
            ObjectDisposedException.ThrowIf(isShutdown, this);
            var identity = new AnimationIdentity(owner, key);
            if (keyedAnimations.TryGetValue(identity, out AnimationEntry? existing))
            {
                if (snapshot.ReplacementMode == AnimationReplacementMode.IgnoreNew)
                {
                    scheduled = false;
                    return existing.Handle;
                }

                RemoveEntryLocked(existing);
                if (existing.TryBeginTerminal(AnimationState.Canceled))
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
                StartTickSourceIfNeededLocked();
            }
            else
            {
                entry.SetState(AnimationState.Running);
                keyedAnimations.Add(identity, entry);
                StopTickSourceIfIdleLocked();
            }
        }

        scheduled = true;
        if (replaced is not null)
            replaced.FinishTerminal(signalCancellation: true);

        if (shouldCompleteImmediately)
            PostImmediateCompletion(entry);

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
                if (!ReferenceEquals(entry.OwnerOrNull, owner))
                    continue;

                RemoveEntryLocked(entry);
                if (entry.TryBeginTerminal(AnimationState.Canceled))
                {
                    canceledCount++;
                    (canceled ??= []).Add(entry);
                }
            }

            foreach (AnimationEntry entry in keyedAnimations.Values.ToArray())
            {
                if (!ReferenceEquals(entry.OwnerOrNull, owner) || activeAnimations.Contains(entry))
                    continue;

                RemoveEntryLocked(entry);
                if (entry.TryBeginTerminal(AnimationState.Canceled))
                {
                    canceledCount++;
                    (canceled ??= []).Add(entry);
                }
            }

            StopTickSourceIfIdleLocked();
        }

        if (canceled is not null)
        {
            foreach (AnimationEntry entry in canceled)
                entry.FinishTerminal(signalCancellation: true);
        }
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

            tickSource.Stop();
        }
    }

    /// <summary>Resumes globally paused time without including the background interval.</summary>
    public void Resume()
    {
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

            StartTickSourceIfNeededLocked();
        }
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
        List<AnimationEntry> canceled = [];
        lock (sync)
        {
            if (isShutdown)
                return;

            isShutdown = true;
            for (int index = activeAnimations.Count - 1; index >= 0; index--)
            {
                AnimationEntry entry = activeAnimations[index];
                RemoveEntryLocked(entry);
                if (entry.TryBeginTerminal(AnimationState.Canceled))
                {
                    canceledCount++;
                    canceled.Add(entry);
                }
            }

            foreach (AnimationEntry entry in keyedAnimations.Values.ToArray())
            {
                RemoveEntryLocked(entry);
                if (entry.TryBeginTerminal(AnimationState.Canceled))
                {
                    canceledCount++;
                    canceled.Add(entry);
                }
            }
            activeAnimations.Clear();
            keyedAnimations.Clear();
            tickSource.Stop();
        }

        foreach (AnimationEntry entry in canceled)
            entry.FinishTerminal(signalCancellation: true);

        Policy.Changed -= HandlePolicyChanged;
        UnbindPlatformAnimationSettings();
        UnbindPlatformLifecycle();
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

    internal static AnimationSchedulerDiagnostics? GetDefaultDiagnosticsIfInitialized()
        => DefaultInstance.IsValueCreated ? DefaultInstance.Value.GetDiagnostics() : null;

    internal void Cancel(AnimationEntry entry)
    {
        bool canceled;
        lock (sync)
        {
            RemoveEntryLocked(entry);
            canceled = entry.TryBeginTerminal(AnimationState.Canceled);
            if (canceled)
                canceledCount++;
            StopTickSourceIfIdleLocked();
        }

        if (canceled)
            entry.FinishTerminal(signalCancellation: true);
    }

    internal void Pause(AnimationEntry entry)
    {
        lock (sync)
        {
            if (entry.IsTerminal || entry.IsIndividuallyPaused)
                return;

            entry.IsIndividuallyPaused = true;
            entry.IndividualPauseTime = GetEffectiveTimeLocked(clock.CurrentTime);
            if (!entry.IsPausedByScheduler)
                entry.ResumeState = entry.State;
            entry.SetState(AnimationState.Paused);
            StopTickSourceIfIdleLocked();
        }
    }

    internal void Resume(AnimationEntry entry)
    {
        lock (sync)
        {
            if (entry.IsTerminal || !entry.IsIndividuallyPaused)
                return;

            TimeSpan effectiveNow = GetEffectiveTimeLocked(clock.CurrentTime);
            entry.StartTime += effectiveNow - entry.IndividualPauseTime;
            entry.IsIndividuallyPaused = false;
            if (!entry.IsPausedByScheduler)
                entry.SetState(entry.ResumeState);
            StartTickSourceIfNeededLocked();
        }
    }

    private static AnimationScheduler CreateDefault() => new();

    private static IAnimationTickSource CreateDefaultTickSource()
        => new PlatformAnimationTickSource();

    private void RequestTick()
    {
        if (Interlocked.Exchange(ref tickPosted, 1) != 0)
            return;

        try
        {
            // Android Choreographer invokes the source on the UI thread. Process that display
            // signal inline so invalidation can participate in the same frame instead of always
            // slipping to the next vsync. Timer-backed sources still marshal through Post.
            if (dispatcher.CheckAccess())
                ProcessTick();
            else
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
            using Application.VisualInvalidationBatchScope batch = Application.BeginVisualInvalidationBatch();
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
                        easedProgress = entry.ApplyEasing(rawProgress);

                    if (!float.IsFinite(easedProgress))
                        throw new InvalidOperationException("The animation easing function returned NaN or infinity.");

                    if (entry.State == AnimationState.Canceled)
                        continue;
                    TimeSpan animationElapsed = elapsed - entry.Options.Delay;
                    if (animationElapsed < TimeSpan.Zero)
                        animationElapsed = TimeSpan.Zero;
                    if (animationElapsed > entry.Options.Duration)
                        animationElapsed = entry.Options.Duration;

                    entry.Invoke(new AnimationFrame(
                        rawProgress,
                        easedProgress,
                        animationElapsed,
                        entry.Options.Duration,
                        entry.CancellationToken));

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
            lock (sync)
                StopTickSourceIfIdleLocked();
        }
    }

    private void Complete(AnimationEntry entry)
    {
        bool completed;
        lock (sync)
        {
            RemoveEntryLocked(entry);
            completed = entry.TryBeginTerminal(AnimationState.Completed);
            if (completed)
                completedCount++;
        }

        if (completed)
            entry.FinishTerminal(signalCancellation: false);
    }

    private void Fault(AnimationEntry entry, Exception exception)
    {
        string ownerType = entry.OwnerOrNull?.GetType().FullName ?? "<released>";
        bool faulted;
        lock (sync)
        {
            RemoveEntryLocked(entry);
            faulted = entry.TryBeginTerminal(AnimationState.Faulted, exception);
            if (faulted)
                faultedCount++;
        }

        if (!faulted)
            return;

        entry.FinishTerminal(signalCancellation: false);
        Trace.TraceError("Animation '{0}' owned by '{1}' faulted: {2}", entry.Key, ownerType, exception);
    }

    private void PostImmediateCompletion(AnimationEntry entry)
    {
        try
        {
            // Scale zero and reduced motion must publish the exact endpoint before a UI-thread
            // caller returns. Background callers retain the normal dispatcher marshalling path.
            if (dispatcher.CheckAccess())
                CompleteImmediately(entry);
            else
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
            using Application.VisualInvalidationBatchScope batch = Application.BeginVisualInvalidationBatch();
            entry.Invoke(new AnimationFrame(
                1f,
                1f,
                entry.Options.Duration,
                entry.Options.Duration,
                entry.CancellationToken));
            Complete(entry);
        }
        catch (Exception exception)
        {
            Fault(entry, exception);
        }
    }

    // Tick-source state transitions are serialized with scheduler state. Computing a transition
    // under the lock and performing it afterward lets a concurrent start overtake a stale stop
    // (or a cancellation overtake a stale start), leaving active work unticked or an idle timer
    // running indefinitely.
    private void StartTickSourceIfNeededLocked()
    {
        if (!isShutdown && !isPaused && HasRunnableAnimationsLocked())
            tickSource.Start(RequestTick);
    }

    private void StopTickSourceIfIdleLocked()
    {
        if (isShutdown || isPaused || !HasRunnableAnimationsLocked())
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
        if (entry.OwnerOrNull is not { } owner)
            return;

        var identity = new AnimationIdentity(owner, entry.Key);
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
                // Keep the keyed entry until the queued final-value callback runs. Owner
                // cancellation, replacement, or disposal can then still cancel that callback.
                activeAnimations.RemoveAt(index);
                (completing ??= []).Add(entry);
            }

            tickSource.Stop();
        }
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
            entries = keyedAnimations.Values.Distinct().ToList();
            activeAnimations.Clear();
            keyedAnimations.Clear();
            tickSource.Stop();
        }
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
