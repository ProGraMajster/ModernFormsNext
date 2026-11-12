using System.Runtime.ExceptionServices;

namespace ModernFormsNext;

public abstract partial class WindowBase
{
    // The existing base events and Form's paired setters share this generation. Checking values
    // alone would revive an old batch if a callback changed the limits and then restored them.
    private long sizeConstraintVersion;
    private long? publishingSizeConstraintVersion;

    internal long AdvanceSizeConstraintVersion() => unchecked(++sizeConstraintVersion);

    internal bool IsCurrentSizeConstraintVersion(long version)
        => version == sizeConstraintVersion && !IsBackendClosed && !InputBindingsClosed;

    internal void RaiseSizeConstraintNotification(bool minimum, long version)
    {
        if (!IsCurrentSizeConstraintVersion(version)) return;
        // A derived hook may replace the constraints before calling base. Retain the originating
        // generation across the virtual call, including nested notifications, so base does not
        // mistake the superseded outer hook for another notification of the newer pair.
        long? previous = publishingSizeConstraintVersion;
        publishingSizeConstraintVersion = version;
        try {
            if (minimum) OnMinimumSizeChanged(EventArgs.Empty);
            else OnMaximumSizeChanged(EventArgs.Empty);
        }
        finally { publishingSizeConstraintVersion = previous; }
    }

    private void NotifySizeConstraintObservers(EventHandler? handlers, EventArgs e)
    {
        if (handlers is null) return;
        long version = publishingSizeConstraintVersion ?? sizeConstraintVersion;
        List<Exception>? failures = null;
        foreach (EventHandler handler in handlers.GetInvocationList()) {
            if (!IsCurrentSizeConstraintVersion(version)) break;
            try { handler(this, e); }
            catch (Exception failure) { (failures ??= []).Add(failure); }
        }
        ThrowSizeConstraintFailures(failures);
    }

    internal static void ThrowSizeConstraintFailures(List<Exception>? failures)
    {
        if (failures?.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures?.Count > 1) throw new AggregateException("Window size constraint callbacks failed.", failures);
    }
}
