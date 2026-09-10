namespace ModernFormsNext.Animations;

public sealed partial class AnimationScheduler
{
    private static readonly object testingDefaultLock = new();
    private static TestingDefaultScope? testingDefaultScope;

    // The production dispatcher is process-scoped, and headless hosts are serialized. Match that
    // lifetime here so background starts use the host dispatcher, while captured ExecutionContexts
    // cannot keep resolving a disposed scheduler after teardown. The ordinary lazy default is
    // neither initialized nor replaced by entering or leaving this scope.
    internal static IDisposable PushDefaultForTesting(AnimationScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        lock (testingDefaultLock)
        {
            if (testingDefaultScope is not null)
                throw new InvalidOperationException("A deterministic ModernFormsNext animation scheduler is already active in this process.");

            var scope = new TestingDefaultScope(scheduler);
            Volatile.Write(ref testingDefaultScope, scope);
            return scope;
        }
    }

    private static AnimationScheduler? GetTestingDefault()
        => Volatile.Read(ref testingDefaultScope)?.Scheduler;

    private sealed class TestingDefaultScope(AnimationScheduler scheduler) : IDisposable
    {
        public AnimationScheduler Scheduler { get; } = scheduler;
        private bool disposed;

        public void Dispose()
        {
            lock (testingDefaultLock)
            {
                if (disposed)
                    return;
                if (!ReferenceEquals(testingDefaultScope, this))
                    throw new InvalidOperationException("The deterministic animation scheduler scope was replaced before disposal.");

                Volatile.Write(ref testingDefaultScope, null);
                disposed = true;
            }
        }
    }
}
