namespace ModernFormsNext.Automation;

/// <summary>Owns an opt-in registration, while borrowing its window/surface through weak references.</summary>
/// <remarks>
/// Dispose on the session UI thread to unregister without closing the application root.
/// Window close/disposal unregisters immediately. Surface disposal or collection is also observed
/// by IsRegistered and the next live operation. Keeping this token never keeps the root alive.
/// </remarks>
public sealed class AutomationRootRegistration : IDisposable
{
    private readonly WeakReference<AutomationSession> session;
    private readonly WeakReference<WindowBase>? window;
    private readonly WeakReference<SkiaControlSurface>? surface;
    private readonly WeakReference<Control>? surfaceControl;
    private bool removed;

    internal AutomationRootRegistration(AutomationSession owner, WindowBase root, AutomationCapability capabilities)
    {
        session = new(owner); window = new(root); Capabilities = capabilities;
        CoordinateSpace = AutomationCoordinateSpace.Screen;
        root.Closed += OnEnded;
        root.Disposed += OnEnded;
    }

    internal AutomationRootRegistration(AutomationSession owner, SkiaControlSurface root, AutomationCapability capabilities)
    {
        session = new(owner); surface = new(root); surfaceControl = new(root.Root); Capabilities = capabilities;
        CoordinateSpace = AutomationCoordinateSpace.Surface;
        root.Root.Disposed += OnEnded;
    }

    /// <summary>Gets the unique registration identity; unregistering and registering again produces a new ID.</summary>
    public string RootId { get; } = Guid.NewGuid().ToString("N");
    /// <summary>Gets the intersection of the requested root policy and session policy.</summary>
    public AutomationCapability Capabilities { get; }
    /// <summary>Gets the coordinate reference for this root's canonical bounds.</summary>
    public AutomationCoordinateSpace CoordinateSpace { get; }
    /// <summary>Gets whether the root remains registered. Read on the owning UI thread.</summary>
    public bool IsRegistered
    {
        get
        {
            if (!session.TryGetTarget(out var owner)) return false;
            owner.VerifyAccess();
            return !owner.IsStopped && !removed && owner.IsLive(this);
        }
    }

    internal bool Represents(object candidate)
        => (window?.TryGetTarget(out var w) == true && ReferenceEquals(w, candidate))
        || (surface?.TryGetTarget(out var s) == true && ReferenceEquals(s, candidate));

    internal bool TryGetPeer(out Accessibility.AccessibleObject? peer)
    {
        peer = null;
        if (removed) return false;
        if (window?.TryGetTarget(out var w) == true)
        {
            // This existing flag is set on actual close/disposal, but not a cancelled close.
            // Component does not expose IsDisposed; using this seam avoids agent API on WindowBase.
            if (w.InputBindingsClosed) return false;
            peer = w.AccessibilityObject;
        }
        else if (surface?.TryGetTarget(out var s) == true && !s.IsDisposed && !s.Root.IsDisposed)
            peer = s.Root.AccessibilityObject;
        return peer is not null;
    }

    /// <summary>Unregisters and removes lifetime subscriptions on the owning UI thread. Repeated calls are safe.</summary>
    public void Dispose()
    {
        if (session.TryGetTarget(out var owner))
        {
            owner.VerifyAccess();
            owner.Remove(this);
        }
        else Detach();
    }

    internal void Detach()
    {
        if (removed) return;
        removed = true;
        if (window?.TryGetTarget(out var w) == true)
        {
            w.Closed -= OnEnded;
            w.Disposed -= OnEnded;
        }
        if (surfaceControl?.TryGetTarget(out var c) == true) c.Disposed -= OnEnded;
    }

    private void OnEnded(object? sender, EventArgs args) => Dispose();
}
