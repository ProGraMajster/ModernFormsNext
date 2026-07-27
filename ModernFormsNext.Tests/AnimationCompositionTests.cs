using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class AnimationCompositionTests
{
    [Fact]
    public async Task CustomDefinitionReceivesTimingAndReleasesContextTarget()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var frames = new List<(float Progress, TimeSpan Elapsed)>();
        var animation = new CallbackAnimation((context, progress) =>
        {
            Assert.Same(target, context.Target);
            frames.Add((progress, context.Elapsed));
        })
        {
            Duration = TimeSpan.FromMilliseconds(100)
        };

        AnimationRun run = animation.Start(target, harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(AnimationState.Completed, await run.Completion);
        Assert.Equal([(0.5f, TimeSpan.FromMilliseconds(50)), (1f, TimeSpan.FromMilliseconds(100))], frames);
        Assert.NotNull(animation.LastContext);
        Assert.Throws<ObjectDisposedException>(() => _ = animation.LastContext!.Target);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public async Task PropertyAnimationCapturesStartValueOnSchedulerUiCallback()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var getterThreads = new List<int>();
        var animation = new PropertyAnimation<float>(
            target,
            "ThreadAffinity",
            () =>
            {
                getterThreads.Add(Environment.CurrentManagedThreadId);
                return target.Opacity;
            },
            0.5f,
            AnimationInterpolators.Float,
            value => target.Opacity = value)
        {
            Duration = TimeSpan.FromMilliseconds(100)
        };

        AnimationRun run = await Task.Run(() => animation.Start(harness.Scheduler));
        Assert.Empty(getterThreads);

        int tickThread = Environment.CurrentManagedThreadId;
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(AnimationState.Completed, await run.Completion);
        Assert.Equal([tickThread], getterThreads);
    }

    [Fact]
    public async Task SequenceRunsChildrenInDeclarationOrder()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var order = new List<int>();

        AnimationDefinition sequence = Animation.Sequence(
            Immediate(_ => order.Add(1)),
            Immediate(_ => order.Add(2)),
            Immediate(_ => order.Add(3)));

        AnimationState state = await sequence.RunAsync(target, harness.Scheduler);

        Assert.Equal(AnimationState.Completed, state);
        Assert.Equal([1, 2, 3], order);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public async Task SequenceStopsAfterFaultAndDoesNotStartLaterChildren()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        bool laterChildStarted = false;

        AnimationRun run = Animation.Sequence(
            Immediate(_ => { }),
            Immediate(_ => throw new TestAnimationException("sequence")),
            Immediate(_ => laterChildStarted = true))
            .Start(target, harness.Scheduler);

        Assert.Equal(AnimationState.Faulted, await run.Completion);
        Assert.IsType<TestAnimationException>(run.Exception);
        Assert.False(laterChildStarted);
    }

    [Fact]
    public async Task SequenceCancellationStopsActiveChildAndDoesNotStartLaterChildren()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        bool laterChildStarted = false;
        var active = new CallbackAnimation((_, _) => { })
        {
            Duration = TimeSpan.FromMilliseconds(100)
        };
        AnimationRun run = Animation.Sequence(
            active,
            Immediate(_ => laterChildStarted = true))
            .Start(target, harness.Scheduler);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));
        run.Cancel();

        Assert.Equal(AnimationState.Canceled, await run.Completion);
        Assert.False(laterChildStarted);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public async Task ParallelStartsEveryChildAndAggregatesFaultsInDeclarationOrder()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        int starts = 0;

        AnimationRun run = Animation.Parallel(
            Immediate(_ =>
            {
                starts++;
                throw new TestAnimationException("first");
            }),
            Immediate(_ =>
            {
                starts++;
                throw new InvalidOperationException("second");
            }))
            .Start(target, harness.Scheduler);

        Assert.Equal(AnimationState.Faulted, await run.Completion);
        var aggregate = Assert.IsType<AggregateException>(run.Exception);
        Assert.Equal(2, starts);
        Assert.Collection(
            aggregate.InnerExceptions,
            error => Assert.Equal("first", error.Message),
            error => Assert.Equal("second", error.Message));
    }

    [Fact]
    public async Task ParallelAggregatesChildSetupFaultsInDeclarationOrder()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        KeyframeAnimation<float> first = KeyframeAnimation<float>.Create(target, _ => { });
        KeyframeAnimation<float> second = KeyframeAnimation<float>.Create(target, _ => { });

        AnimationRun run = Animation.Parallel(first, second).Start(harness.Scheduler);

        Assert.Equal(AnimationState.Faulted, await run.Completion);
        var aggregate = Assert.IsType<AggregateException>(run.Exception);
        Assert.Collection(
            aggregate.InnerExceptions,
            error => Assert.Contains("at least one keyframe", error.Message, StringComparison.Ordinal),
            error => Assert.Contains("at least one keyframe", error.Message, StringComparison.Ordinal));
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public async Task ParallelCancellationPropagatesToEveryActiveChild()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var first = new CallbackAnimation((_, _) => { }) { Duration = TimeSpan.FromMilliseconds(100) };
        var second = new CallbackAnimation((_, _) => { }) { Duration = TimeSpan.FromMilliseconds(200) };
        AnimationRun run = Animation.Parallel(first, second).Start(target, harness.Scheduler);

        Assert.Equal(2, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        run.Cancel();

        Assert.Equal(AnimationState.Canceled, await run.Completion);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(2, harness.Scheduler.GetDiagnostics().CanceledCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public async Task TimelineStartsEntriesOnceAtMonotonicOffsets()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var starts = new List<string>();
        var timeline = new AnimationTimeline()
            .At(TimeSpan.Zero, Immediate(_ => starts.Add("zero")))
            .At(TimeSpan.FromMilliseconds(100), Immediate(_ => starts.Add("hundred")));

        AnimationRun run = timeline.Start(target, harness.Scheduler);
        Assert.Equal(["zero"], starts);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(99));
        Assert.Equal(["zero"], starts);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(1));
        Assert.Equal(AnimationState.Completed, await run.Completion);
        Assert.Equal(["zero", "hundred"], starts);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public async Task TimelineCancellationPreventsDelayedEntriesFromStarting()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        int delayedStarts = 0;
        var timeline = new AnimationTimeline()
            .At(TimeSpan.FromMilliseconds(100), Immediate(_ => delayedStarts++))
            .At(TimeSpan.FromMilliseconds(200), Immediate(_ => delayedStarts++));
        AnimationRun run = timeline.Start(target, harness.Scheduler);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        run.Cancel();

        Assert.Equal(AnimationState.Canceled, await run.Completion);
        Assert.Equal(0, delayedStarts);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public async Task TimelineAggregatesEntrySetupFaultsInDeclarationOrder()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var timeline = new AnimationTimeline()
            .At(TimeSpan.Zero, KeyframeAnimation<float>.Create(target, _ => { }))
            .At(TimeSpan.Zero, KeyframeAnimation<float>.Create(target, _ => { }));

        AnimationRun run = timeline.Start(harness.Scheduler);

        Assert.Equal(AnimationState.Faulted, await run.Completion);
        var aggregate = Assert.IsType<AggregateException>(run.Exception);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.All(
            aggregate.InnerExceptions,
            error => Assert.Contains("at least one keyframe", error.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RepeatAndAutoReverseApplyExactEndpointsWithoutAccumulatingHandles()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var progress = new List<float>();
        AnimationDefinition animation = Immediate(value => progress.Add(value));
        animation.Repeat(3).AutoReverse();

        AnimationState state = await animation.RunAsync(target, harness.Scheduler);

        Assert.Equal(AnimationState.Completed, state);
        Assert.Equal([1f, 0f, 1f, 0f, 1f, 0f], progress);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public async Task InfiniteRepeatCollapsesUnderReducedMotion()
    {
        using var harness = new AnimationSchedulerTestHarness();
        harness.Policy.ReducedMotion = true;
        var target = new Control();
        int updates = 0;
        AnimationDefinition animation = Immediate(_ => updates++);
        animation.RepeatForever();

        AnimationState state = await animation.RunAsync(target, harness.Scheduler);

        Assert.Equal(AnimationState.Completed, state);
        Assert.Equal(1, updates);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public async Task InfiniteRepeatEndsThroughCancellationAndLeavesSchedulerIdle()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var animation = new CallbackAnimation((_, _) => { })
        {
            Duration = TimeSpan.FromMilliseconds(100)
        };
        animation.RepeatForever();

        AnimationRun run = animation.Start(new Control(), harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        run.Cancel();

        Assert.Equal(AnimationState.Canceled, await run.Completion);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public async Task EmptyCompositionCannotEnterATightInfiniteRepeatLoop()
    {
        using var harness = new AnimationSchedulerTestHarness();
        AnimationDefinition empty = Animation.Sequence().RepeatForever();

        AnimationRun run = empty.Start(harness.Scheduler);

        Assert.Equal(AnimationState.Faulted, await run.Completion);
        Assert.IsType<InvalidOperationException>(run.Exception);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void KeyframesValidateOrderDuplicatesAndFiniteInput()
    {
        var animation = KeyframeAnimation<float>
            .Create(new Control(), _ => { })
            .Keyframe(0f, 0f)
            .Keyframe(0.5f, 1f);

        Assert.Throws<ArgumentException>(() => animation.Keyframe(0.25f, 2f));
        Assert.Throws<ArgumentException>(() => animation.Keyframe(0.5f, 2f));
        Assert.Throws<ArgumentOutOfRangeException>(() => animation.Keyframe(float.NaN, 2f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => KeyframeAnimation<float>.Create(new Control(), _ => { }).Keyframe(0f, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => animation.Sample(float.PositiveInfinity));
    }

    [Fact]
    public void KeyframesSampleEndpointsSegmentsDuplicatesAndCustomInterpolation()
    {
        var target = new Control();
        float applied = -1f;
        var animation = KeyframeAnimation<float>
            .Create(target, value => applied = value, new OffsetInterpolator())
            .Keyframe(0f, 0f)
            .Keyframe(0.5f, 10f, static progress => progress * progress);
        animation.DuplicatePositionPolicy = KeyframeDuplicatePositionPolicy.KeepBoth;
        animation
            .Keyframe(0.5f, 20f)
            .Keyframe(1f, 30f);

        Assert.Equal(0f, animation.Sample(0f));
        Assert.Equal(3.5f, animation.Sample(0.25f));
        Assert.Equal(20f, animation.Sample(0.5f));
        Assert.Equal(26f, animation.Seek(0.75f));
        Assert.Equal(26f, applied);
        Assert.Equal(30f, animation.Sample(1f));
    }

    [Fact]
    public void KeyframesEnforceLimitReplaceDuplicatesAndRejectInvalidSegmentEasing()
    {
        var target = new Control();
        var replacement = KeyframeAnimation<float>
            .Create(target, _ => { });
        replacement.DuplicatePositionPolicy = KeyframeDuplicatePositionPolicy.ReplacePrevious;
        replacement
            .Keyframe(0f, 1f)
            .Keyframe(0f, 2f)
            .Keyframe(1f, 3f, static _ => float.NaN);

        Assert.Equal(2, replacement.Count);
        Assert.Equal(2f, replacement.Sample(0f));
        Assert.Throws<InvalidOperationException>(() => replacement.Sample(0.5f));

        var maximum = KeyframeAnimation<float>.Create(target, _ => { });
        for (int index = 0; index < KeyframeAnimation<float>.MaximumKeyframeCount; index++)
            maximum.Keyframe(index / (float)(KeyframeAnimation<float>.MaximumKeyframeCount - 1), index);
        Assert.Throws<InvalidOperationException>(() => maximum.Keyframe(1f, 999f));

        var invalidInterpolation = KeyframeAnimation<float>
            .Create(target, _ => { }, new NonFiniteInterpolator())
            .Keyframe(0f, 0f)
            .Keyframe(1f, 1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => invalidInterpolation.Sample(0.5f));
    }

    [Fact]
    public async Task NestedGroupCancellationDoesNotCancelUnrelatedTargetWork()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var child = new CallbackAnimation((_, _) => { }) { Duration = TimeSpan.FromMilliseconds(100) };
        AnimationRun group = Animation.Parallel(
            Animation.Sequence(child),
            new CallbackAnimation((_, _) => { }) { Duration = TimeSpan.FromMilliseconds(100) })
            .Start(target, harness.Scheduler);
        AnimationHandle unrelated = harness.Scheduler.Start(
            target,
            "Unrelated",
            _ => { },
            new AnimationOptions { Duration = TimeSpan.FromMilliseconds(100) });

        group.Dispose();

        Assert.Equal(AnimationState.Canceled, await group.Completion);
        Assert.Equal(AnimationState.Running, unrelated.State);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        unrelated.Cancel();
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public async Task CancelingIgnoredDefinitionRunDoesNotCancelExistingChannel()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var options = new AnimationOptions
        {
            Duration = TimeSpan.FromMilliseconds(100),
            ReplacementMode = AnimationReplacementMode.IgnoreNew
        };
        AnimationRun existing = target.FadeTo(0.5f, options).Start(harness.Scheduler);
        AnimationRun ignored = target.FadeTo(0.25f, options).Start(harness.Scheduler);

        ignored.Cancel();

        Assert.Equal(AnimationState.Canceled, await ignored.Completion);
        Assert.Equal(AnimationState.Running, existing.State);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(AnimationState.Completed, await existing.Completion);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public async Task IgnoredRepeatedDefinitionDoesNotTakeOverAfterExistingCompletion()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var options = new AnimationOptions
        {
            Duration = TimeSpan.FromMilliseconds(100),
            ReplacementMode = AnimationReplacementMode.IgnoreNew
        };
        AnimationRun existing = target.FadeTo(0.5f, options).Start(harness.Scheduler);
        AnimationDefinition ignoredDefinition = target.FadeTo(0.25f, options).Repeat(3);
        AnimationRun ignored = ignoredDefinition.Start(harness.Scheduler);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(AnimationState.Completed, await existing.Completion);
        Assert.Equal(AnimationState.Completed, await ignored.Completion);
        Assert.Equal(0.5f, target.Opacity, 3);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public async Task BoundMultiAxisFactoryKeepsStableReplacementChannelsAcrossRuns()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var target = new Control();
        var slow = new AnimationOptions { Duration = TimeSpan.FromMilliseconds(200) };
        var fast = new AnimationOptions { Duration = TimeSpan.FromMilliseconds(100) };
        AnimationRun stale = target.TranslateTo(100f, 200f, slow).Start(harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        AnimationRun latest = target.TranslateTo(20f, 40f, fast).Start(harness.Scheduler);

        Assert.Equal(AnimationState.Canceled, await stale.Completion);
        Assert.Equal(2, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(AnimationState.Completed, await latest.Completion);
        Assert.Equal(20f, target.TranslationX, 3);
        Assert.Equal(40f, target.TranslationY, 3);
        Assert.False(harness.TickSource.IsRunning);
    }

    private static CallbackAnimation Immediate(Action<float> update)
        => new((_, progress) => update(progress)) { Duration = TimeSpan.Zero };

    private sealed class CallbackAnimation(Action<AnimationContext, float> update) : AnimationDefinition
    {
        public AnimationContext? LastContext { get; private set; }

        protected override void Update(AnimationContext context, float progress)
        {
            LastContext = context;
            update(context, progress);
        }
    }

    private sealed class OffsetInterpolator : IAnimationInterpolator<float>
    {
        public float Interpolate(float from, float to, float progress)
            => from + ((to - from) * progress) + (progress is > 0f and < 1f ? 1f : 0f);
    }

    private sealed class NonFiniteInterpolator : IAnimationInterpolator<float>
    {
        public float Interpolate(float from, float to, float progress) => float.NaN;
    }

    private sealed class TestAnimationException(string message) : Exception(message);
}
