using System.Runtime.ExceptionServices;
using System.Windows.Input;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext;

/// <summary>Represents a reusable Task-based action with single-flight execution.</summary>
/// <remarks>
/// Create, query, start and cancel on the creating UI thread. IsExecuting, ExecutionTask and
/// cancellation-state getters can be read from any thread. A running invocation makes CanExecute
/// false for all sources sharing this command. Sources never dispose or automatically cancel it.
/// Completion notifications use ordinary synchronous events and may originate on a background
/// thread; framework command sources marshal them through the existing dispatcher. There is no
/// polling, Task.Run, async void, global lock or additional scheduler.
/// </remarks>
/// <example><code>
/// var load = new AsyncCommand(async (_, token) =&gt; await LoadDocumentAsync(token));
/// var button = new Button { Text = "Load", Command = load };
/// // Application code can await and catch failures instead of using ICommand.Execute:
/// await load.ExecuteAsync();
/// </code></example>
public sealed class AsyncCommand : ICommand
{
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private readonly Func<object?, CancellationToken, Task> execute;
    private readonly Predicate<object?>? canExecute;
    private readonly bool cancellable;
    private Invocation? current;
    private Task? executionTask;

    /// <summary>Creates a parameterless asynchronous command.</summary>
    /// <param name="execute">The operation; it must return a non-null Task.</param>
    /// <param name="canExecute">Optional availability predicate.</param>
    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this.execute = (_, _) => execute();
        this.canExecute = canExecute is null ? null : _ => canExecute();
    }

    /// <summary>Creates a parameter-aware asynchronous command.</summary>
    /// <param name="execute">The operation receiving the nullable parameter unchanged.</param>
    /// <param name="canExecute">Optional availability predicate.</param>
    public AsyncCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this.execute = (parameter, _) => execute(parameter);
        this.canExecute = canExecute;
    }

    /// <summary>Creates an asynchronous command supporting cooperative cancellation.</summary>
    /// <param name="execute">The operation receiving the parameter and a fresh per-invocation token.</param>
    /// <param name="canExecute">Optional availability predicate.</param>
    /// <remarks>The two-argument callback avoids ambiguous object/token unary lambda overloads.</remarks>
    public AsyncCommand(Func<object?, CancellationToken, Task> execute, Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this.execute = execute;
        this.canExecute = canExecute;
        cancellable = true;
    }

    /// <summary>Gets whether an invocation currently owns the single-flight slot.</summary>
    public bool IsExecuting => Volatile.Read(ref current) is not null;
    /// <summary>Gets the most recently started invocation's task, or null before the first start.</summary>
    /// <remarks>
    /// Rejected starts do not replace this task. It retains its result/fault for explicit application
    /// observation and shutdown coordination. Await pending work before ending the UI loop if its
    /// completion must update UI or deliver dispatcher exceptions.
    /// </remarks>
    public Task? ExecutionTask => Volatile.Read(ref executionTask);
    /// <summary>Gets whether the current operation accepts a first cancellation request.</summary>
    public bool CanCancel => cancellable && Volatile.Read(ref current) is { CancellationRequested: false };
    /// <summary>Gets whether cancellation was requested for the current invocation.</summary>
    /// <remarks>Returns false again after the invocation finishes.</remarks>
    public bool IsCancellationRequested => Volatile.Read(ref current)?.CancellationRequested == true;

    /// <summary>Occurs when sources should reevaluate availability and execution/cancellation state.</summary>
    public event EventHandler? CanExecuteChanged;
    /// <summary>Occurs for opt-in execution outcomes without user data.</summary>
    /// <remarks>
    /// Observers should be fast and must marshal UI work when called from a continuation thread.
    /// Observer failures become execution failures; an original operation failure takes precedence.
    /// </remarks>
    public event EventHandler<AsyncCommandDiagnosticEventArgs>? Diagnostic;

    /// <summary>Queries availability on the creating UI thread without starting work.</summary>
    /// <param name="parameter">The nullable application parameter.</param>
    /// <returns>False while executing; otherwise the predicate result, or true without a predicate.</returns>
    public bool CanExecute(object? parameter)
    {
        VerifyAccess();
        if (IsExecuting) return false;
        bool available = canExecute?.Invoke(parameter) ?? true;
        // A predicate can start this command reentrantly. Never let its earlier availability
        // result replace that running invocation or report an available source while it is busy.
        return available && !IsExecuting;
    }

    /// <summary>Starts a supervised invocation for an ICommand source.</summary>
    /// <param name="parameter">The nullable application parameter.</param>
    /// <remarks>
    /// A rejected start is a no-op. Execution faults are observed and posted, unchanged, to the
    /// existing UI dispatcher exception path. Cancellation is not an unhandled failure. Predicate
    /// and thread-validation exceptions propagate synchronously. Use ExecuteAsync to await/catch
    /// an operation instead; do not block the UI thread waiting for either task.
    /// </remarks>
    public void Execute(object? parameter) => Start(parameter, reportFault: true, checkAvailability: true);

    /// <summary>Starts an invocation whose completion/failure is owned by its awaiting caller.</summary>
    /// <param name="parameter">The nullable application parameter.</param>
    /// <returns>The invocation task, or a completed task when unavailable/already executing.</returns>
    /// <remarks>
    /// Unlike Execute, faults are returned through the Task and are not additionally posted to the
    /// dispatcher. State recovers on success, failure and cancellation. Framework-source enabled
    /// updates posted by a background notification still require the existing UI queue to run.
    /// </remarks>
    public Task ExecuteAsync(object? parameter = null) => Start(parameter, reportFault: false, checkAvailability: true);

    // Button/menu/input sources already queried and checked their current binding. Preserve those
    // predicate counts while still refusing a competing start of this shared command.
    internal void ExecuteCore(object? parameter) => Start(parameter, reportFault: true, checkAvailability: false);

    /// <summary>Requests cooperative cancellation on the creating UI thread.</summary>
    /// <remarks>
    /// No-op without a cancellable running invocation or after its first request. Ignoring the token
    /// does not manufacture cancellation. Cancellation-callback exceptions propagate to this caller.
    /// The command owns its token source; disposing a UI source never calls Cancel automatically.
    /// </remarks>
    public void Cancel()
    {
        VerifyAccess();
        if (!cancellable || Volatile.Read(ref current) is not { } invocation) return;
        Exception? callbackError = null;
        lock (invocation)
        {
            if (invocation.Completed || invocation.CancellationRequested) return;
            invocation.CancellationRequested = true;
            invocation.Cancelling = true;
            try { invocation.Cancellation!.Cancel(); }
            catch (Exception error) { callbackError = error; }
            finally
            {
                // A token callback may complete the operation inline inside Cancel. Defer CTS
                // disposal until Cancel itself returns, including this same-thread reentrant case.
                invocation.Cancelling = false;
                if (invocation.Completed) invocation.Cancellation!.Dispose();
            }
        }
        try { RaiseCanExecuteChanged(); }
        catch when (callbackError is not null) { } // Preserve the original cancellation failure.
        if (callbackError is not null) ExceptionDispatchInfo.Capture(callbackError).Throw();
    }

    /// <summary>Notifies sources after application availability changes, without polling.</summary>
    /// <remarks>
    /// Normal synchronous event semantics apply. May be called from a background thread; framework
    /// sources post their own guarded refresh through the existing UI dispatcher.
    /// </remarks>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private Task Start(object? parameter, bool reportFault, bool checkAvailability)
    {
        VerifyAccess();
        if (IsExecuting || (checkAvailability && !CanExecute(parameter))) return Task.CompletedTask;
        var invocation = new Invocation(Dispatcher.UIThread, parameter?.GetType(), reportFault, cancellable);
        Volatile.Write(ref current, invocation);
        Volatile.Write(ref executionTask, invocation.Completion.Task);
        Task operation;
        bool invokingOperation = false;
        try
        {
            RaiseCanExecuteChanged();
            Report(AsyncCommandDiagnosticKind.Started, invocation.ParameterType);
            invokingOperation = true;
            operation = execute(parameter, invocation.Cancellation?.Token ?? CancellationToken.None)
                ?? throw new InvalidOperationException("An asynchronous command callback returned a null Task.");
        }
        catch (Exception error)
        {
            bool cancelled = invokingOperation && error is OperationCanceledException;
            Finish(invocation, cancelled ? null : error, cancelled);
            return invocation.Completion.Task;
        }

        // Observe the original Task directly; no async-void callback or discarded supervisor Task.
        // ConfigureAwait(false) leaves continuation scheduling to the Task, while command sources
        // retain their existing dispatcher policy for UI updates.
        if (operation.IsCompleted) CompleteOperation(operation, invocation);
        else operation.ConfigureAwait(false).GetAwaiter().OnCompleted(() => CompleteOperation(operation, invocation));
        return invocation.Completion.Task;
    }

    private void CompleteOperation(Task operation, Invocation invocation)
    {
        Exception? error = null;
        try { operation.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) when (operation.IsCanceled) { }
        catch (Exception failure) { error = failure; }
        Finish(invocation, error, operation.IsCanceled);
    }

    private void Finish(Invocation invocation, Exception? error, bool cancelled)
    {
        lock (invocation)
        {
            invocation.Completed = true;
            if (!invocation.Cancelling) invocation.Cancellation?.Dispose();
        }
        Interlocked.CompareExchange(ref current, null, invocation);
        try { RaiseCanExecuteChanged(); }
        catch (Exception observerError) { error ??= observerError; }
        try
        {
            Report(error is not null ? AsyncCommandDiagnosticKind.Faulted : cancelled
                ? AsyncCommandDiagnosticKind.Cancelled : AsyncCommandDiagnosticKind.Completed,
                invocation.ParameterType, error?.GetType());
        }
        catch (Exception observerError) { error ??= observerError; }

        if (error is not null)
        {
            invocation.Completion.TrySetException(error);
            if (invocation.ReportFault)
            {
                _ = invocation.Completion.Task.Exception; // Observe even when the UI loop is ending.
                var original = ExceptionDispatchInfo.Capture(error);
                invocation.Dispatcher.Post(original.Throw);
            }
        }
        else if (cancelled) invocation.Completion.TrySetCanceled();
        else invocation.Completion.TrySetResult();
    }

    private void Report(AsyncCommandDiagnosticKind kind, Type? parameterType, Type? exceptionType = null)
        => Diagnostic?.Invoke(this, new(kind, parameterType, exceptionType));

    private void VerifyAccess()
    {
        if (threadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("Async commands must be started, queried and cancelled on their creating UI thread.");
    }

    private sealed class Invocation(Dispatcher dispatcher, Type? parameterType, bool reportFault, bool cancellable)
    {
        internal readonly Dispatcher Dispatcher = dispatcher;
        internal readonly Type? ParameterType = parameterType;
        internal readonly bool ReportFault = reportFault;
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly CancellationTokenSource? Cancellation = cancellable ? new() : null;
        internal volatile bool CancellationRequested;
        internal bool Completed, Cancelling;
    }
}
