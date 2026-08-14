namespace ModernFormsNext.WindowKit.Backend.Android.Animation;

internal enum AndroidFrameCallbackAction
{
    None,
    Post,
    Remove
}

/// <summary>
/// Stores the Android frame-source state without retaining a native surface or callback object.
/// </summary>
/// <remarks>
/// The native wrapper serializes calls with its own lock. Keeping this state Android-API-free
/// makes idle/wake, surface gating, and duplicate-callback behavior deterministic in unit tests.
/// </remarks>
internal sealed class AndroidFrameCallbackState
{
    public bool SchedulerDemand { get; private set; }

    public int ActiveSurfaceCount { get; private set; }

    public bool CallbackPending { get; private set; }

    public long PostedCallbackCount { get; private set; }

    public long DeliveredCallbackCount { get; private set; }

    public bool IsDisposed { get; private set; }

    public void SetSchedulerDemand(bool active)
    {
        if (!IsDisposed)
            SchedulerDemand = active;
    }

    public void AddActiveSurface()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ActiveSurfaceCount++;
    }

    public void RemoveActiveSurface()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (ActiveSurfaceCount <= 0)
            throw new InvalidOperationException("No active Android animation surface is registered.");
        ActiveSurfaceCount--;
    }

    public AndroidFrameCallbackAction Reconcile()
    {
        bool shouldRun = !IsDisposed && SchedulerDemand && ActiveSurfaceCount > 0;
        if (shouldRun == CallbackPending)
            return AndroidFrameCallbackAction.None;

        CallbackPending = shouldRun;
        if (shouldRun)
        {
            PostedCallbackCount++;
            return AndroidFrameCallbackAction.Post;
        }

        return AndroidFrameCallbackAction.Remove;
    }

    public bool BeginFrameDelivery()
    {
        if (!CallbackPending)
            return false;

        CallbackPending = false;
        if (IsDisposed || !SchedulerDemand || ActiveSurfaceCount == 0)
            return false;

        DeliveredCallbackCount++;
        return true;
    }

    public AndroidFrameCallbackAction Dispose()
    {
        if (IsDisposed)
            return AndroidFrameCallbackAction.None;

        bool remove = CallbackPending;
        IsDisposed = true;
        SchedulerDemand = false;
        ActiveSurfaceCount = 0;
        CallbackPending = false;
        return remove ? AndroidFrameCallbackAction.Remove : AndroidFrameCallbackAction.None;
    }
}
