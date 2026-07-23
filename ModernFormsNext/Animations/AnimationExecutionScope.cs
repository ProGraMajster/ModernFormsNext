namespace ModernFormsNext.Animations;

internal readonly record struct AnimationExecutionScope(
    AnimationScheduler Scheduler,
    Control? DefaultTarget,
    CancellationToken CancellationToken,
    string KeyPrefix,
    object RunOwner)
{
    private static long nextScopeId;

    public AnimationExecutionScope CreateChild(int childIndex)
    {
        long scopeId = Interlocked.Increment(ref nextScopeId);
        return this with { KeyPrefix = $"{KeyPrefix}Group:{scopeId}:{childIndex}:" };
    }
}
