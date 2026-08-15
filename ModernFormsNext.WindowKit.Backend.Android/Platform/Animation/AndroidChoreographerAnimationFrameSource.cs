using Android.OS;
using Android.Views;
using ModernFormsNext.WindowKit.Backend.Android.Animation;

namespace ModernFormsNext.WindowKit.Backend.Android;

/// <summary>
/// Uses one coalesced Android <see cref="Choreographer"/> callback for shared scheduler work.
/// </summary>
internal sealed class AndroidChoreographerAnimationFrameSource : Java.Lang.Object,
    IPlatformAnimationFrameSource,
    Choreographer.IFrameCallback
{
    private readonly object sync = new();
    private readonly AndroidFrameCallbackState state = new();
    private readonly Handler mainHandler;
    private readonly Action<string>? diagnosticSink;
    private Choreographer? choreographer;
    private Action? frameRequested;
    private bool reconcilePosted;
    private bool disposed;

    public AndroidChoreographerAnimationFrameSource(Action<string>? diagnosticSink)
    {
        this.diagnosticSink = diagnosticSink;
        Looper mainLooper = Looper.MainLooper
            ?? throw new InvalidOperationException("Android did not provide a main Looper.");
        mainHandler = new Handler(mainLooper);
    }

    public bool IsCallbackPending
    {
        get
        {
            lock (sync)
                return state.CallbackPending;
        }
    }

    internal AndroidAnimationSurfaceRegistration CreateSurfaceRegistration()
    {
        lock (sync)
            ObjectDisposedException.ThrowIf(disposed, this);
        return new AndroidAnimationSurfaceRegistration(this);
    }

    public void Start(Action frameRequested)
    {
        ArgumentNullException.ThrowIfNull(frameRequested);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            this.frameRequested = frameRequested;
            state.SetSchedulerDemand(active: true);
            QueueReconcileLocked();
        }
    }

    public void Stop()
    {
        lock (sync)
        {
            if (disposed)
                return;
            frameRequested = null;
            state.SetSchedulerDemand(active: false);
            QueueReconcileLocked();
        }
    }

    public void DoFrame(long frameTimeNanos)
    {
        Action? callback;
        lock (sync)
        {
            if (disposed)
                return;
            callback = state.BeginFrameDelivery() ? frameRequested : null;
        }

        try
        {
            callback?.Invoke();
        }
        catch (Exception exception)
        {
            // Scheduler callbacks isolate animation faults themselves. This final boundary keeps
            // an unexpected integration failure from tearing down Android's Choreographer loop.
            AndroidLogger.Error("Android animation frame callback failed.", exception, diagnosticSink);
        }
        finally
        {
            lock (sync)
            {
                if (!disposed)
                    QueueReconcileLocked();
            }
        }
    }

    internal AndroidAnimationFrameSourceDiagnostics GetDiagnostics()
    {
        lock (sync)
        {
            return new AndroidAnimationFrameSourceDiagnostics(
                state.CallbackPending,
                state.SchedulerDemand,
                state.ActiveSurfaceCount,
                state.PostedCallbackCount,
                state.DeliveredCallbackCount);
        }
    }

    internal void SetSurfaceActive(bool active)
    {
        lock (sync)
        {
            if (disposed)
                return;
            if (active)
                state.AddActiveSurface();
            else
                state.RemoveActiveSurface();
            QueueReconcileLocked();
        }
    }

    protected override void Dispose(bool disposing)
    {
        AndroidFrameCallbackAction action;
        lock (sync)
        {
            if (disposed)
                return;
            disposed = true;
            frameRequested = null;
            action = state.Dispose();
        }

        if (action == AndroidFrameCallbackAction.Remove)
            mainHandler.Post(RemoveFrameCallbackOnMainThread);
        base.Dispose(disposing);
    }

    private void QueueReconcileLocked()
    {
        if (reconcilePosted)
            return;

        reconcilePosted = true;
        if (!mainHandler.Post(ReconcileOnMainThread))
        {
            reconcilePosted = false;
            throw new InvalidOperationException("Android rejected animation frame reconciliation.");
        }
    }

    private void ReconcileOnMainThread()
    {
        AndroidFrameCallbackAction action;
        lock (sync)
        {
            reconcilePosted = false;
            action = state.Reconcile();
        }

        switch (action)
        {
            case AndroidFrameCallbackAction.Post:
                GetChoreographerOnMainThread().PostFrameCallback(this);
                break;
            case AndroidFrameCallbackAction.Remove:
                RemoveFrameCallbackOnMainThread();
                break;
        }
    }

    private Choreographer GetChoreographerOnMainThread()
    {
        if (!ReferenceEquals(Looper.MyLooper(), Looper.MainLooper))
        {
            throw new InvalidOperationException(
                "Android animation frame reconciliation must run on the main Looper.");
        }

        return choreographer ??= Choreographer.Instance
            ?? throw new InvalidOperationException("Android did not provide Choreographer.");
    }

    private void RemoveFrameCallbackOnMainThread()
    {
        // A stop/dispose can overtake the first posted reconciliation. In that case no native
        // Choreographer instance or callback exists yet, so there is nothing to remove.
        choreographer?.RemoveFrameCallback(this);
    }
}

internal readonly record struct AndroidAnimationFrameSourceDiagnostics(
    bool CallbackPending,
    bool SchedulerDemand,
    int ActiveSurfaceCount,
    long PostedCallbackCount,
    long DeliveredCallbackCount);

internal sealed class AndroidAnimationSurfaceRegistration : IDisposable
{
    private AndroidChoreographerAnimationFrameSource? owner;
    private bool active;

    public AndroidAnimationSurfaceRegistration(AndroidChoreographerAnimationFrameSource owner)
        => this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public void SetActive(bool value)
    {
        if (owner is null || active == value)
            return;

        active = value;
        owner.SetSurfaceActive(value);
    }

    public void Dispose()
    {
        AndroidChoreographerAnimationFrameSource? source = owner;
        if (source is null)
            return;

        owner = null;
        if (active)
        {
            active = false;
            source.SetSurfaceActive(active: false);
        }
    }
}
