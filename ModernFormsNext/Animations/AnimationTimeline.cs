namespace ModernFormsNext.Animations;

/// <summary>
/// Schedules definitions at monotonic offsets on the shared animation scheduler.
/// </summary>
/// <remarks>
/// Timeline offsets are implemented as scheduler-backed delays, so application lifecycle pause
/// excludes background time. Each entry starts exactly once per timeline leg.
/// </remarks>
public sealed class AnimationTimeline : AnimationDefinition
{
    private readonly List<TimelineEntry> entries = [];

    /// <summary>Adds an animation at the specified non-negative offset.</summary>
    /// <param name="offset">Time from the beginning of the timeline leg.</param>
    /// <param name="animation">The definition to start at that offset.</param>
    /// <returns>This timeline.</returns>
    public AnimationTimeline At(TimeSpan offset, AnimationDefinition animation)
    {
        if (offset < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Timeline offsets cannot be negative.");
        ArgumentNullException.ThrowIfNull(animation);
        entries.Add(new TimelineEntry(offset, animation, entries.Count));
        return this;
    }

    /// <summary>Gets the number of scheduled timeline entries.</summary>
    public int Count => entries.Count;

    internal override bool RequiresTarget => false;

    internal override bool HasSchedulableWork
        => entries.Any(static entry =>
            entry.Offset > TimeSpan.Zero || entry.Animation.HasSchedulableWork);

    /// <inheritdoc/>
    protected override void Update(AnimationContext context, float progress)
    {
    }

    internal override async AnimationCompletion<AnimationExecutionResult> ExecuteCoreAsync(
        AnimationExecutionScope scope,
        bool reverse)
    {
        if (entries.Count == 0)
            return AnimationExecutionResult.Completed;

        TimeSpan lastOffset = entries.Max(static entry => entry.Offset);
        var tasks = new AnimationCompletion<AnimationExecutionResult>[entries.Count];
        for (int index = 0; index < entries.Count; index++)
        {
            TimelineEntry entry = entries[index];
            TimeSpan offset = reverse ? lastOffset - entry.Offset : entry.Offset;
            tasks[index] = ExecuteEntryAsync(entry, offset, scope.CreateChild(entry.Index), reverse);
        }

        List<Exception>? faults = null;
        bool canceled = false;
        // Entries have already started concurrently. Observe their direct signals in declaration
        // order so offsets and completion need no thread-pool continuation between clock frames.
        foreach (AnimationCompletion<AnimationExecutionResult> task in tasks)
        {
            AnimationExecutionResult result = await task.On(scope.Scheduler);
            if (result.State == AnimationState.Faulted)
                (faults ??= []).Add(result.Exception!);
            else if (result.State == AnimationState.Canceled)
                canceled = true;
        }

        if (faults is not null)
            return AnimationExecutionResult.Faulted(new AggregateException(faults));
        return canceled ? AnimationExecutionResult.Canceled : AnimationExecutionResult.Completed;
    }

    private static async AnimationCompletion<AnimationExecutionResult> ExecuteEntryAsync(
        TimelineEntry entry,
        TimeSpan offset,
        AnimationExecutionScope scope,
        bool reverse)
    {
        try
        {
            if (offset > TimeSpan.Zero)
            {
                var delay = new DelayAnimation(offset);
                AnimationExecutionResult delayResult =
                    await delay.ExecuteAsync(scope.CreateChild(0)).On(scope.Scheduler);
                if (delayResult.State != AnimationState.Completed)
                    return delayResult;
            }

            return await entry.Animation
                .ExecuteAsync(scope.CreateChild(1), reverse)
                .On(scope.Scheduler);
        }
        catch (OperationCanceledException) when (scope.CancellationToken.IsCancellationRequested)
        {
            return AnimationExecutionResult.Canceled;
        }
        catch (Exception exception)
        {
            return AnimationExecutionResult.Faulted(exception);
        }
    }

    private readonly record struct TimelineEntry(
        TimeSpan Offset,
        AnimationDefinition Animation,
        int Index);
}
