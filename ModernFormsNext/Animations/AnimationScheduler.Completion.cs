namespace ModernFormsNext.Animations;

public sealed partial class AnimationScheduler
{
    private readonly Queue<Action> frameworkContinuations = new();
    private bool runningFrameworkContinuations;

    internal bool CheckFrameworkAccess() => dispatcher.CheckAccess();

    internal void RunFrameworkContinuation(Action continuation, Action<Exception> dispatchFailed)
    {
        bool hasAccess;
        try
        {
            hasAccess = dispatcher.CheckAccess();
            if (!hasAccess)
            {
                // Cancellation may originate on any thread. Subsequent framework legs and their
                // zero-duration update callbacks must still enter the owning UI dispatcher.
                // Posting establishes dispatcher ownership. Do not check and repost from inside
                // the callback: existing adapters may execute Post inline without changing their
                // CheckAccess result, just as the scheduler's normal tick dispatch permits.
                dispatcher.Post(() => ExecuteFrameworkContinuation(continuation));
                return;
            }
        }
        catch (Exception exception)
        {
            // A disposed host can reject either access checking or posting. The private awaiter
            // resumes only its fault/cleanup path, never a subsequent UI update.
            dispatchFailed(exception);
            return;
        }

        ExecuteFrameworkContinuation(continuation);
    }

    private void ExecuteFrameworkContinuation(Action continuation)
    {
        // Production dispatchers enter on their UI thread. Also serialize legacy inline adapters
        // used by internal tests, whose Post can enter from competing threads. Nested compositions
        // append work instead of recursively completing parents. Callbacks run outside every lock.
        lock (frameworkContinuations)
        {
            frameworkContinuations.Enqueue(continuation);
            if (runningFrameworkContinuations)
                return;
            runningFrameworkContinuations = true;
        }
        try
        {
            while (true)
            {
                Action next;
                lock (frameworkContinuations)
                {
                    if (!frameworkContinuations.TryDequeue(out next!))
                    {
                        runningFrameworkContinuations = false;
                        return;
                    }
                }
                next();
            }
        }
        catch
        {
            lock (frameworkContinuations)
                runningFrameworkContinuations = false;
            throw;
        }
    }
}
