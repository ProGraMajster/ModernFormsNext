namespace ModernFormsNext;

public abstract partial class WindowBase
{
    // Baseline/identity for notifications, not another DPI model. Scaling reads IWindowBaseImpl.
    private double dpiScaling;
    private DpiChangedEventArgs? dpiChange;

    /// <summary>Occurs after the backend confirms a different top-level window rendering scale.</summary>
    /// <remarks>
    /// Raised on the UI thread after the control tree's DPI cache invalidation, hooks and window
    /// layout request. Explicitly suspended layout remains deferred. Scaling equals NewScale;
    /// identical confirmations are ignored. This does not mean monitor, visibility, activation or
    /// window-state changes. Geometry events can accompany DPI changes without a universal ordering.
    /// The internal ControlAdapter does not publish an additional public DPI event. Android surface
    /// density is not mapped to this desktop window lifecycle. Observer failures do not roll back DPI.
    /// </remarks>
    public event EventHandler<DpiChangedEventArgs>? DpiChanged;

    /// <summary>Raises DpiChanged after the window's required DPI update has completed.</summary>
    /// <param name="e">The previous and newly confirmed rendering scales and derived integer DPI.</param>
    /// <remarks>Called on the UI thread. Overrides must call base to notify subscribers.</remarks>
    protected virtual void OnDpiChanged(DpiChangedEventArgs e)
    {
        long visibility = visibilityVersion;
        List<Exception> failures = [];
        if (DpiChanged is { } handlers)
            foreach (EventHandler<DpiChangedEventArgs> handler in handlers.GetInvocationList()) {
                if (!IsCurrentDpiChange(e) || !IsCurrentShowRequest(visibility)) break;
                try { handler(this, e); }
                catch (Exception failure) { failures.Add(failure); }
            }
        Control.ThrowDpiFailures(failures);
    }

    internal bool IsCurrentDpiChange(DpiChangedEventArgs e) =>
        !backendClosed && ReferenceEquals(dpiChange, e) && Scaling == e.NewScale;

    private void OnBackendScalingChanged(double scale)
    {
        if (backendClosed || scale != Scaling || scale == dpiScaling) return;
        var change = new DpiChangedEventArgs(dpiScaling, scale);
        dpiScaling = scale;
        dpiChange = change;
        using var batch = Application.BeginVisualInvalidationBatch();
        List<Exception> failures = [];
        // Keep processing still-current descendants and final layout after an observer failure.
        // Reentrant DPI replaces the identity and retires the remainder of the older traversal.
        try { UpdateWindowGeometry(() => adapter.NotifyDpiChangedForSubtree(this, change, failures)); }
        catch (Exception failure) { failures.Add(failure); }
        if (IsCurrentDpiChange(change)) {
            try { Invalidate(); }
            catch (Exception failure) { failures.Add(failure); }
            if (IsCurrentDpiChange(change)) {
                try { OnDpiChanged(change); }
                catch (Exception failure) { failures.Add(failure); }
            }
        }
        Control.ThrowDpiFailures(failures);
    }
}
