using System.Drawing;
using System.Runtime.ExceptionServices;

namespace ModernFormsNext;

public abstract partial class WindowBase
{
    // Notification baselines only. Public geometry continues to read the backend (and
    // Form's managed chrome); callback payloads never overwrite that canonical state.
    private Size geometrySize;
    private Point geometryLocation;
    private Size? geometryClientSize;
    private long geometryVersion;
    private int geometryLayoutDepth;
    private bool geometryLayoutPending;

    /// <summary>Occurs after a change to <see cref="Size"/> has been applied to window layout.</summary>
    /// <remarks>
    /// Raised on the UI thread before SizeChanged. Size, adapter bounds and child layout
    /// already reflect the backend's actual logical-pixel size. An unchanged Size does not
    /// raise Resize, including repeated backend callbacks and scale-only changes.
    /// Geometry may change during Load or startup positioning, before the first display.
    /// </remarks>
    public event EventHandler? Resize;

    /// <summary>Occurs when the actual logical-pixel <see cref="Size"/> changes.</summary>
    /// <remarks>
    /// Raised on the UI thread after layout and Resize. A programmatic request and its
    /// backend confirmation share this notification path. Reentrant geometry, Hide or Close
    /// supersedes remaining notifications of an older operation. Observer failures propagate
    /// after state and layout commit; the existing backend exception policy still applies.
    /// </remarks>
    public event EventHandler? SizeChanged;

    /// <summary>Occurs when the actual <see cref="Location"/> changes in physical screen pixels.</summary>
    /// <remarks>
    /// Raised on the UI thread for programmatic and backend-reported moves, including startup
    /// centering. Negative coordinates are preserved and no additional DPI scaling is applied.
    /// The handler reads the committed Location; repeated values do not raise this event.
    /// </remarks>
    public event EventHandler? LocationChanged;

    /// <summary>Raises <see cref="Resize"/> after the committed size has been laid out.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>Called on the UI thread. Overrides must call base to notify subscribers.</remarks>
    protected virtual void OnResize(EventArgs e) => NotifyGeometryObservers(Resize, e);

    /// <summary>Raises <see cref="SizeChanged"/> after Resize for the same committed size.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>Called on the UI thread. Overrides must call base to notify subscribers.</remarks>
    protected virtual void OnSizeChanged(EventArgs e) => NotifyGeometryObservers(SizeChanged, e);

    /// <summary>Raises <see cref="LocationChanged"/> after the physical screen position commits.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>Called on the UI thread. Overrides must call base to notify subscribers.</remarks>
    protected virtual void OnLocationChanged(EventArgs e) => NotifyGeometryObservers(LocationChanged, e);

    internal virtual Size? GeometryClientSize => null;
    internal virtual void NotifyClientSizeChanged() { }

    internal void UpdateWindowGeometry(Action? update = null)
    {
        if (backendClosed) return;
        if (geometryLayoutDepth > 0 || adapter.IsLayoutSuspended) {
            // A duplicate native callback inside layout must not request another identical
            // pass indefinitely. A move needs no second layout; publication reads its final
            // position after this pass. DPI updates may still require a fresh layout.
            var bounds = DisplayRectangle;
            bounds.Width = Math.Max(0, bounds.Width);
            bounds.Height = Math.Max(0, bounds.Height);
            if (adapter.Bounds != bounds || update is not null) geometryLayoutPending = true;
            update?.Invoke();
            // Also defer move-only publication until an explicitly suspended root resumes.
            if (adapter.IsLayoutSuspended) adapter.PerformLayout();
            return;
        }

        geometryLayoutDepth++;
        try {
            update?.Invoke();
            do {
                geometryLayoutPending = false;
                if (backendClosed) return;
                // Native resize and chrome updates use the same root layout. If layout
                // callbacks change backend geometry, finish another pass before publishing.
                adapter.PerformLayout();
            } while (!backendClosed && geometryLayoutPending);
        }
        finally { geometryLayoutDepth--; }
        PublishGeometryChanges();
    }

    internal void OnWindowLayoutCompleted()
    {
        if (backendClosed || geometryLayoutDepth > 0) return;
        if (geometryLayoutPending) UpdateWindowGeometry();
        else PublishGeometryChanges();
    }

    private void PublishGeometryChanges()
    {
        if (backendClosed) return;
        Size size = Size;
        Point location = Location;
        Size? clientSize = GeometryClientSize;
        bool resized = size != geometrySize;
        bool moved = location != geometryLocation;
        bool clientResized = geometryClientSize.HasValue && clientSize != geometryClientSize;
        geometrySize = size;
        geometryLocation = location;
        geometryClientSize = clientSize;
        if (!resized && !moved && !clientResized) return;

        long version = ++geometryVersion;
        long visibility = visibilityVersion;
        List<Exception> failures = [];
        void Notify(Action action)
        {
            if (!IsCurrentGeometry(version, visibility)) return;
            try { action(); }
            catch (Exception exception) { failures.Add(exception); }
        }
        if (resized) {
            Notify(() => OnResize(EventArgs.Empty));
            Notify(() => OnSizeChanged(EventArgs.Empty));
        }
        if (clientResized) Notify(NotifyClientSizeChanged);
        if (moved) Notify(() => OnLocationChanged(EventArgs.Empty));
        ThrowGeometryFailures(failures);
    }

    internal void NotifyGeometryObservers(EventHandler? handlers, EventArgs e)
    {
        if (handlers is null) return;
        long version = geometryVersion;
        long visibility = visibilityVersion;
        List<Exception> failures = [];
        foreach (EventHandler handler in handlers.GetInvocationList()) {
            if (!IsCurrentGeometry(version, visibility)) break;
            try { handler(this, e); }
            catch (Exception exception) { failures.Add(exception); }
        }
        ThrowGeometryFailures(failures);
    }

    private bool IsCurrentGeometry(long version, long visibility) => !backendClosed &&
        version == geometryVersion && visibility == visibilityVersion &&
        // A handler can suspend root layout before changing backend geometry. In that
        // case the next publication/version is deferred, but old observers are stale now.
        Size == geometrySize && Location == geometryLocation && GeometryClientSize == geometryClientSize;

    private static void ThrowGeometryFailures(List<Exception> failures)
    {
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Window geometry callbacks failed.", failures);
    }
}
