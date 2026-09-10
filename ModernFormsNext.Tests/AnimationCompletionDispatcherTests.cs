using ModernFormsNext.Animations;
using System.Runtime.ExceptionServices;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class AnimationCompletionDispatcherTests
{
    [Fact]
    public void InlinePostingAdapterDoesNotRepostRecursivelyWhenAccessCheckRemainsFalse()
    {
        var dispatcher = new InlinePostingDispatcher();
        using var scheduler = new AnimationScheduler(
            new ManualAnimationClock(), dispatcher, new ManualAnimationTickSource(), new AnimationPolicy());
        using var target = new Control();
        bool laterStarted = false;
        using AnimationRun run = Animation.Sequence(new Probe(_ => { }), new Probe(_ => laterStarted = true))
            .Start(target, scheduler);
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
        Assert.True(run.Completion.IsCompletedSuccessfully);
        Assert.InRange(dispatcher.PostCount, 1, 16);
        Assert.False(laterStarted);
        Assert.Equal(0, scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RejectedDispatcherUnwindsEveryCompositionAndReleasesTerminalEntries(
        bool rejectAccessCheck, bool shutdown)
    {
        var clock = new ManualAnimationClock();
        var ticks = new ManualAnimationTickSource();
        var dispatcher = new RejectingDispatcher();
        using var scheduler = new AnimationScheduler(clock, dispatcher, ticks, new AnimationPolicy());
        using var target = new Control();
        List<AnimationContext> contexts = [];
        bool laterStarted = false;
        using AnimationRun first = Animation.Sequence(
            new Probe(contexts.Add), new Probe(_ => laterStarted = true)).Start(target, scheduler);
        using AnimationRun second = Animation.Parallel(new Probe(contexts.Add), new Probe(contexts.Add))
            .Start(target, scheduler);
        clock.Advance(TimeSpan.FromMilliseconds(125));
        ticks.Fire();
        Assert.Equal(3, contexts.Count);
        dispatcher.Reject = true;
        dispatcher.RejectAccessCheck = rejectAccessCheck;

        if (shutdown)
            scheduler.Shutdown();
        else
            scheduler.CancelAll(target);

        Assert.True(first.Completion.IsCompletedSuccessfully);
        Assert.True(second.Completion.IsCompletedSuccessfully);
        Assert.Equal(AnimationState.Faulted, first.State);
        Assert.Equal(AnimationState.Faulted, second.State);
        Assert.Equal(0, scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(laterStarted);
        foreach (AnimationContext context in contexts)
            Assert.Throws<ObjectDisposedException>(() => context.Target);
        if (shutdown)
            Assert.True(ticks.IsDisposed);
    }

    private sealed class RejectingDispatcher : IAnimationDispatcher
    {
        internal bool Reject { get; set; }
        internal bool RejectAccessCheck { get; set; }
        public bool CheckAccess()
        {
            if (Reject && RejectAccessCheck)
                throw new ObjectDisposedException("test dispatcher");
            return !Reject;
        }

        public void Post(Action action)
        {
            if (Reject)
                throw new ObjectDisposedException("test dispatcher");
            action();
        }
    }

    private sealed class InlinePostingDispatcher : IAnimationDispatcher
    {
        private readonly int ownerThread = Environment.CurrentManagedThreadId;
        private int postCount;
        internal int PostCount => Volatile.Read(ref postCount);
        public bool CheckAccess() => Environment.CurrentManagedThreadId == ownerThread;
        public void Post(Action action)
        {
            Interlocked.Increment(ref postCount);
            action();
        }
    }

    private sealed class Probe(Action<AnimationContext> update) : AnimationDefinition
    {
        protected override void Update(AnimationContext context, float progress) => update(context);
    }
}
