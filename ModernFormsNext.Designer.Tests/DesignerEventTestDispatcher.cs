using System.Collections.Concurrent;

namespace ModernFormsNext.Designer.Tests;

// A windowless test UI thread pumps only this test's async continuations. Session transactions
// retain their production owner-thread check while real source analysis runs on its worker.
internal sealed class DesignerEventTestDispatcher : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> callbacks = new();

    public override void Post(SendOrPostCallback callback, object? state) => callbacks.Add((callback, state));

    internal static void Run(Func<Task> action)
    {
        var previous = Current;
        var dispatcher = new DesignerEventTestDispatcher();
        SetSynchronizationContext(dispatcher);
        try
        {
            var operation = action();
            _ = operation.ContinueWith(_ => dispatcher.callbacks.CompleteAdding(), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            foreach (var item in dispatcher.callbacks.GetConsumingEnumerable()) item.Callback(item.State);
            // The dedicated pump above has already completed every UI continuation. This unwraps
            // test failures without blocking an application dispatcher or relaxing its affinity.
#pragma warning disable VSTHRD002
            operation.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
        finally { SetSynchronizationContext(previous); dispatcher.callbacks.Dispose(); }
    }
}
