using System.ComponentModel;

namespace ModernFormsNext.Animations;

/// <summary>
/// Defines a reusable scheduler-backed animation that can be run directly or composed.
/// </summary>
/// <remarks>
/// <para>
/// Derive from this class and implement <see cref="Update"/> for custom animations. The same
/// definition can be run more than once; per-run timing and target state are held by
/// <see cref="AnimationRun"/>, not by the definition.
/// </para>
/// <para>
/// Repeat count is the number of forward iterations. When auto-reverse is enabled, each iteration
/// has one forward and one reverse leg. Infinite repeat requires cancellation in normal-motion
/// mode and collapses to one deterministic sample under reduced motion.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class ShakeAnimation : AnimationDefinition
/// {
///     public float Distance { get; set; } = 8f;
///     public int Oscillations { get; set; } = 4;
///
///     protected override void Update(AnimationContext context, float progress)
///     {
///         context.Target.TranslationX =
///             MathF.Sin(progress * MathF.PI * 2f * Oscillations)
///             * Distance
///             * (1f - progress);
///     }
/// }
///
/// await new ShakeAnimation().RunAsync(button);
/// </code>
/// </example>
public abstract class AnimationDefinition
{
    private TimeSpan duration = TimeSpan.FromMilliseconds(250);
    private TimeSpan delay;
    private Func<float, float> easing = Easings.Linear;
    private string? key;
    private AnimationReplacementMode replacementMode = AnimationReplacementMode.Replace;
    private int repeatCount = 1;
    private bool repeatsForever;
    private bool autoReverse;

