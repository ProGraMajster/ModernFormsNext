using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Threading;

// Real owned HWNDs, moved/resized through SetWindowPos. No mouse automation or fake
// framework callback invocation; the operating system produces WM_MOVE / WM_SIZE.
internal static class WindowGeometryScenario
{
    internal static int Run()
    {
        try {
            using var owner = new Form { Text = "Window geometry regression", StartPosition = FormStartPosition.Manual };
            Exception? failure = null;
            owner.Shown += (_, _) => Dispatcher.UIThread.Post(() => {
                try {
                    Exercise(false);
                    Exercise(true);
                    CancelAndClose(owner);
                }
                catch (Exception error) { failure = error; }
                finally { owner.Close(); }
            });
            Application.Run(owner);
            if (failure is not null) throw failure;
            Console.WriteLine("WINDOW_GEOMETRY:PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Exercise(bool systemChrome)
    {
        using var form = new Form { UseSystemDecorations = systemChrome, StartPosition = FormStartPosition.Manual,
            Size = new Size(400, 300), Location = new Point(80, 90) };
        using var fill = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var nested = fill.Controls.Add(new Panel { Dock = DockStyle.Fill });
        form.Show();
        nint hwnd = form.PlatformHandle.Handle;
        Require(GetWindowThreadProcessId(hwnd, out uint process) == GetCurrentThreadId() && process == Environment.ProcessId,
            "Geometry operations must target an owned UI-thread HWND.");
        List<string> sizeEvents = [];
        List<Point> positions = [];
        string stage = "programmatic";
        // Keep assertions out of reverse P/Invoke callbacks: report them after the native
        // operation returns, preserving the existing backend exception policy.
        Exception? observationFailure = null;
        void Observe(string name) {
            sizeEvents.Add(name);
            try {
                Require(GetClientRect(hwnd, out var native), "GetClientRect failed.");
                Require(form.Size == new Size((int)(native.Width / form.Scaling), (int)(native.Height / form.Scaling)), "Size differs from native drawable size.");
                Require(fill.Size == form.ClientSize && nested.Size == fill.Size,
                    $"Geometry event ran before nested layout: {stage}/{name}, size={form.Size}, client={form.ClientSize}, fill={fill.Size}, nested={nested.Size}, scale={form.Scaling}, state={form.WindowState}.");
            }
            catch (Exception error) { observationFailure ??= error; }
        }
        form.Resize += (_, _) => Observe("Resize");
        form.SizeChanged += (_, _) => Observe("Size");
        form.ClientSizeChanged += (_, _) => Observe("Client");
        form.LocationChanged += (_, _) => positions.Add(form.Location);
        void CheckOrder() {
            if (observationFailure is not null) throw observationFailure;
            Require(sizeEvents.SequenceEqual(["Resize", "Size", "Client"]), "Unexpected geometry order: " + string.Join(",", sizeEvents));
            sizeEvents.Clear();
        }

        form.Size = new Size(500, 360);
        CheckOrder();
        form.Size = form.Size;
        form.ClientSize = form.ClientSize;
        Require(sizeEvents.Count == 0, "No-op programmatic size produced an event.");

        Require(GetWindowRect(hwnd, out var rect), "GetWindowRect failed.");
        stage = "native resize";
        _ = SendMessage(hwnd, 0x0231, 0, 0); // Existing enter/exit move-size lifecycle.
        Require(SetWindowPos(hwnd, 0, 0, 0, rect.Width + 120, rect.Height + 90, 0x0016), "Native resize failed.");
        _ = SendMessage(hwnd, 0x0232, 0, 0);
        CheckOrder();
        Console.WriteLine("WINDOW_GEOMETRY:NATIVE_RESIZE:" + systemChrome);

        positions.Clear();
        form.Location = new Point(-180, -120);
        Require(positions.SequenceEqual([form.Location]) && form.Location == new Point(-180, -120), "Programmatic physical location diverged.");
        form.Location = form.Location;
        Require(positions.Count == 1, "No-op move produced another notification.");
        positions.Clear();
        Require(SetWindowPos(hwnd, 0, -240, 50, 0, 0, 0x0015), "Native move failed.");
        Require(positions.SequenceEqual([form.Location]), "Native move notification did not use public Location.");
        Require(form.Location.X < 0, "Negative physical position was lost.");
        Require(sizeEvents.Count == 0, "Move invented a size change.");
        Console.WriteLine("WINDOW_GEOMETRY:NATIVE_MOVE:" + systemChrome);

        form.MinimumSize = new Size(300, 220);
        form.MaximumSize = new Size(700, 500);
        sizeEvents.Clear();
        stage = "minimum";
        form.Size = new Size(10, 10);
        Require(form.Size.Width >= 300 && form.Size.Height >= 220, "Minimum tracking constraint was not applied.");
        CheckOrder();
        form.Size = new Size(10, 10);
        Require(sizeEvents.Count == 0, "An identical clamped result produced duplicate events.");
        stage = "maximum";
        form.Size = new Size(1200, 1000);
        Require(form.Size.Width <= 700 && form.Size.Height <= 500, "Maximum tracking constraint was not applied.");
        CheckOrder();

        stage = "maximize";
        form.WindowState = FormWindowState.Maximized;
        stage = "restore from maximized";
        form.WindowState = FormWindowState.Normal;
        stage = "minimize";
        form.WindowState = FormWindowState.Minimized;
        stage = "restore";
        form.WindowState = FormWindowState.Normal;
        if (observationFailure is not null) throw observationFailure;
        Require(fill.Size == form.ClientSize, "Restore left stale layout.");
        sizeEvents.Clear();
        form.MaximumSize = Size.Empty;

        if (!systemChrome) {
            stage = "dpi";
            // Exercise the existing DPI path with an injected DPI message and a real
            // suggested rectangle; this is not physical monitor/DPI-device validation.
            var suggested = new NativeRect { Left = 40, Top = 50, Right = 640, Bottom = 500 };
            nint data = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRect>());
            try {
                Marshal.StructureToPtr(suggested, data, false);
                _ = SendMessage(hwnd, 0x02E0, (nint)(144 | (144 << 16)), data);
            }
            finally { Marshal.FreeHGlobal(data); }
            if (observationFailure is not null) throw observationFailure;
            Require(form.Scaling == 1.5 && form.Size == new Size(400, 300), "DPI geometry was not committed in logical pixels.");
            Require(sizeEvents.Count(name => name == "Resize") == 1 && sizeEvents.Count(name => name == "Size") == 1,
                "WM_SIZE and ScalingChanged produced duplicate size events.");
            Console.WriteLine("WINDOW_GEOMETRY:DPI");
        }
        sizeEvents.Clear();
        stage = "reentrant resize";
        EventHandler reenter = (_, _) => { if (form.Size.Width == 520) form.Size = new Size(580, 430); };
        form.Resize += reenter;
        form.Size = new Size(520, 400);
        form.Resize -= reenter;
        if (observationFailure is not null) throw observationFailure;
        Require(form.Size == new Size(580, 430) && sizeEvents.SequenceEqual(["Resize", "Resize", "Size", "Client"]),
            $"Reentrant native resize left stale state or emitted an obsolete SizeChanged: size={form.Size}, events={string.Join(",", sizeEvents)}, chrome={systemChrome}.");
        form.Close();
    }

    private static void CancelAndClose(Form owner)
    {
        using var canceled = new Form();
        canceled.Load += (_, _) => canceled.Size = new Size(500, 400);
        canceled.ClientSizeChanged += (_, _) => canceled.Hide();
        var completion = canceled.ShowDialog(owner);
        Require(completion.IsCompletedSuccessfully && !IsWindowVisible(canceled.PlatformHandle.Handle), "ClientSizeChanged did not cancel pending modal show.");
        Require(IsWindowEnabled(owner.PlatformHandle.Handle), "Canceled geometry show retained modal owner state.");

        using var closing = new Form { StartPosition = FormStartPosition.Manual };
        closing.Show();
        nint hwnd = closing.PlatformHandle.Handle;
        closing.LocationChanged += (_, _) => closing.Close();
        closing.Location = new Point(-250, 80);
        Require(!IsWindow(hwnd) && !Application.OpenForms.Contains(closing), "Close from native move left a live window.");
        Console.WriteLine("WINDOW_GEOMETRY:REENTRANCY");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hwnd, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowEnabled(nint hwnd);
}
