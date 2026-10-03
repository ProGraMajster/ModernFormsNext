using System.Runtime.ExceptionServices;
using ModernFormsNext.WindowKit.Controls;

namespace ModernFormsNext;

public partial class Form
{
    // A notification baseline, not a second state model: WindowState still reads the backend.
    private FormWindowState notifiedWindowState;
    private long windowStateNotificationVersion;

    /// <summary>Occurs after the backend confirms a different <see cref="WindowState"/>.</summary>
    /// <remarks>
    /// Raised synchronously on the UI thread. The handler reads NewState from WindowState.
    /// Identical confirmations and no-op assignments do not raise another event. Configuring
    /// a hidden window (including before first Show) changes the existing getter but does not
    /// raise this event until the backend applies/confirms that state when shown. OldState is
    /// the last confirmed notification state, initially the newly created backend's state.
    /// This notification is independent of geometry, visibility and activation. Windows normally
    /// delivers applicable geometry notifications first; no universal cross-backend order is promised.
    /// Reentrant state changes, Hide and Close supersede remaining older observers. State is not
    /// rolled back after an observer failure; the existing backend exception policy applies.
    /// This event does not describe Android Activity or orientation lifecycle.
    /// </remarks>
    public event EventHandler<WindowStateChangedEventArgs>? WindowStateChanged;

    /// <summary>Raises <see cref="WindowStateChanged"/> for an already confirmed state.</summary>
    /// <param name="e">The previous and newly confirmed states.</param>
    /// <remarks>Called on the UI thread. Overrides must call base to notify subscribers.</remarks>
    protected virtual void OnWindowStateChanged(WindowStateChangedEventArgs e)
    {
        long version = windowStateNotificationVersion;
        long visibility = VisibilityOperationVersion;
        List<Exception> failures = [];
        if (WindowStateChanged is { } handlers)
            foreach (EventHandler<WindowStateChangedEventArgs> handler in handlers.GetInvocationList()) {
                if (IsBackendClosed || !IsCurrentShowRequest(visibility) ||
                    version != windowStateNotificationVersion || WindowState != e.NewState) break;
                try { handler(this, e); }
                catch (Exception error) { failures.Add(error); }
            }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Window state callbacks failed.", failures);
    }

    private void OnBackendWindowStateChanged(WindowState state)
    {
        if (IsBackendClosed) return;
        var current = (FormWindowState)state;
        // Reject delayed payloads and unsupported backend states (Form has no fullscreen API).
        if (current is not (FormWindowState.Normal or FormWindowState.Minimized or FormWindowState.Maximized) ||
            WindowState != current || notifiedWindowState == current) return;
        var args = new WindowStateChangedEventArgs(notifiedWindowState, current);
        notifiedWindowState = current;
        windowStateNotificationVersion++;
        OnWindowStateChanged(args);
    }
}
