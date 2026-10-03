using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Threading;

// Synthetic WM_DPICHANGED on an owned real HWND exercises the production native message path.
// This is not evidence of physically moving a window between monitors with different DPI.
internal static class DpiChangedScenario
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
                        RetireWindow(systemChrome, false);
                        RetireWindow(systemChrome, true);
                    }
                }
                catch (Exception error) { failure = error; }
                finally { owner.Close(); }
            });
            Application.Run(owner);
            if (failure is not null) throw failure;
            Console.WriteLine("DPI_CHANGED:PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Transitions(bool systemChrome)
    {
        using var form = new Form { UseSystemDecorations = systemChrome, StartPosition = FormStartPosition.Manual,
            Size = new Size(400, 300) };
        using var parent = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        using var child = parent.Controls.Add(new TextBox { Dock = DockStyle.Fill, Text = "DPI cache" });
        form.Show();
        nint hwnd = form.PlatformHandle.Handle;
        Require(GetWindowThreadProcessId(hwnd, out uint process) == GetCurrentThreadId() && process == Environment.ProcessId,
            "DPI scenario must use its own UI-thread HWND.");
        Require(form.Scaling == GetDpiForWindow(hwnd) / 96d, "Initial backend scale differs from native DPI.");
        Exception? observerFailure = null;
        List<string> order = [];
        double oldScale = form.Scaling, expectedScale = oldScale;
        int controls = 0, windows = 0;
        parent.DpiChanged += (_, _) => order.Add("parent");
        child.DpiChanged += (_, e) => {
            controls++; order.Add("child");
            Capture(() => {
                Require(e.OldScale == oldScale && e.NewScale == expectedScale, "Child old/new scale mismatch.");
                Require(child.Scaling == e.NewScale && child.DeviceDpi == e.NewDpi, "Child getter precedes DPI commit.");
                Require(child.Size == parent.ClientSize, "DPI child layout is stale.");
            });
        };
        form.Resize += (_, _) => order.Add("Resize");
        form.LocationChanged += (_, _) => order.Add("Location");
        form.DpiChanged += (_, e) => {
            windows++; order.Add("window");
            Capture(() => {
                Require(form.Scaling == e.NewScale && e.NewScale == expectedScale && e.OldScale == oldScale, "Window old/new/getter mismatch.");
                Require(e.OldDpi == (int)(oldScale * 96) && e.NewDpi == (int)(expectedScale * 96), "Integer DPI conversion mismatch.");
                Require(parent.Size == form.ClientSize, "Window DPI event preceded final client layout.");
            });
        };
        foreach (int dpi in new[] { 96, 120, 144, 192, 216 }) {
            oldScale = form.Scaling; expectedScale = dpi / 96d;
            order.Clear(); controls = windows = 0;
            ApplyDpi(hwnd, dpi);
            if (observerFailure is not null) throw observerFailure;
            int expected = oldScale == expectedScale ? 0 : 1;
            Require(controls == expected && windows == expected, "Missing or duplicated public DPI event.");
            if (expected == 1)
                Require(order.IndexOf("parent") < order.IndexOf("child") && order.IndexOf("child") < order.IndexOf("window"), "Parent/child/window order changed.");
            ApplyDpi(hwnd, dpi, move: true);
            Require(controls == expected && windows == expected, "Same DPI at a different location emitted an event.");
        }
        form.Close();
        Console.WriteLine("DPI_CHANGED:NATIVE:" + systemChrome);

        // Keep assertions outside reverse P/Invoke; exception propagation is covered headlessly.
        void Capture(Action assertion) { try { assertion(); } catch (Exception error) { observerFailure ??= error; } }
    }

    private static void Reentrancy(bool systemChrome)
    {
        using var form = new Form { UseSystemDecorations = systemChrome, StartPosition = FormStartPosition.Manual };
        using var child = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        form.Show();
        nint hwnd = form.PlatformHandle.Handle;
        ApplyDpi(hwnd, 96);
        List<double> seen = [];
        child.DpiChanged += (_, e) => { if (e.NewDpi == 144) ApplyDpi(hwnd, 192); };
        form.DpiChanged += (_, e) => { seen.Add(e.NewScale); form.Size = new Size(470, 340); };
        ApplyDpi(hwnd, 144);
        Require(seen.SequenceEqual([2d]) && form.Scaling == 2, "Older native DPI transition survived a reentrant change.");
        Require(form.Size == new Size(470, 340) && child.Size == form.ClientSize, "Older suggested rectangle overwrote observer geometry.");
        form.Close();
        Console.WriteLine("DPI_CHANGED:REENTRANCY:" + systemChrome);
    }

    private static void RetireWindow(bool systemChrome, bool close)
    {
        using var form = new Form { UseSystemDecorations = systemChrome, StartPosition = FormStartPosition.Manual };
        form.Show();
        nint hwnd = form.PlatformHandle.Handle;
        ApplyDpi(hwnd, 96);
        int later = 0;
        form.DpiChanged += (_, _) => { if (close) form.Close(); else form.Hide(); };
        form.DpiChanged += (_, _) => later++;
        ApplyDpi(hwnd, 144);
        Require(later == 0 && !form.Visible && !IsWindowVisible(hwnd), "Retired DPI transition continued visibility/observers.");
        if (close) Require(!IsWindow(hwnd), "DPI callback resurrected closed HWND.");
        Console.WriteLine("DPI_CHANGED:RETIRE:" + close);
    }

    private static void ApplyDpi(nint hwnd, int dpi, bool move = false)
    {
        Require(GetWindowRect(hwnd, out var suggested), "Cannot read HWND rectangle.");
        if (move) { suggested.Left++; suggested.Right++; }
        nint memory = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRect>());
        try {
            Marshal.StructureToPtr(suggested, memory, false);
            _ = SendMessage(hwnd, 0x02E0, (dpi << 16) | dpi, memory);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint hwnd, out NativeRect rectangle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
}
