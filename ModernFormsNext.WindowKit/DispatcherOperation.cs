using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ModernFormsNext.WindowKit.Threading;

/// <summary>
/// Represents a unit of work queued to a <see cref="Dispatcher"/>.
/// </summary>
/// <remarks>
/// A dispatcher operation can be awaited, aborted while it is still pending, or waited on
/// synchronously. Synchronous waits from the dispatcher thread are restricted to avoid
/// deadlocking the UI loop.
/// </remarks>
public class DispatcherOperation
{
    /// <summary>
    /// Indicates whether exceptions thrown by the callback should be rethrown on the dispatcher thread.
    /// </summary>
    protected readonly bool ThrowOnUiThread;

    /// <summary>
    /// Gets the current lifecycle state of the operation.
    /// </summary>
    public DispatcherOperationStatus Status { get; protected set; }

    /// <summary>
    /// Gets the dispatcher that owns this operation.
    /// </summary>
    public Dispatcher Dispatcher { get; }

    /// <summary>
    /// Gets or sets the dispatcher priority for the operation.
    /// </summary>
    /// <remarks>
    /// Changing the priority of a queued operation asks the owning dispatcher to reposition it
    /// according to the new priority.
    /// </remarks>
    public DispatcherPriority Priority
    {
        get => _priority;
        set
        {
            _priority = value;
            // Dispatcher is null in ctor
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            Dispatcher?.SetPriority(this, value);
        }
    }

    /// <summary>
    /// Stores the delegate invoked when the operation runs.
    /// </summary>
    /// <remarks>
    /// Derived operation types own the concrete delegate type and cast it during invocation.
    /// </remarks>
    protected object? Callback;

    /// <summary>
    /// Stores the task completion source used to represent asynchronous completion.
    /// </summary>
    protected object? TaskSource;
    
    internal DispatcherOperation? SequentialPrev { get; set; }
    internal DispatcherOperation? SequentialNext { get; set; }
    internal DispatcherOperation? PriorityPrev { get; set; }
    internal DispatcherOperation? PriorityNext { get; set; }
    internal PriorityChain? Chain { get; set; }
    
    internal bool IsQueued => Chain != null;

    private EventHandler? _aborted;
    private EventHandler? _completed;
    private DispatcherPriority _priority;

    internal DispatcherOperation(Dispatcher dispatcher, DispatcherPriority priority, Action callback, bool throwOnUiThread) :
        this(dispatcher, priority, throwOnUiThread)
    {
        Callback = callback;
    }

    private protected DispatcherOperation(Dispatcher dispatcher, DispatcherPriority priority, bool throwOnUiThread)
    {
        ThrowOnUiThread = throwOnUiThread;
        Priority = priority;
        Dispatcher = dispatcher;
    }

    /// <summary>
    ///     An event that is raised when the operation is aborted or canceled.
    /// </summary>
    public event EventHandler Aborted
    {
        add
        {
            lock (Dispatcher.InstanceLock)
            {
                _aborted += value;
            }
        }

        remove
        {
            lock(Dispatcher.InstanceLock)
            {
                _aborted -= value;
            }
        }
    }

    /// <summary>
    ///     An event that is raised when the operation completes.
    /// </summary>
    /// <remarks>
    ///     Completed indicates that the operation was invoked and has
    ///     either completed successfully or faulted. Note that a canceled
    ///     or aborted operation is never is never considered completed.
    /// </remarks>
    public event EventHandler Completed
    {
        add
        {
            lock (Dispatcher.InstanceLock)
            {
                _completed += value;
            }
        }
        
        remove
        {
            lock(Dispatcher.InstanceLock)
            {
                _completed -= value;
            }
        }
    }
    
    /// <summary>
    /// Attempts to abort the operation before it starts executing.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the pending operation was aborted; otherwise,
    /// <see langword="false"/> when it had already started, completed, or been aborted.
    /// </returns>
    public bool Abort()
    {
        lock (Dispatcher.InstanceLock)
        {
            if (Status != DispatcherOperationStatus.Pending)
                return false;
            Dispatcher.Abort(this);
            return true;
        }
    }

    /// <summary>
    ///     Waits for this operation to complete.
    /// </summary>
    /// <returns>
    ///     The status of the operation.  To obtain the return value
    ///     of the invoked delegate, use the the Result property.
    /// </returns>
    public void Wait() => Wait(TimeSpan.FromMilliseconds(-1));

