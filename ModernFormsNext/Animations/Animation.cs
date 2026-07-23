namespace ModernFormsNext.Animations;

/// <summary>
/// Creates scheduler-backed animation compositions.
/// </summary>
public static class Animation
{
    /// <summary>Creates a composition that runs its children in declaration order.</summary>
    /// <param name="animations">The non-null child definitions.</param>
    /// <returns>A reusable sequential definition.</returns>
    public static AnimationDefinition Sequence(params AnimationDefinition[] animations)
        => new SequenceAnimation(animations);

    /// <summary>Creates a composition that starts all children together.</summary>
    /// <param name="animations">The non-null child definitions.</param>
    /// <returns>A reusable parallel definition.</returns>
    public static AnimationDefinition Parallel(params AnimationDefinition[] animations)
        => new ParallelAnimation(animations);

    /// <summary>
    /// Creates a scheduler-driven delay that observes lifecycle pause and reduced-motion policy.
    /// </summary>
    /// <param name="duration">The non-negative delay interval.</param>
    /// <returns>A reusable delay definition.</returns>
    public static AnimationDefinition Delay(TimeSpan duration)
        => new DelayAnimation(duration);
}
