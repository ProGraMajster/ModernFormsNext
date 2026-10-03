using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    // Notification identity/parent generation only: Scaling still reads the owning backend.
    private DpiChangedEventArgs? dpiNotification;
    private int dpiParentVersion;

    /// <summary>Occurs when the owning window confirms a different rendering scale for this control.</summary>
    /// <remarks>
    /// Raised on the UI thread through OnDpiChanged after this control's DPI caches are invalidated
    /// and its base layout request has run (or been deferred by SuspendLayout). Scaling and DeviceDpi
    /// already reflect NewScale and NewDpi. The existing traversal is parent-first; descendants have
    /// not necessarily completed their own DPI hooks yet. Identical scales do not notify again.
    /// Detached, disposed or superseded controls are skipped. This is not a monitor-change event or
    /// a WinForms BeforeParent/AfterParent phase. Android surface density is not bridged here.
    /// </remarks>
    public event EventHandler<DpiChangedEventArgs>? DpiChanged;

    /// <summary>Refreshes device-dependent measurements after the owning window's DPI changes.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>
    /// Called on the UI thread after the new window scale is available. Logical bounds, padding
    /// and font sizes are unchanged. The base method invalidates painting and requests layout;
    /// overrides must discard cached device-space text or geometry before calling base, which
    /// then raises DpiChanged. The existing EventArgs signature is preserved; framework transitions
    /// pass DpiChangedEventArgs. Logical bounds are not rescaled by this hook.
    /// </remarks>
    protected virtual void OnDpiChanged(EventArgs e)
    {
        Invalidate();
        PerformLayout();
        if (this is ControlAdapter || e is not DpiChangedEventArgs change || DpiChanged is not { } handlers)
            return;
        var window = FindWindow();
        if (window is null) return;
        long visibility = window.VisibilityOperationVersion;
        List<Exception> failures = [];
        foreach (EventHandler<DpiChangedEventArgs> handler in handlers.GetInvocationList()) {
            if (!IsCurrentDpiNotification(window, change) || !window.IsCurrentShowRequest(visibility)) break;
            try { handler(this, change); }
            catch (Exception failure) { failures.Add(failure); }
        }
        ThrowDpiFailures(failures);
    }

    internal void NotifyDpiChangedForSubtree(WindowBase window, DpiChangedEventArgs change, List<Exception> failures)
    {
        if (IsDisposed || Disposing || !ReferenceEquals(FindWindow(), window) ||
            !window.IsCurrentDpiChange(change) || ReferenceEquals(dpiNotification, change)) return;
        dpiNotification = change;
        dpiParentVersion = parentAssignmentVersion;
        // Snapshot before user code. A removal/reparent (even away and back) retires that old
        // child route; the notification identity prevents a move to a later subtree firing twice.
        var children = Controls.GetAllControls().Select(child => (child, child.parentAssignmentVersion)).ToArray();
        FreeBackBuffer();
        CommonProperties.xClearPreferredSizeCache(this);
        try { OnDpiChanged(change); }
        catch (Exception failure) { failures.Add(failure); }
        foreach (var (child, assignment) in children) {
            if (!IsCurrentDpiNotification(window, change)) break;
            if (ReferenceEquals(child.Parent, this) && child.parentAssignmentVersion == assignment)
                child.NotifyDpiChangedForSubtree(window, change, failures);
        }
    }

    private bool IsCurrentDpiNotification(WindowBase window, DpiChangedEventArgs change) =>
        !IsDisposed && !Disposing && dpiParentVersion == parentAssignmentVersion &&
        ReferenceEquals(dpiNotification, change) && ReferenceEquals(FindWindow(), window) &&
        window.IsCurrentDpiChange(change);

    internal static void ThrowDpiFailures(List<Exception> failures)
    {
        if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("DPI callbacks or layout failed.", failures);
    }
}
