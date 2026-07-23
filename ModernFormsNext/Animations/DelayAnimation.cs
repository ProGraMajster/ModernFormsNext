namespace ModernFormsNext.Animations;

/// <summary>
/// Represents a scheduler-driven pause inside a composition or timeline.
/// </summary>
public sealed class DelayAnimation : AnimationDefinition
{
    /// <summary>Creates a delay with the specified non-negative interval.</summary>
    public DelayAnimation(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Delay cannot be negative.");
        Duration = duration;
        Key = "Delay";
    }

    internal override bool RequiresTarget => false;

    /// <inheritdoc/>
    protected override void Update(AnimationContext context, float progress)
    {
    }

    internal override Task<AnimationExecutionResult> ExecuteCoreAsync(
        AnimationExecutionScope scope,
        bool reverse)
    {
        AnimationHandle handle = scope.Scheduler.StartFrames(
            scope.RunOwner,
            scope.KeyPrefix + ResolveKey(),
            static _ => { },
            CreateOptions());
        return AwaitLeafAsync(handle, context: null, scope.CancellationToken);
    }
}
