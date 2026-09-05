using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

/// <summary>Contains callback failures and prevents UI-thread self-waits or late queued mutations.</summary>
internal sealed class AndroidAccessibilityDispatch(IPlatformDispatcher dispatcher, Action? reportFailure = null)
{
    private bool failureReported;

    internal T Run<T>(Func<T> callback, T fallback, int timeoutMilliseconds = 2000)
    {
        try
        {
            if (dispatcher.CheckAccess()) return callback();
            using var cancellation = new CancellationTokenSource();
            var task = dispatcher.InvokeAsync(callback, cancellation.Token);
            if (task.Wait(timeoutMilliseconds)) return task.GetAwaiter().GetResult();
            cancellation.Cancel();
        }
        catch (Exception)
        {
            // Exception messages and parameters can contain user text. Report only one generic
            // category per provider; stale/unsupported queries normally return without throwing.
            if (!failureReported)
            {
                failureReported = true;
                reportFailure?.Invoke();
            }
        }
        return fallback;
    }
}
