using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

// Native adapters and net10 tests share this state machine. Cancellation finishes the Task,
// but holds the slot until callback/destruction: Android may still display its native modal.
// Reserved codes never repeat during a process, including after timeout or recreation.
internal sealed class NativeRequestCoordinator<THost, TResult> where THost : class
{
    private readonly object sync = new();
    private readonly int lastCode;
    private int nextCode;
    private Pending? pending;
    private bool shutdown;

    internal NativeRequestCoordinator(int firstCode = 8192, int lastCode = 32767)
    {
        nextCode = firstCode;
        this.lastCode = lastCode;
    }
    internal bool Busy { get { lock (sync) return pending is not null; } }
    internal bool IsShutdown { get { lock (sync) return shutdown; } }

    internal (int Code, Task<TResult> Task) Begin(THost host, CancellationToken cancellationToken,
        Action? retire = null, long generation = 0)
    {
        ArgumentNullException.ThrowIfNull(host);
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (shutdown) throw new PlatformServiceException(PlatformServiceStatus.Shutdown);
            if (pending is not null) throw new PlatformServiceException(PlatformServiceStatus.Busy);
            if (nextCode > lastCode) throw new PlatformServiceException(PlatformServiceStatus.Unavailable);
            var request = new Pending(nextCode++, new WeakReference<THost>(host), retire, generation);
            pending = request;
            request.Registration = cancellationToken.Register(() => request.Completion.TrySetCanceled(cancellationToken));
            return (request.Code, request.Completion.Task);
        }
    }

    internal bool Complete(THost host, int code, TResult result, long generation = 0)
    {
        Pending? request;
        lock (sync)
        {
            request = pending;
            if (request is null || request.Code != code || request.Generation != generation || !request.Host.TryGetTarget(out var owner) ||
                !ReferenceEquals(owner, host)) return false;
            pending = null;
        }
        request.Registration.Dispose();
        Finish(request, () => request.Completion.TrySetResult(result));
        return true;
    }

    internal void FailStart(int code, Exception exception)
    {
        Pending? request;
        lock (sync)
        {
            request = pending;
            if (request is null || request.Code != code) return;
            pending = null;
        }
        request.Registration.Dispose();
        Finish(request, () => request.Completion.TrySetException(exception));
    }

    internal void Destroy(THost host)
    {
        Pending? request;
        lock (sync)
        {
            request = pending;
            if (request is null || !request.Host.TryGetTarget(out var owner) || !ReferenceEquals(owner, host)) return;
            pending = null;
        }
        request.Registration.Dispose();
        Finish(request, () => request.Completion.TrySetException(new PlatformServiceException(PlatformServiceStatus.HostLost)));
    }

    internal void Shutdown()
    {
        Pending? request;
        lock (sync) { shutdown = true; request = pending; pending = null; }
        if (request is null) return;
        request.Registration.Dispose();
        Finish(request, () => request.Completion.TrySetException(new PlatformServiceException(PlatformServiceStatus.Shutdown)));
    }

    // Dismissible native UI releases its slot only on the UI thread, after requesting native
    // cleanup. SAF/permission cancellation keeps the original retain-until-callback policy.
    internal bool Cancel(THost host, int code, CancellationToken token, long generation = 0)
    {
        Pending? request;
        lock (sync)
        {
            request = pending;
            if (request is null || request.Code != code || request.Generation != generation ||
                !request.Host.TryGetTarget(out var owner) || !ReferenceEquals(owner, host)) return false;
            pending = null;
        }
        request.Registration.Dispose();
        Finish(request, () => request.Completion.TrySetCanceled(token));
        return true;
    }

    private static void Finish(Pending request, Action complete)
    {
        var cleanup = request.Retire;
        request.Retire = null;
        try { cleanup?.Invoke(); }
        catch (Exception exception) { request.Completion.TrySetException(exception); }
        finally { complete(); }
    }

    private sealed class Pending(int code, WeakReference<THost> host, Action? retire, long generation)
    {
        internal readonly int Code = code;
        internal readonly long Generation = generation;
        internal Action? Retire = retire;
        internal readonly WeakReference<THost> Host = host;
        internal readonly TaskCompletionSource<TResult> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationTokenRegistration Registration;
    }
}
