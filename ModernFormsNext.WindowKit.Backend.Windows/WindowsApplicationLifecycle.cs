using ModernFormsNext.WindowKit.Backend.Lifecycle;
using System.Diagnostics;

namespace ModernFormsNext.WindowKit.Backend.Windows;

// One publisher is registered under the existing lifecycle key. Window focus remains distinct
// from process activation and from power suspension: deactivation does not pause desktop work.
internal sealed class WindowsApplicationLifecycle
{
    private readonly HashSet<nint> windows = [];
    private readonly Action<Action> dispatch;
    private bool active;
    private bool suspended;
    private PlatformApplicationPhase phaseBeforeSuspension;
    private long generation;

    internal WindowsApplicationLifecycle(Action verifyAccess, Action<Action> dispatch)
    {
        this.dispatch = dispatch;
        Publisher = new PlatformApplicationLifecyclePublisher(verifyAccess,
            new PlatformApplicationLifecycleSnapshot(PlatformApplicationPhase.NotStarted,
                PlatformApplicationLifecycleState.NoHost));
    }

    internal PlatformApplicationLifecyclePublisher Publisher { get; }

    internal void WindowCreated(nint handle) => Notify(() =>
    {
        if (handle == 0 || !windows.Add(handle)) return;
        generation++;
        Publish();
    });

    internal void WindowDestroyed(nint handle) => Notify(() =>
    {
        if (!windows.Remove(handle)) return;
        if (windows.Count == 0) active = false;
        Publish();
    });

    internal void ApplicationActivated(bool value) => Notify(() =>
    {
        active = value;
        Publish();
    });

    internal void PowerChanged(int nativeEvent) => Notify(() =>
    {
        // Win32 PBT_APMSUSPEND, PBT_APMRESUMECRITICAL, PBT_APMRESUMESUSPEND and
        // PBT_APMRESUMEAUTOMATIC. Windows can send both automatic and interactive resume.
        if (nativeEvent == 4)
        {
            if (suspended) return;
            phaseBeforeSuspension = Publisher.Snapshot.Phase;
            suspended = true;
            try { Publisher.RequestSaveState(PlatformApplicationStateReason.Suspend); }
            finally { Publish(); }
        }
        else if (nativeEvent is 6 or 7 or 18)
        {
            if (!suspended) return;
            suspended = false;
            Publish();
        }
    });

    internal void SessionEndRequested() => Notify(() => Publisher.RequestSaveState(PlatformApplicationStateReason.Exit));

    internal void SessionEnded(bool ending) => Notify(() =>
    {
        if (!ending) return; // WM_ENDSESSION(FALSE) means shutdown was canceled.
        // Native shutdown is an exit request, not proof that managed cleanup has completed.
        // Application.Run owns cancellation, OnExit and the eventual Exited notification.
        var current = Publisher.Snapshot;
        if (current.Phase is PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited) return;
        Publisher.Publish(new PlatformApplicationLifecycleSnapshot(PlatformApplicationPhase.Exiting,
            current.State, false, current.HostCount, current.HostGeneration));
    });

    private void Publish()
    {
        var current = Publisher.Snapshot;
        if (current.Phase is PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited) return;
        // Host creation can precede Application.Run. It cannot start the application or replay
        // its launch activation; ordinary native updates preserve the application's phase.
        var phase = suspended ? PlatformApplicationPhase.Suspended
            : current.Phase == PlatformApplicationPhase.Suspended ? phaseBeforeSuspension : current.Phase;
        Publisher.Publish(new PlatformApplicationLifecycleSnapshot(phase,
            windows.Count == 0 ? PlatformApplicationLifecycleState.NoHost
                : suspended ? PlatformApplicationLifecycleState.Background : PlatformApplicationLifecycleState.Foreground,
            active && !suspended && windows.Count > 0, windows.Count, generation));
    }

    private void Notify(Action action)
    {
        try
        {
            dispatch(() =>
            {
                try { action(); }
                catch (Exception exception) { Report(exception); }
            });
        }
        catch (Exception exception) { Report(exception); }
    }

    private static void Report(Exception exception)
    {
        try { Trace.WriteLine($"ModernFormsNext application lifecycle callback failed ({exception.GetType().Name})."); }
        catch { /* A custom trace listener must not unwind a native lifecycle callback. */ }
    }
}