    /// <summary>
    ///     Waits for this operation to complete.
    /// </summary>
    /// <param name="timeout">
    ///     The maximum amount of time to wait.
    /// </param>
    public void Wait(TimeSpan timeout)
    {
        if ((Status == DispatcherOperationStatus.Pending || Status == DispatcherOperationStatus.Executing) &&
            timeout.TotalMilliseconds != 0)
        {
            if (Dispatcher.CheckAccess())
            {
                if (Status == DispatcherOperationStatus.Executing)
                {
                    // We are the dispatching thread, and the current operation state is
                    // executing, which means that the operation is in the middle of
                    // executing (on this thread) and is trying to wait for the execution
                    // to complete.  Unfortunately, the thread will now deadlock, so
                    // we throw an exception instead.
                    throw new InvalidOperationException("A thread cannot wait on operations already running on the same thread.");
                }
                
                var cts = new CancellationTokenSource();
                EventHandler finishedHandler = delegate
                {
                    cts.Cancel();
                };
                Completed += finishedHandler;
                Aborted += finishedHandler;
                try
                {
                    while (Status == DispatcherOperationStatus.Pending)
                    {
                        if (Dispatcher.SupportsRunLoops)
                        {
                            if (Priority >= DispatcherPriority.MinimumForegroundPriority)
                                Dispatcher.RunJobs(Priority, cts.Token);
                            else
                                Dispatcher.PushFrame(new DispatcherOperationFrame(this, timeout));
                        }
                        else
                            Dispatcher.RunJobs(DispatcherPriority.MinimumActiveValue, cts.Token);
                    }
                }
                finally
                {
                    Completed -= finishedHandler;
                    Aborted -= finishedHandler;
                }
            }
        }
        GetTask().GetAwaiter().GetResult();
    }

    private class DispatcherOperationFrame : DispatcherFrame
    {
        // Note: we pass "exitWhenRequested=false" to the base
        // DispatcherFrame construsctor because we do not want to exit
        // this frame if the dispatcher is shutting down. This is
        // because we may need to invoke operations during the shutdown process.
        public DispatcherOperationFrame(DispatcherOperation op, TimeSpan timeout) : base(false)
        {
            _operation = op;

            // We will exit this frame once the operation is completed or aborted.
            _operation.Aborted += OnCompletedOrAborted;
            _operation.Completed += OnCompletedOrAborted;

            // We will exit the frame if the operation is not completed within
            // the requested timeout.
            if (timeout.TotalMilliseconds > 0)
            {
                _waitTimer = new Timer(_ => Exit(),
                    null,
                    timeout,
                    TimeSpan.FromMilliseconds(-1));
            }

            // Some other thread could have aborted the operation while we were
            // setting up the handlers.  We check the state again and mark the
            // frame as "should not continue" if this happened.
            if (_operation.Status != DispatcherOperationStatus.Pending)
            {
                Exit();
            }
        }

        private void Exit()
        {
            Continue = false;

            if (_waitTimer != null)
            {
                _waitTimer.Dispose();
            }

            _operation.Aborted -= OnCompletedOrAborted;
            _operation.Completed -= OnCompletedOrAborted;
        }

        private void OnCompletedOrAborted(object? sender, EventArgs e) => Exit();

        private DispatcherOperation _operation;
        private Timer? _waitTimer;
    }

    /// <summary>
    /// Gets a task that completes when this operation completes or is aborted.
    /// </summary>
    /// <returns>The task representing the operation lifecycle.</returns>
    public Task GetTask() => GetTaskCore();
    
    /// <summary>
    ///     Returns an awaiter for awaiting the completion of the operation.
    /// </summary>
    /// <remarks>
    ///     This method is intended to be used by compilers.
    /// </remarks>
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public TaskAwaiter GetAwaiter()
    {
        return GetTask().GetAwaiter();
    }

    internal void DoAbort()
    {
        Status = DispatcherOperationStatus.Aborted;
        AbortTask();
        _aborted?.Invoke(this, EventArgs.Empty);
    }
    
