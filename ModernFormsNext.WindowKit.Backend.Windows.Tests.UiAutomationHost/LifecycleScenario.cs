using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Threading;

// This opt-in mode runs in its own process so all native windows, subscriptions and
// Application.Run belong to one real UI thread, independently of xUnit worker threads.
internal static class LifecycleScenario
{
    internal static int Run()
    {
        try
        {
            using var main = new Form { Text = "ModernFormsNext lifecycle integration", ClientSize = new Size(320, 180) };
            Form? secondary = null;
            Exception? scenarioFailure = null;
            var events = new List<string>();
            int activations = 0;
            int exitCalls = 0;
            bool inactiveAnimationContinues = false;
            bool suspendPausesAnimation = false;
            bool resumeRestartsAnimation = false;
            bool twoWindowsObserved = false;
            bool windowActivationObserved = false;
            var lifecycle = Application.Lifecycle;
            lifecycle.LifecycleChanged += (_, e) => events.Add($"Phase:{e.Current.Phase}");
            lifecycle.ActivationReceived += (_, _) => activations++;
            lifecycle.StateSaving += (_, e) => events.Add($"Save:{e.Reason}");
            Application.OnExit += (_, _) => { exitCalls++; events.Add("OnExit"); };

            // Post from Shown so the scenario executes after the real native message loop
            // starts; simulated session end must cancel that loop, not merely skip startup.
            main.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    Require(lifecycle.Snapshot.Phase == PlatformApplicationPhase.Running, "Run did not enter Running before Shown.");
                    Require(activations == 1, "Application.Run must deliver initial activation exactly once.");
                    Require(lifecycle.Snapshot.HostCount == 1, "The first Form must own one native host.");
                    secondary = new Form { Text = "ModernFormsNext second lifecycle host", ClientSize = new Size(240, 160) };
                    secondary.Show();
                    twoWindowsObserved = lifecycle.Snapshot.HostCount == 2 && Application.OpenForms.Count == 2;
                    Require(twoWindowsObserved, "Two native Form hosts were not reported.");

                    SendOwned(main.PlatformHandle.Handle, 0x0006, 0); // WM_ACTIVATE / WA_INACTIVE
                    SendOwned(secondary.PlatformHandle.Handle, 0x0006, 1); // WA_ACTIVE
                    windowActivationObserved = !main.IsActive && secondary.IsActive;
                    Require(windowActivationObserved, "Native window activation did not reach WindowBase.");

                    var scheduler = AnimationScheduler.Default;
                    using var animation = scheduler.Start(main, "native-lifecycle-probe", _ => { },
                        new AnimationOptions { Duration = TimeSpan.FromSeconds(30) });
                    SendOwned(main.PlatformHandle.Handle, 0x001C, 0); // WM_ACTIVATEAPP / inactive process
                    inactiveAnimationContinues = lifecycle.Snapshot.State == PlatformApplicationLifecycleState.Foreground &&
                        !lifecycle.Snapshot.IsActive && !scheduler.GetDiagnostics().IsPaused;
                    Require(inactiveAnimationContinues, "Desktop deactivation incorrectly paused foreground animation work.");

                    nint messageWindow = FindOwnedMessageWindow();
                    events.Clear();
                    SendOwned(messageWindow, 0x0218, 4); // WM_POWERBROADCAST / PBT_APMSUSPEND
                    suspendPausesAnimation = lifecycle.Snapshot.Phase == PlatformApplicationPhase.Suspended &&
                        scheduler.GetDiagnostics().IsPaused && !scheduler.GetDiagnostics().IsTickSourceRunning;
                    Require(suspendPausesAnimation, "Native suspend did not pause the registered animation scheduler.");
                    Require(events.SequenceEqual(["Save:Suspend", "Phase:Suspended"]), "State save must precede native suspension.");
                    SendOwned(messageWindow, 0x0218, 18); // PBT_APMRESUMEAUTOMATIC
                    SendOwned(messageWindow, 0x0218, 7); // duplicate interactive resume
                    resumeRestartsAnimation = lifecycle.Snapshot.Phase == PlatformApplicationPhase.Running &&
                        !scheduler.GetDiagnostics().IsPaused;
                    Require(resumeRestartsAnimation, "Native resume did not resume the registered scheduler.");
                    Require(events.Count(item => item == "Phase:Running") == 1, "Duplicate native resume was not normalized.");

                    secondary.Close();
                    Require(lifecycle.Snapshot.HostCount == 1, "Closing the secondary window lost the main native host.");
                    events.Clear();
                    SendOwned(messageWindow, 0x0011, 0); // WM_QUERYENDSESSION: save opportunity only
                    SendOwned(messageWindow, 0x0016, 0); // WM_ENDSESSION(FALSE): canceled OS shutdown
                    Require(exitCalls == 0 && lifecycle.Snapshot.Phase == PlatformApplicationPhase.Running,
                        "Canceled native shutdown ended Application.Run.");
                    Require(events.SequenceEqual(["Save:Exit"]), "Native shutdown query did not request explicit state.");
                    events.Clear();
                    // This is a direct message to our own hidden window. It never asks Windows
                    // to log off, shut down, suspend, or broadcast a message to other applications.
                    SendOwned(messageWindow, 0x0016, 1);
                    Require(lifecycle.Snapshot.Phase == PlatformApplicationPhase.Exiting,
                        "Native session end did not publish managed exit intent.");
                }
                catch (Exception exception)
                {
                    scenarioFailure = exception;
                    Application.Exit();
                }
            });

            Application.Run(main);
            secondary?.Dispose();
            if (scenarioFailure is not null) throw new InvalidOperationException("Native lifecycle scenario failed.", scenarioFailure);
            Require(exitCalls == 1, "Native exit must invoke OnExit exactly once.");
            Require(lifecycle.Snapshot.Phase == PlatformApplicationPhase.Exited, "The real application loop did not complete exit.");
            Require(events.SequenceEqual(["Phase:Exiting", "OnExit", "Phase:Exited"]),
                $"Unexpected managed exit order: {string.Join(", ", events)}");
            Console.WriteLine("LIFECYCLE:" + JsonSerializer.Serialize(new
            {
                Activations = activations, ExitCalls = exitCalls, TwoWindowsObserved = twoWindowsObserved,
                WindowActivationObserved = windowActivationObserved,
                InactiveAnimationContinues = inactiveAnimationContinues,
                SuspendPausesAnimation = suspendPausesAnimation, ResumeRestartsAnimation = resumeRestartsAnimation,
                FinalPhase = lifecycle.Snapshot.Phase.ToString(), ExitOrder = events
            }));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static nint FindOwnedMessageWindow()
    {
        nint found = 0;
        EnumThreadWindows(GetCurrentThreadId(), (window, _) =>
        {
            var name = new StringBuilder(256);
            _ = GetClassName(window, name, name.Capacity);
            if (!name.ToString().StartsWith("AvaloniaMessageWindow ", StringComparison.Ordinal)) return true;
            found = window;
            return false;
        }, 0);
        Require(found != 0, "Unable to locate the current UI thread's platform message window.");
        return found;
    }

    private static void SendOwned(nint window, uint message, nint value)
    {
        uint thread = GetWindowThreadProcessId(window, out uint process);
        Require(window != 0 && process == Environment.ProcessId && thread == GetCurrentThreadId(),
            "Native lifecycle tests may message only their own UI-thread windows.");
        _ = SendMessage(window, message, value, 0);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private delegate bool EnumWindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumThreadWindows(uint threadId, EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, StringBuilder name, int capacity);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
