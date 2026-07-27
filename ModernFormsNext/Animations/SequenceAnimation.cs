namespace ModernFormsNext.Animations;

/// <summary>
/// Runs child animation definitions sequentially without blocking the UI dispatcher.
/// </summary>
public sealed class SequenceAnimation : AnimationDefinition
{
    private readonly AnimationDefinition[] children;

    /// <summary>Creates a sequential composition from the specified children.</summary>
    public SequenceAnimation(IEnumerable<AnimationDefinition> animations)
    {
        ArgumentNullException.ThrowIfNull(animations);
        children = animations.ToArray();
        if (children.Any(static child => child is null))
            throw new ArgumentException("Animation compositions cannot contain null children.", nameof(animations));
    }

    /// <summary>Gets the children in declaration order.</summary>
    public IReadOnlyList<AnimationDefinition> Children => children;

    internal override bool RequiresTarget => false;

    internal override bool HasSchedulableWork
        => children.Any(static child => child.HasSchedulableWork);

    /// <inheritdoc/>
    protected override void Update(AnimationContext context, float progress)
    {
    }

    internal override async Task<AnimationExecutionResult> ExecuteCoreAsync(
        AnimationExecutionScope scope,
        bool reverse)
    {
        for (int step = 0; step < children.Length; step++)
        {
            int childIndex = reverse ? children.Length - step - 1 : step;
            AnimationExecutionResult result =
                await children[childIndex]
                    .ExecuteAsync(scope.CreateChild(childIndex), reverse)
                    .ConfigureAwait(false);
            if (result.State != AnimationState.Completed)
                return result;
        }

        return AnimationExecutionResult.Completed;
    }
}