    internal void Execute()
    {
        lock (Dispatcher.InstanceLock)
        {
            Status = DispatcherOperationStatus.Executing;
        }

        try
        {
            using (AvaloniaSynchronizationContext.Ensure(Priority))
                InvokeCore();
        }
        finally
        {
            _completed?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>
    /// Invokes the queued callback and completes the operation task.
    /// </summary>
    /// <remarks>
    /// Derived operation types override this method when the callback has a return value or a
    /// different delegate shape.
    /// </remarks>
    protected virtual void InvokeCore()
    {
        try
        {
            ((Action)Callback!)();
            lock (Dispatcher.InstanceLock)
            {
                Status = DispatcherOperationStatus.Completed;
                if (TaskSource is TaskCompletionSource<object?> tcs)
                    tcs.SetResult(null);
            }
        }
        catch (Exception e)
        {
            lock (Dispatcher.InstanceLock)
            {
                Status = DispatcherOperationStatus.Completed;
                if (TaskSource is TaskCompletionSource<object?> tcs)
                    tcs.SetException(e);
            }

            if (ThrowOnUiThread)
                throw;
        }
    }

    internal virtual object? GetResult() => null;
    
    /// <summary>
    /// Transitions the task backing this operation into the canceled state.
    /// </summary>
    protected virtual void AbortTask() => (TaskSource as TaskCompletionSource<object?>)?.SetCanceled();

    private static CancellationToken CreateCancelledToken()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    private static readonly Task s_abortedTask = Task.FromCanceled(CreateCancelledToken());

    /// <summary>
    /// Gets or creates the task that represents completion of this operation.
    /// </summary>
    /// <returns>The task representing operation completion.</returns>
    protected virtual Task GetTaskCore()
    {
        lock (Dispatcher.InstanceLock)
        {
            if (Status == DispatcherOperationStatus.Aborted)
                return s_abortedTask;
            if (Status == DispatcherOperationStatus.Completed)
                return Task.CompletedTask;
            if (TaskSource is not TaskCompletionSource<object?> tcs)
                TaskSource = tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return tcs.Task;
        }
    }
}

/// <summary>
/// Represents a dispatcher operation that produces a result.
/// </summary>
/// <typeparam name="T">The operation result type.</typeparam>
public class DispatcherOperation<T> : DispatcherOperation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DispatcherOperation{T}"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher that owns the operation.</param>
    /// <param name="priority">The dispatcher priority used to schedule the operation.</param>
    /// <param name="callback">The callback invoked on the dispatcher thread.</param>
    public DispatcherOperation(Dispatcher dispatcher, DispatcherPriority priority, Func<T> callback) : base(dispatcher, priority, false)
    {
        TaskSource = new TaskCompletionSource<T>();
        Callback = callback;
    }

    private TaskCompletionSource<T> TaskCompletionSource => (TaskCompletionSource<T>)TaskSource!;

    /// <summary>
    /// Returns an awaiter for awaiting the operation result.
    /// </summary>
    /// <returns>The task awaiter for the result task.</returns>
    public new TaskAwaiter<T> GetAwaiter() => GetTask().GetAwaiter();

    /// <summary>
    /// Gets the task that completes with this operation's result.
    /// </summary>
    /// <returns>The result task.</returns>
    public new Task<T> GetTask() => TaskCompletionSource!.Task;

    /// <inheritdoc />
    protected override Task GetTaskCore() => GetTask();

    /// <inheritdoc />
    protected override void AbortTask() => TaskCompletionSource.SetCanceled();

    internal override object? GetResult() => GetTask().Result;

    /// <inheritdoc />
    protected override void InvokeCore()
    {
        try
        {
            var result = ((Func<T>)Callback!)();
            lock (Dispatcher.InstanceLock)
            {
                Status = DispatcherOperationStatus.Completed;
                TaskCompletionSource.SetResult(result);
            }
        }
        catch (Exception e)
        {
            lock (Dispatcher.InstanceLock)
            {
                Status = DispatcherOperationStatus.Completed;
                TaskCompletionSource.SetException(e);
            }
        }
    }

    /// <summary>
    /// Gets the result of the completed dispatcher operation.
    /// </summary>
    /// <remarks>
    /// Reading this property from the dispatcher thread is allowed only after the operation has
    /// completed. Non-UI threads can block until the result is available.
    /// </remarks>
    public T Result
    {
        get
        {
            if (TaskCompletionSource.Task.IsCompleted || !Dispatcher.CheckAccess())
                return TaskCompletionSource.Task.GetAwaiter().GetResult();
            throw new InvalidOperationException("Synchronous wait is only supported on non-UI threads");
        }
    }
}

internal class SendOrPostCallbackDispatcherOperation : DispatcherOperation
{
    private readonly object? _arg;

    internal SendOrPostCallbackDispatcherOperation(Dispatcher dispatcher, DispatcherPriority priority, 
        SendOrPostCallback callback, object? arg, bool throwOnUiThread) 
        : base(dispatcher, priority, throwOnUiThread)
    {
        Callback = callback;
        _arg = arg;
    }
    
    protected override void InvokeCore()
    {
        try
        {
            ((SendOrPostCallback)Callback!)(_arg);
            lock (Dispatcher.InstanceLock)
            {
                Status = DispatcherOperationStatus.Completed;
                if (TaskSource is TaskCompletionSource<object?> tcs)
                    tcs.SetResult(null);
            }
        }
        catch (Exception e)
        {
            lock (Dispatcher.InstanceLock)
            {
                Status = DispatcherOperationStatus.Completed;
                if (TaskSource is TaskCompletionSource<object?> tcs)
                    tcs.SetException(e);
            }

            if (ThrowOnUiThread)
                throw;
        }
    }
}

/// <summary>
/// Identifies the lifecycle state of a <see cref="DispatcherOperation"/>.
/// </summary>
public enum DispatcherOperationStatus
{
    /// <summary>
    /// The operation is queued and has not started executing.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The operation was aborted before it completed.
    /// </summary>
    Aborted = 1,

    /// <summary>
    /// The operation completed, either successfully or with a captured exception.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The operation callback is currently executing.
    /// </summary>
    Executing = 3,
}
