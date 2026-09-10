using ModernFormsNext.Animations;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class TestClockCompositionTests
{
    private static readonly TimeSpan HalfSecond = TimeSpan.FromMilliseconds(500);

    [Fact]
    public void SequenceStartsNextLegAtTheCompletedFrameTime()
    {
        using var host = ModernFormsTestHost.Create();
        var target = new Control();
        host.Show(target);
        List<float> first = [];
        List<float> second = [];
        using AnimationRun run = Animation.Sequence(
            Probe("first", first.Add), Probe("second", second.Add)).Start(target);

        host.Clock.Advance(HalfSecond);
        Assert.Equal(1f, Assert.Single(first));
        Assert.Empty(second);
        Assert.Equal(1, host.GetDiagnostics().ActiveAnimationCount);
        host.Clock.Advance(HalfSecond / 2);
        Assert.Equal(.5f, Assert.Single(second));
        host.Clock.Advance(HalfSecond / 2);

        Assert.Equal(new[] { .5f, 1f }, second);
        Assert.Equal(AnimationState.Completed, run.State);
        Assert.True(run.Completion.IsCompletedSuccessfully);
        Assert.Equal(0, host.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void ParallelFramesAndTerminalStateUseTheSameClock()
    {
        using var host = ModernFormsTestHost.Create();
        var target = new Control();
        host.Show(target);
        List<float> first = [];
        List<float> second = [];
        using AnimationRun run = Animation.Parallel(
            Probe("first", first.Add), Probe("second", second.Add, HalfSecond * 2)).Start(target);

        host.Clock.Advance(HalfSecond);
        Assert.Equal(1f, Assert.Single(first));
        Assert.Equal(.5f, Assert.Single(second));
        Assert.Equal(AnimationState.Running, run.State);
        host.Clock.Advance(HalfSecond);

        Assert.Equal(new[] { .5f, 1f }, second);
        Assert.Equal(AnimationState.Completed, run.State);
        Assert.True(run.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public void RepeatAndReverseDoNotRequireBackgroundContinuationTiming()
    {
        using var host = ModernFormsTestHost.Create();
        var target = new Control();
        host.Show(target);
        List<float> values = [];
        using AnimationRun run = Probe("repeat", values.Add).Repeat(2).AutoReverse().Start(target);

        for (int index = 0; index < 4; index++)
            host.Clock.Advance(HalfSecond);

        Assert.Equal(new[] { 1f, 0f, 1f, 0f }, values);
        Assert.Equal(AnimationState.Completed, run.State);
        Assert.Equal(0, host.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void NestedParallelSequenceStartsFollowingLegOnTheUiThread()
    {
        using var host = ModernFormsTestHost.Create();
        var target = new Control();
        host.Show(target);
        List<string> values = [];
        int ownerThread = Environment.CurrentManagedThreadId;
        using AnimationRun run = Animation.Sequence(
            Animation.Parallel(Probe("a", _ => values.Add("a")), Probe("b", _ => values.Add("b"))),
            Probe("c", _ =>
            {
                Assert.Equal(ownerThread, Environment.CurrentManagedThreadId);
                values.Add("c");
            })).Start(target);

        host.Clock.Advance(HalfSecond);
        host.Clock.Advance(HalfSecond);

        Assert.Equal(new[] { "a", "b", "c" }, values);
        Assert.Equal(AnimationState.Completed, run.State);
    }

    [Fact]
    public void CancellationFinishesCompositionAndNeverStartsItsLaterChild()
    {
        using var host = ModernFormsTestHost.Create();
        var target = new Control();
        host.Show(target);
        var laterStarted = false;
        using AnimationRun run = Animation.Sequence(
            Probe("first", _ => { }), Probe("second", _ => laterStarted = true)).Start(target);

        host.Clock.Advance(HalfSecond / 2);
        run.Cancel();
        host.Clock.Advance(HalfSecond * 2);

        Assert.Equal(AnimationState.Canceled, run.State);
        Assert.True(run.Completion.IsCompletedSuccessfully);
        Assert.False(laterStarted);
        Assert.Equal(0, host.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void TimelineStartsOffsetLegAtItsDeterministicDeadline()
    {
        using var host = ModernFormsTestHost.Create();
        var target = new Control();
        host.Show(target);
        List<string> values = [];
        using AnimationRun run = new AnimationTimeline()
            .At(TimeSpan.Zero, Probe("first", _ => values.Add("first")))
            .At(HalfSecond, Probe("second", _ => values.Add("second")))
            .Start(target);

        host.Clock.Advance(HalfSecond);
        Assert.Equal("first", Assert.Single(values));
        Assert.Equal(1, host.GetDiagnostics().ActiveAnimationCount);
        host.Clock.Advance(HalfSecond);

        Assert.Equal(new[] { "first", "second" }, values);
        Assert.Equal(AnimationState.Completed, run.State);
    }

    [Fact]
    public void SequenceAlsoAdvancesUnderTheProductionDispatcherSynchronizationContext()
    {
        using var host = ModernFormsTestHost.Create();
        var target = new Control();
        host.Show(target);
        List<string> values = [];
        using AnimationRun run = Animation.Sequence(
            Probe("first", _ => values.Add("first")), Probe("second", _ => values.Add("second"))).Start(target);
        host.Dispatcher.Post(() =>
        {
            Assert.NotNull(SynchronizationContext.Current);
            host.Clock.Advance(HalfSecond);
            Assert.Equal(1, host.GetDiagnostics().ActiveAnimationCount);
            host.Clock.Advance(HalfSecond);
            Assert.Equal(AnimationState.Completed, run.State);
        });

        host.Dispatcher.Drain();
        host.Dispatcher.ThrowUnhandledExceptions();

        Assert.Equal(new[] { "first", "second" }, values);
    }

    [Fact]
    public void BackgroundCancellationPublishesCompositionCleanupOnTheOwningDispatcher()
    {
        using var host = ModernFormsTestHost.Create();
        using var target = new Control();
        bool laterStarted = false;
        using AnimationRun run = Animation.Sequence(
            Probe("first", _ => { }), Probe("second", _ => laterStarted = true)).Start(target);

        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { run.Cancel(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        });
        thread.Start();
        thread.Join();
        failure?.Throw();

        Assert.Equal(AnimationState.Canceled, run.State);
        Assert.False(run.Completion.IsCompleted);
        host.Dispatcher.Drain();
        Assert.True(run.Completion.IsCompletedSuccessfully);
        Assert.False(laterStarted);
        Assert.Equal(0, host.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void CancelingIgnoredRunDetachesItsWaitWithoutCancelingTheExistingChannel()
    {
        using var host = ModernFormsTestHost.Create();
        using var target = new Control();
        List<float> originalValues = [];
        using AnimationRun original = Probe("shared", originalValues.Add).Start(target);
        var ignoredDefinition = Probe("shared", _ => throw new InvalidOperationException("Ignored callback ran."));
        ignoredDefinition.ReplacementMode = AnimationReplacementMode.IgnoreNew;
        using AnimationRun ignored = ignoredDefinition.Repeat(2).Start(target);

        ignored.Cancel();
        host.Clock.Advance(HalfSecond);

        Assert.Equal(AnimationState.Canceled, ignored.State);
        Assert.True(ignored.Completion.IsCompletedSuccessfully);
        Assert.Equal(AnimationState.Completed, original.State);
        Assert.Equal(1f, Assert.Single(originalValues));
    }

    [Fact]
    public void NextImmediateChildObservesReleasedChannelAndCanScheduleTheSameOwner()
    {
        using var host = ModernFormsTestHost.Create();
        using var target = new Control();
        AnimationHandle? replacement = null;
        using AnimationRun run = Animation.Sequence(
            Probe("first", _ => { }),
            Probe("next", _ =>
            {
                Assert.Equal(0, host.GetDiagnostics().ActiveAnimationCount);
                replacement = AnimationScheduler.Default.Start(target, "first", _ => { },
                    new AnimationOptions { Duration = HalfSecond });
            }, TimeSpan.Zero)).Start(target);

        host.Clock.Advance(HalfSecond);

        Assert.Equal(AnimationState.Completed, run.State);
        Assert.NotNull(replacement);
        Assert.Equal(AnimationState.Running, replacement.State);
        Assert.Equal(1, host.GetDiagnostics().ActiveAnimationCount);
        host.Clock.Advance(HalfSecond);
        Assert.Equal(AnimationState.Completed, replacement.State);
    }

    [Fact]
    public void PropertyAndKeyframeDefinitionsAdvanceThroughTheSameCompositionPath()
    {
        using var host = ModernFormsTestHost.Create();
        using var target = new Control();
        List<float> propertyValues = [];
        List<float> keyframeValues = [];
        var property = new PropertyAnimation<float>(
            target, "property", 0f, 10f, AnimationInterpolators.Float, propertyValues.Add)
        { Duration = HalfSecond, Easing = Easings.Linear };
        var keyframes = KeyframeAnimation<float>.Create(target, keyframeValues.Add)
            .Keyframe(0f, 10f).Keyframe(1f, 20f);
        keyframes.Duration = HalfSecond;
        using AnimationRun run = Animation.Sequence(property, keyframes).Start(target);

        host.Clock.Advance(HalfSecond / 2);
        host.Clock.Advance(HalfSecond / 2);
        host.Clock.Advance(HalfSecond / 2);
        host.Clock.Advance(HalfSecond / 2);

        Assert.Equal(new[] { 5f, 10f }, propertyValues);
        Assert.Equal(new[] { 15f, 20f }, keyframeValues);
        Assert.Equal(AnimationState.Completed, run.State);
    }

    [Fact]
    public void RetainedCompletedRunDoesNotRetainItsCompositionTarget()
    {
        using var host = ModernFormsTestHost.Create();
        (AnimationRun run, WeakReference target) = CreateCompletedRun(host);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(target.IsAlive);
        Assert.Equal(AnimationState.Completed, run.State);
        GC.KeepAlive(run);
    }

    [Fact]
    public async Task PublicCompletionDoesNotInlineApplicationContinuationOnTheTickThread()
    {
        Task<bool> continuation;
        using (var host = ModernFormsTestHost.Create())
        {
            int tickThread = Environment.CurrentManagedThreadId;
            int advancing = 1;
            AnimationHandle handle = AnimationScheduler.Default.Start(new object(), "public-completion", _ => { },
                new AnimationOptions { Duration = HalfSecond });
            continuation = handle.Completion.ContinueWith(
                _ => Environment.CurrentManagedThreadId == tickThread && Volatile.Read(ref advancing) != 0,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            try { host.Clock.Advance(HalfSecond); }
            finally { Volatile.Write(ref advancing, 0); }
            Assert.True(handle.Completion.IsCompletedSuccessfully);
        }

        // The host is disposed before awaiting, so xUnit may resume this assertion on any thread.
        Assert.False(await continuation);
    }

    private static ProgressProbe Probe(string key, Action<float> update, TimeSpan? duration = null)
        => new(update) { Key = key, Duration = duration ?? HalfSecond, Easing = Easings.Linear };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (AnimationRun Run, WeakReference Target) CreateCompletedRun(ModernFormsTestHost host)
    {
        using var target = new Control();
        var reference = new WeakReference(target);
        AnimationRun run = Animation.Sequence(Probe("a", _ => { }), Probe("b", _ => { })).Start(target);
        host.Clock.Advance(HalfSecond);
        host.Clock.Advance(HalfSecond);
        return (run, reference);
    }

    private sealed class ProgressProbe(Action<float> update) : AnimationDefinition
    {
        protected override void Update(AnimationContext context, float progress) => update(progress);
    }
}
