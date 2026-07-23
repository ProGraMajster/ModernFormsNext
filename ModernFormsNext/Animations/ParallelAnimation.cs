namespace ModernFormsNext.Animations;

/// <summary>
/// Runs child animation definitions concurrently on the shared scheduler.
/// </summary>
/// <remarks>
/// All children are started before completion is awaited. Faults are reported in declaration
/// order through an <see cref="AggregateException"/>; cancellation is returned when no child
/// faulted and at least one child was canceled.
/// </remarks>
public sealed class ParallelAnimation : AnimationDefinition
{
    private readonly AnimationDefinition[] children;

    /// <summary>Creates a parallel composition from the specified children.</summary>
    public ParallelAnimation(IEnumerable<AnimationDefinition> animations)
    {
        ArgumentNullException.ThrowIfNull(animations);
        children = animations.ToArray();
        if (children.Any(static child => child is null))
            throw new ArgumentException("Animation compositions cannot contain null children.", nameof(animations));
    }

    /// <summary>Gets the children in declaration order.</summary>
    public IReadOnlyList<AnimationDefinition> Children => children;

    internal override bool RequiresTarget => false;

    /// <inheritdoc/>
    protected override void Update(AnimationContext context, float progress)
    {
    }

    internal override async Task<AnimationExecutionResult> ExecuteCoreAsync(
        AnimationExecutionScope scope,
        bool reverse)
    {
        var tasks = new Task<AnimationExecutionResult>[children.Length];
        for (int index = 0; index < children.Length; index++)
            tasks[index] = children[index].ExecuteAsync(scope.CreateChild(index), reverse);

        AnimationExecutionResult[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
        List<Exception>? faults = null;
        bool canceled = false;
        foreach (AnimationExecutionResult result in results)
        {
            if (result.State == AnimationState.Faulted)
                (faults ??= []).Add(result.Exception!);
            else if (result.State == AnimationState.Canceled)
                canceled = true;
        }

        if (faults is not null)
            return AnimationExecutionResult.Faulted(new AggregateException(faults));
        return canceled ? AnimationExecutionResult.Canceled : AnimationExecutionResult.Completed;
    }
}
