using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Threading;

// State changes operate on real owned HWNDs, without mouse automation or fake callbacks.
internal static class WindowStateScenario
{
    internal static int Run()
    {
        try {
            using var owner = new Form { StartPosition = FormStartPosition.Manual };
            Exception? failure = null;
            owner.Shown += (_, _) => Dispatcher.UIThread.Post(() => {
                try {
                    foreach (bool systemChrome in new[] { false, true }) {
                        Transitions(systemChrome);
                        Reentrancy(systemChrome);
                        CancelShow(owner, systemChrome, false);
                        CancelShow(owner, systemChrome, true);
                    }
                }
                catch (Exception error) { failure = error; }
                finally { owner.Close(); }
            });
            Application.Run(owner);
            if (failure is not null) throw failure;
            Console.WriteLine("WINDOW_STATE:PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Transitions(bool systemChrome)
    {
        using var form = new Form { UseSystemDecorations = systemChrome, StartPosition = FormStartPosition.Manual,
            Size = new Size(450, 320) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        nint hwnd = form.PlatformHandle.Handle;
        Require(GetWindowThreadProcessId(hwnd, out uint process) == GetCurrentThreadId() && process == Environment.ProcessId,
            "State regression must use an owned UI-thread HWND.");
        List<(FormWindowState Old, FormWindowState New)> changes = [];
        List<string> lifecycle = [];
        List<string> order = [];
        Exception? observerFailure = null;
        form.Load += (_, _) => lifecycle.Add("Load");
        form.VisibleChanged += (_, _) => lifecycle.Add(form.Visible ? "Visible" : "Hidden");
        form.Shown += (_, _) => lifecycle.Add("Shown");
        form.Resize += (_, _) => order.Add("Resize");
        form.SizeChanged += (_, _) => order.Add("Size");
        form.ClientSizeChanged += (_, _) => order.Add("Client");
        form.WindowStateChanged += (_, e) => {
            changes.Add((e.OldState, e.NewState));
            lifecycle.Add("State");
            order.Add("State");
            // Assertions are captured here and thrown after the native call, never through
            // reverse P/Invoke. Observer exception propagation is tested in the headless host.
            try {
                Require(form.WindowState == e.NewState, $"Getter {form.WindowState} differs from NewState {e.NewState}.");
                Require(IsWindowVisible(hwnd), "Hidden configuration emitted a state event.");
                Require((e.NewState == FormWindowState.Minimized) == IsIconic(hwnd), "Minimized state differs from HWND.");
                if (e.NewState != FormWindowState.Minimized) {
                    Require((e.NewState == FormWindowState.Maximized) == IsZoomed(hwnd), "Maximized state differs from HWND.");
                    Require(fill.Size == form.ClientSize, "State event preceded applicable client layout.");
                }
            }
            catch (Exception error) { observerFailure ??= error; }
        };
        void Check(FormWindowState oldState, FormWindowState newState) {
            if (observerFailure is not null) throw observerFailure;
            Require(changes.SequenceEqual([(oldState, newState)]),
                $"Expected {oldState}>{newState}; got {string.Join(",", changes)}; getter={form.WindowState}, iconic={IsIconic(hwnd)}, chrome={systemChrome}.");
            changes.Clear();
        }

        form.WindowState = FormWindowState.Maximized;
        Require(form.WindowState == FormWindowState.Maximized && changes.Count == 0 && !IsWindowVisible(hwnd), "Pre-show configuration contract failed.");
        form.Show();
        Check(FormWindowState.Normal, FormWindowState.Maximized);
        Require(lifecycle.SequenceEqual(["Load", "Visible", "State", "Shown"]), "First-show lifecycle changed: " + string.Join(",", lifecycle));
        Require(order.Last() == "State" && order.Contains("Resize") && order.IndexOf("Resize") < order.IndexOf("Size"), "State did not follow resize/layout.");
        form.WindowState = form.WindowState;
        Require(changes.Count == 0, "No-op state emitted a duplicate.");
        form.WindowState = FormWindowState.Normal;
        Check(FormWindowState.Maximized, FormWindowState.Normal);

        // WM_SYSCOMMAND exercises the system menu/native title-bar path into WM_SIZE.
        _ = SendMessage(hwnd, 0x0112, 0xF030, 0);
        Check(FormWindowState.Normal, FormWindowState.Maximized);
        _ = SendMessage(hwnd, 0x0112, 0xF120, 0);
        Check(FormWindowState.Maximized, FormWindowState.Normal);
        _ = SendMessage(hwnd, 0x0112, 0xF020, 0);
        Check(FormWindowState.Normal, FormWindowState.Minimized);
        form.WindowState = form.WindowState;
        Require(changes.Count == 0, "Minimized no-op emitted a duplicate.");
        _ = SendMessage(hwnd, 0x0112, 0xF120, 0);
        Check(FormWindowState.Minimized, FormWindowState.Normal);
        form.WindowState = FormWindowState.Maximized;
        Check(FormWindowState.Normal, FormWindowState.Maximized);
        form.WindowState = FormWindowState.Minimized;
        Check(FormWindowState.Maximized, FormWindowState.Minimized);
        _ = SendMessage(hwnd, 0x0112, 0xF120, 0);
        Check(FormWindowState.Minimized, FormWindowState.Maximized);
        form.Hide(); form.Show();
        Require(form.WindowState == FormWindowState.Maximized && changes.Count == 0, "Native state was lost or duplicated across Hide/Show.");

        form.Hide();
        form.WindowState = FormWindowState.Normal;
        form.WindowState = FormWindowState.Minimized;
        Require(changes.Count == 0 && form.WindowState == FormWindowState.Minimized, "Hidden configuration was reported prematurely.");
        form.Show();
        Check(FormWindowState.Maximized, FormWindowState.Minimized);
        form.Close();
        Console.WriteLine("WINDOW_STATE:NATIVE:" + systemChrome);
    }

    private static void Reentrancy(bool systemChrome)
    {
        using var form = new Form { UseSystemDecorations = systemChrome, StartPosition = FormStartPosition.Manual };
        form.Show();
        List<string> changes = [];
        EventHandler<WindowStateChangedEventArgs> change = (_, e) => {
            changes.Add($"{e.OldState}>{e.NewState}");
            if (e.NewState == FormWindowState.Maximized) form.WindowState = FormWindowState.Normal;
        };
        EventHandler<WindowStateChangedEventArgs> later = (_, e) => changes.Add("last:" + e.NewState);
        form.WindowStateChanged += change;
        form.WindowStateChanged += later;
        form.WindowState = FormWindowState.Maximized;
        Require(changes.SequenceEqual(["Normal>Maximized", "Maximized>Normal", "last:Normal"]) && form.WindowState == FormWindowState.Normal,
            "Reentrant state observer left an obsolete state: " + string.Join(",", changes));
        form.Hide(); form.Show();
        Require(form.WindowState == FormWindowState.Normal && changes.Count == 3, "Older setter overwrote reentrant state for the next Show.");
        form.WindowStateChanged -= change;
        form.WindowStateChanged -= later;
        changes.Clear();
        form.WindowStateChanged += later;
        EventHandler resize = (_, _) => { if (form.WindowState == FormWindowState.Maximized) form.WindowState = FormWindowState.Normal; };
        form.Resize += resize;
        form.WindowState = FormWindowState.Maximized;
        Require(form.WindowState == FormWindowState.Normal && changes.Count == 0, "WM_SIZE published a state superseded by a geometry observer.");
        form.Resize -= resize;
        EventHandler noOp = (_, _) => form.WindowState = form.WindowState;
        form.Resize += noOp;
        form.WindowState = FormWindowState.Maximized;
        Require(changes.SequenceEqual(["last:Maximized"]), "No-op state assignment from geometry suppressed confirmation.");
        form.Close();
        Console.WriteLine("WINDOW_STATE:REENTRANCY:" + systemChrome);
    }

    private static void CancelShow(Form owner, bool systemChrome, bool close)
    {
        using var form = new Form { UseSystemDecorations = systemChrome, WindowState = FormWindowState.Maximized };
        nint hwnd = form.PlatformHandle.Handle;
        int later = 0, shown = 0;
        form.WindowStateChanged += (_, _) => { if (close) form.Close(); else form.Hide(); };
        form.WindowStateChanged += (_, _) => later++;
        form.Shown += (_, _) => shown++;
        var completion = form.ShowDialog(owner);
        Require(completion.IsCompletedSuccessfully && IsWindowEnabled(owner.PlatformHandle.Handle), "State observer retained modal owner input.");
        Require(!form.Visible && !IsWindowVisible(hwnd) && later == 0 && shown == 0 && !Application.OpenForms.Contains(form), "Canceled state show continued stale lifecycle.");
        if (close) Require(!IsWindow(hwnd), "Closed HWND was resurrected.");
        Console.WriteLine("WINDOW_STATE:CANCEL:" + close);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowEnabled(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
}
