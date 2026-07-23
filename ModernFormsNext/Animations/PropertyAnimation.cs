namespace ModernFormsNext.Animations;

/// <summary>
/// Defines a typed property animation using a target, value provider, setter, and interpolator.
/// </summary>
/// <typeparam name="T">The value type produced on the UI dispatcher.</typeparam>
public sealed class PropertyAnimation<T> : AnimationDefinition
{
    private readonly Control target;
    private readonly Func<T> from;
    private readonly T to;
    private readonly IAnimationInterpolator<T> interpolator;
    private readonly Action<T> update;

    /// <summary>Creates a property animation with an explicit fixed start value.</summary>
    public PropertyAnimation(
        Control target,
        string key,
        T from,
        T to,
        IAnimationInterpolator<T> interpolator,
        Action<T> update)
        : this(target, key, () => from, to, interpolator, update)
    {
    }

    internal PropertyAnimation(
        Control target,
        string key,
        Func<T> from,
        T to,
        IAnimationInterpolator<T> interpolator,
        Action<T> update)
    {
        this.target = target ?? throw new ArgumentNullException(nameof(target));
        Key = key;
        this.from = from ?? throw new ArgumentNullException(nameof(from));
        this.to = to;
        this.interpolator = interpolator ?? throw new ArgumentNullException(nameof(interpolator));
        this.update = update ?? throw new ArgumentNullException(nameof(update));
    }

    /// <summary>Gets the bound target control.</summary>
    public Control Target => target;

    /// <summary>Gets the target value.</summary>
    public T To => to;

    internal override Control? BoundTarget => target;

    /// <inheritdoc/>
    protected override void Update(AnimationContext context, float progress)
    {
        // Per-run start capture is implemented in ExecuteCoreAsync.
    }

    internal override async Task<AnimationExecutionResult> ExecuteAsync(
        AnimationExecutionScope scope,
        bool reverse = false)
    {
        T start = from();
        int iteration = 0;
        bool collapseInfinite = RepeatsForever && scope.Scheduler.Policy.ShouldCompleteImmediately;

        while (RepeatsForever || iteration < RepeatCount)
        {
            scope.CancellationToken.ThrowIfCancellationRequested();
            AnimationExecutionResult forward =
                await ExecutePropertyLegAsync(scope, start, reverse).ConfigureAwait(false);
            if (forward.State != AnimationState.Completed)
                return forward;

            if (IsAutoReversed)
            {
                AnimationExecutionResult backward =
                    await ExecutePropertyLegAsync(scope, start, !reverse).ConfigureAwait(false);
                if (backward.State != AnimationState.Completed)
                    return backward;
            }

            iteration++;
            if (collapseInfinite)
                break;
        }

        return AnimationExecutionResult.Completed;
    }

    internal override Task<AnimationExecutionResult> ExecuteCoreAsync(
        AnimationExecutionScope scope,
        bool reverse)
    {
        T start = from();
        return ExecutePropertyLegAsync(scope, start, reverse);
    }

    private Task<AnimationExecutionResult> ExecutePropertyLegAsync(
        AnimationExecutionScope scope,
        T start,
        bool reverse)
    {
        return ScheduleAsync(
            scope,
            target,
            ResolveKey(),
            reverse,
            (_, progress) => update(interpolator.Interpolate(start, to, progress)));
    }
}
