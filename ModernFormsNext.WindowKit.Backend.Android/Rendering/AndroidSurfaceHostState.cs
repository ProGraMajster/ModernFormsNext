namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Stores deterministic lifecycle and invalidation state for an Android Skia surface.
/// </summary>
/// <remarks>
/// This type deliberately contains no Android objects. It is shared with the plain <c>net10.0</c>
/// target so resize, invalidation, cancellation, and disposal behavior can be tested without an
/// emulator. All members must be called from the Android UI thread.
/// </remarks>
public sealed class AndroidSurfaceHostState
{
    private readonly HashSet<int> activePointers = [];

    /// <summary>Gets the current surface lifecycle state.</summary>
    public AndroidSurfaceLifecycleState LifecycleState { get; private set; }

    /// <summary>Gets the surface width in logical pixels.</summary>
    public float LogicalWidth { get; private set; }

    /// <summary>Gets the surface height in logical pixels.</summary>
    public float LogicalHeight { get; private set; }

    /// <summary>Gets the number of completed render passes.</summary>
    public long RenderCount { get; private set; }

    /// <summary>Gets a value indicating whether a native invalidation is already queued.</summary>
    public bool IsInvalidationPending { get; private set; }

    /// <summary>Gets the number of active pointers tracked by the surface.</summary>
    public int ActivePointerCount => activePointers.Count;

    /// <summary>Marks the surface as attached and active.</summary>
    public void Resume()
    {
        ThrowIfDisposed();
        LifecycleState = AndroidSurfaceLifecycleState.Resumed;
    }

    /// <summary>Pauses input and returns pointer identifiers that must receive cancellation.</summary>
    public IReadOnlyList<int> Pause()
    {
        ThrowIfDisposed();
        LifecycleState = AndroidSurfaceLifecycleState.Paused;
        return CancelPointers();
    }

    /// <summary>Marks the owning activity as stopped and cancels any remaining pointers.</summary>
    public IReadOnlyList<int> Stop()
    {
        ThrowIfDisposed();
        LifecycleState = AndroidSurfaceLifecycleState.Stopped;
        return CancelPointers();
    }

    /// <summary>Updates the logical size and requests a render only when the size changed.</summary>
    /// <returns><see langword="true"/> when the logical size changed.</returns>
    public bool Resize(float width, float height)
    {
        ThrowIfDisposed();
        if (width < 0 || height < 0 || !float.IsFinite(width) || !float.IsFinite(height))
            throw new ArgumentOutOfRangeException(nameof(width), "Surface dimensions must be non-negative and finite.");

        if (LogicalWidth == width && LogicalHeight == height)
            return false;

        LogicalWidth = width;
        LogicalHeight = height;
        RequestInvalidation();
        return true;
    }

    /// <summary>Queues one native invalidation, coalescing repeated requests.</summary>
    /// <returns><see langword="true"/> when the caller must invalidate the native view.</returns>
    public bool RequestInvalidation()
    {
        ThrowIfDisposed();
        if (LifecycleState == AndroidSurfaceLifecycleState.Uninitialized)
            throw new InvalidOperationException("The Android surface must be resumed before it can be invalidated.");
        if (IsInvalidationPending)
            return false;

        IsInvalidationPending = true;
        return true;
    }

    /// <summary>Records a render and clears its pending invalidation.</summary>
    public void CompleteRender()
    {
        ThrowIfDisposed();
        if (LifecycleState == AndroidSurfaceLifecycleState.Uninitialized)
            throw new InvalidOperationException("The Android surface must be resumed before it can render.");

        IsInvalidationPending = false;
        RenderCount++;
    }

    /// <summary>Updates the active-pointer set and reports whether input was accepted.</summary>
    public bool TrackPointer(int pointerId, AndroidPointerAction action)
    {
        ThrowIfDisposed();
        if (LifecycleState != AndroidSurfaceLifecycleState.Resumed)
            return false;

        switch (action)
        {
            case AndroidPointerAction.Down:
                activePointers.Add(pointerId);
                break;
            case AndroidPointerAction.Up:
            case AndroidPointerAction.Cancel:
                activePointers.Remove(pointerId);
                break;
        }

        return true;
    }

    /// <summary>Releases the host permanently and returns pointers that require cancellation.</summary>
    public IReadOnlyList<int> Dispose()
    {
        if (LifecycleState == AndroidSurfaceLifecycleState.Disposed)
            return [];

        var cancelled = CancelPointers();
        IsInvalidationPending = false;
        LifecycleState = AndroidSurfaceLifecycleState.Disposed;
        return cancelled;
    }

    private IReadOnlyList<int> CancelPointers()
    {
        if (activePointers.Count == 0)
            return [];

        var cancelled = activePointers.Order().ToArray();
        activePointers.Clear();
        return cancelled;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(LifecycleState == AndroidSurfaceLifecycleState.Disposed, this);
    }
}
