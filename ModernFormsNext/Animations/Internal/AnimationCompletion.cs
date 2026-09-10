using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace ModernFormsNext.Animations;

/// <summary>
/// Internal terminal signal for framework animation bookkeeping. Public completion remains a Task
/// with asynchronous continuations; this signal never accepts application continuations.
/// </summary>
/// <remarks>
/// Task awaiters may defer even a non-asynchronous TCS continuation when a dispatcher synchronization
/// context is current. Direct registrations keep the next composition leg on the same frame, with
/// dispatch and recursion control supplied explicitly by the owning animation scheduler.
/// </remarks>
[AsyncMethodBuilder(typeof(AnimationCompletionMethodBuilder<>))]
internal sealed class AnimationCompletion<T>
{
    private readonly object sync = new();
    private List<Registration>? registrations;
    private bool completed;
    private T? result;
    private ExceptionDispatchInfo? exception;

    internal bool IsCompleted { get { lock (sync) return completed; } }

    internal T GetResult()
    {
        lock (sync)
        {
            if (!completed)
                throw new InvalidOperationException("Animation bookkeeping has not completed.");
            exception?.Throw();
            return result!;
        }
    }

    internal bool TrySetResult(T value) => Complete(value, null);

    internal void SetException(Exception fault) => Complete(default, ExceptionDispatchInfo.Capture(fault));

    private bool Complete(T? value, ExceptionDispatchInfo? fault)
    {
        List<Registration>? callbacks;
        lock (sync)
        {
            if (completed)
                return false;
            result = value;
            exception = fault;
            completed = true;
            callbacks = registrations;
            registrations = null;
        }

        // Scheduler entries have already released their channels and callbacks before publishing.
        // Neither this signal's lock nor the scheduler's lock is held while a next leg starts.
        if (callbacks is not null)
            foreach (Registration registration in callbacks)
                registration.Invoke();
        return true;
    }

    internal IDisposable Register(Action continuation)
    {
        var registration = new Registration(this, continuation);
        lock (sync)
        {
            if (!completed)
            {
                (registrations ??= []).Add(registration);
                return registration;
            }
        }
        registration.Invoke();
        return registration;
    }

    internal Awaiter On(AnimationScheduler scheduler) => new(this, scheduler);

    internal sealed class Awaiter(AnimationCompletion<T> completion, AnimationScheduler scheduler)
        : ICriticalNotifyCompletion
    {
        private ExceptionDispatchInfo? dispatchFailure;
        private int resumed;
        public Awaiter GetAwaiter() => this;
        public bool IsCompleted => completion.IsCompleted && scheduler.CheckFrameworkAccess();
        public T GetResult()
        {
            Volatile.Read(ref dispatchFailure)?.Throw();
            return completion.GetResult();
        }
        public void OnCompleted(Action continuation) => UnsafeOnCompleted(continuation);
        public void UnsafeOnCompleted(Action continuation)
        {
            completion.Register(() => scheduler.RunFrameworkContinuation(
                () => Resume(continuation),
                exception =>
                {
                    // A rejected dispatcher cannot run another UI leg. Resume this private await
                    // only to throw and unwind finally blocks, releasing targets and parent runs.
                    Volatile.Write(ref dispatchFailure, ExceptionDispatchInfo.Capture(exception));
                    Resume(continuation);
                }));
        }

        private void Resume(Action continuation)
        {
            if (Interlocked.Exchange(ref resumed, 1) == 0)
                continuation();
        }
    }

    private sealed class Registration(AnimationCompletion<T> source, Action continuation) : IDisposable
    {
        private Action? callback = continuation;
        internal void Invoke() => Interlocked.Exchange(ref callback, null)?.Invoke();
        public void Dispose()
        {
            Interlocked.Exchange(ref callback, null);
            lock (source.sync)
                source.registrations?.Remove(this);
        }
    }
}

/// <summary>
/// Reuses the runtime's state-machine/ExecutionContext handling, while publishing the internal
/// direct signal instead of exposing the builder's Task to animation bookkeeping.
/// </summary>
internal struct AnimationCompletionMethodBuilder<T>
{
    private AsyncTaskMethodBuilder<T> core;
    private AnimationCompletion<T> completion;

    public static AnimationCompletionMethodBuilder<T> Create() => new()
    {
        core = AsyncTaskMethodBuilder<T>.Create(),
        completion = new AnimationCompletion<T>()
    };

    public AnimationCompletion<T> Task => completion;
    public void SetResult(T result)
    {
        core.SetResult(result);
        completion.TrySetResult(result);
    }

    public void SetException(Exception exception)
    {
        // The runtime box is private and has no observers. Complete it successfully so it releases
        // its captured state without creating an unobserved Task exception; the signal owns faults.
        core.SetResult(default!);
        completion.SetException(exception);
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine) => core.SetStateMachine(stateMachine);
    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        => core.Start(ref stateMachine);
    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine
        => core.AwaitOnCompleted(ref awaiter, ref stateMachine);
    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine
        => core.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
}