    /// <summary>Gets or sets the unscaled duration of one playback leg.</summary>
    public TimeSpan Duration
    {
        get => duration;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Animation duration cannot be negative.");
            duration = value;
        }
    }

    /// <summary>Gets or sets the unscaled delay before each playback leg.</summary>
    public TimeSpan Delay
    {
        get => delay;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Animation delay cannot be negative.");
            delay = value;
        }
    }

    /// <summary>Gets or sets the easing used by one playback leg.</summary>
    public Func<float, float> Easing
    {
        get => easing;
        set => easing = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the owner-local replacement key. A generated type-based key is used when null.
    /// </summary>
    public string? Key
    {
        get => key;
        set
        {
            if (value is not null)
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
            key = value;
        }
    }

    /// <summary>Gets or sets owner/key replacement behavior.</summary>
    public AnimationReplacementMode ReplacementMode
    {
        get => replacementMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            replacementMode = value;
        }
    }

    /// <summary>Gets the configured finite forward-iteration count.</summary>
    public int RepeatCount => repeatCount;

    /// <summary>Gets whether the definition repeats until cancellation.</summary>
    public bool RepeatsForever => repeatsForever;

    /// <summary>Gets whether every forward iteration has a reverse leg.</summary>
    public bool IsAutoReversed => autoReverse;

    /// <summary>Configures a finite number of forward iterations.</summary>
    /// <param name="count">A positive iteration count.</param>
    /// <returns>This definition.</returns>
    public AnimationDefinition Repeat(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Repeat count must be positive.");
        repeatCount = count;
        repeatsForever = false;
        return this;
    }

    /// <summary>Repeats until the returned run is canceled.</summary>
    /// <returns>This definition.</returns>
    /// <remarks>
    /// A composition with no scheduler-backed children faults its run instead of entering a
    /// synchronous busy loop. Reduced-motion policy collapses a non-empty infinite repeat to one
    /// deterministic iteration.
    /// </remarks>
    public AnimationDefinition RepeatForever()
    {
        repeatsForever = true;
        return this;
    }

    /// <summary>Adds a reverse leg after every forward iteration.</summary>
    /// <returns>This definition.</returns>
    public AnimationDefinition AutoReverse()
    {
        autoReverse = true;
        return this;
    }

    /// <summary>Starts a definition whose target is already bound by a factory or composition.</summary>
    public AnimationRun Start(
        AnimationScheduler? scheduler = null,
        CancellationToken cancellationToken = default)
    {
        Control? target = BoundTarget;
        if (target is null && RequiresTarget)
            throw new InvalidOperationException("This animation definition requires a target control.");
        return StartCore(target, scheduler ?? AnimationScheduler.Default, cancellationToken);
    }

    /// <summary>Starts this definition for the specified target control.</summary>
    public AnimationRun Start(
        Control target,
        AnimationScheduler? scheduler = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return StartCore(target, scheduler ?? AnimationScheduler.Default, cancellationToken);
    }

    /// <summary>Runs a bound definition and returns its terminal state.</summary>
    public Task<AnimationState> RunAsync(
        AnimationScheduler? scheduler = null,
        CancellationToken cancellationToken = default)
        => Start(scheduler, cancellationToken).Completion;

    /// <summary>Runs this definition for the specified target and returns its terminal state.</summary>
    public Task<AnimationState> RunAsync(
        Control target,
        AnimationScheduler? scheduler = null,
        CancellationToken cancellationToken = default)
        => Start(target, scheduler, cancellationToken).Completion;

    /// <summary>Updates one frame of a custom definition.</summary>
    /// <param name="context">The reused timing and target context for this run.</param>
    /// <param name="progress">Direction-aware eased progress.</param>
    protected abstract void Update(AnimationContext context, float progress);

    internal virtual Control? BoundTarget => null;

    internal virtual bool RequiresTarget => true;

    internal virtual bool HasSchedulableWork => true;

    internal virtual async Task<AnimationExecutionResult> ExecuteAsync(AnimationExecutionScope scope, bool reverse = false)
    {
        if (repeatsForever && !HasSchedulableWork)
        {
            return AnimationExecutionResult.Faulted(new InvalidOperationException(
                "An empty animation composition cannot repeat forever because it has no scheduler-backed work to yield."));
        }

        int iteration = 0;
        bool collapseInfinite = repeatsForever && scope.Scheduler.Policy.ShouldCompleteImmediately;

        while (repeatsForever || iteration < repeatCount)
        {
            scope.CancellationToken.ThrowIfCancellationRequested();
            AnimationExecutionResult forward = await ExecuteCoreAsync(scope, reverse).ConfigureAwait(false);
            if (forward.State != AnimationState.Completed || forward.WasIgnored)
                return forward;

            if (autoReverse)
            {
                AnimationExecutionResult backward = await ExecuteCoreAsync(scope, !reverse).ConfigureAwait(false);
                if (backward.State != AnimationState.Completed || backward.WasIgnored)
                    return backward;
            }

            iteration++;
            if (collapseInfinite)
                break;
        }

        return AnimationExecutionResult.Completed;
    }

    internal virtual Task<AnimationExecutionResult> ExecuteCoreAsync(
        AnimationExecutionScope scope,
        bool reverse)
    {
        Control target = BoundTarget ?? scope.DefaultTarget
            ?? throw new InvalidOperationException("This animation definition requires a target control.");
        return ScheduleAsync(scope, target, ResolveKey(), reverse, Update);
    }

    internal Task<AnimationExecutionResult> ScheduleAsync(
        AnimationExecutionScope scope,
        Control target,
        string key,
        bool reverse,
        Action<AnimationContext, float> update)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(update);

        var context = new AnimationContext(target, scope.CancellationToken);
        AnimationHandle handle = scope.Scheduler.StartFrames(
            target,
            scope.KeyPrefix + key,
            frame =>
            {
                context.SetFrame(frame, reverse);
                update(context, context.EasedProgress);
            },
            CreateOptions(),
            out bool scheduled);

        return AwaitLeafAsync(handle, context, scope.CancellationToken, scheduled);
    }

    internal AnimationOptions CreateOptions()
        => new()
        {
            Duration = Duration,
            Delay = Delay,
            Easing = Easing,
            ReplacementMode = ReplacementMode
        };

    internal string ResolveKey() => Key ?? GetType().FullName ?? GetType().Name;

    private AnimationRun StartCore(
        Control? target,
        AnimationScheduler scheduler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var run = new AnimationRun();
        run.Start(
            token => ExecuteAsync(new AnimationExecutionScope(
                scheduler,
                target,
                token,
                string.Empty,
                new object())),
            cancellationToken);
        return run;
    }

    internal static async Task<AnimationExecutionResult> AwaitLeafAsync(
        AnimationHandle handle,
        AnimationContext? context,
        CancellationToken cancellationToken,
        bool cancelHandleOnCancellation = true)
    {
        TaskCompletionSource? ignoredRunCancellation = null;
        using CancellationTokenRegistration registration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(
                cancelHandleOnCancellation
                    ? handle.Cancel
                    : () => ignoredRunCancellation?.TrySetResult())
            : default;

        try
        {
            Task<AnimationState> completion = handle.Completion;
            if (!cancelHandleOnCancellation && cancellationToken.CanBeCanceled && !completion.IsCompleted)
            {
                ignoredRunCancellation =
                    new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                if (cancellationToken.IsCancellationRequested)
                    ignoredRunCancellation.TrySetResult();
                Task finished = await Task.WhenAny(
                    completion,
                    ignoredRunCancellation.Task).ConfigureAwait(false);
                if (!ReferenceEquals(finished, completion))
                    return AnimationExecutionResult.Canceled;
            }

            AnimationState state = await completion.ConfigureAwait(false);
            return state switch
            {
                AnimationState.Completed => cancelHandleOnCancellation
                    ? AnimationExecutionResult.Completed
                    : AnimationExecutionResult.Ignored,
                AnimationState.Canceled => AnimationExecutionResult.Canceled,
                AnimationState.Faulted => AnimationExecutionResult.Faulted(
                    handle.Exception ?? new InvalidOperationException("The animation fault did not provide an exception.")),
                _ => throw new InvalidOperationException($"Unexpected terminal animation state '{state}'.")
            };
        }
        finally
        {
            context?.ReleaseTarget();
        }
    }
}
