using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Threading;

// A separate process owns the real UI thread. Observe the HWND's first show messages;
// checking only Form.Visible would not detect premature native display during Load.
internal static class FormLoadScenario
{
    internal static int Run()
    {
        try
        {
            using var main = new Form { Text = "Form.Load native regression", ClientSize = new Size(320, 200) };
            List<string> order = [];
            using var observation = new ShowObservation(main, () => order.Add("NativeShow"));
            int loads = 0;
            Exception? failure = null;
            main.Load += (_, _) => {
                loads++;
                Require(!main.Visible && !IsWindowVisible(main.PlatformHandle.Handle), "Load ran after native visibility.");
                Require(!observation.Observed, "The HWND received a show request before Load.");
                main.Show();
                Require(!observation.Observed, "Reentrant Show reached the HWND during Load.");
                main.Controls.Add(new Label { Text = "Loaded", Dock = DockStyle.Fill });
                main.Size = new Size(400, 260);
                order.Add("Load");
            };
            main.Shown += (_, _) => {
                try
                {
                    order.Add("Shown");
                    Require(order.SequenceEqual(["Load", "NativeShow", "Shown"]), "Unexpected first-show order: " + string.Join(",", order));
                    Require(IsWindowVisible(main.PlatformHandle.Handle), "Shown has no visible HWND.");
                    Require(main.Size == new Size(400, 260), "Load size was not applied before showing.");
                    Console.WriteLine("FORM_LOAD:Load>NativeShow>Shown");
                }
                catch (Exception error) { failure = error; }
                Dispatcher.UIThread.Post(() => {
                    try
                    {
                        if (failure is not null) return;
                        main.Hide();
                        main.Show();
                        Require(loads == 1, "Hide/Show repeated Load.");
                        Dialog(main);
                        CanceledDisplay(main);
                        FailedLoad(main);
                    }
                    catch (Exception error) { failure = error; }
                    finally { main.Close(); }
                });
            };
            Application.Run(main);
            if (failure is not null) throw failure;
            Console.WriteLine("FORM_LOAD:PASS");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Dialog(Form owner)
    {
        using var dialog = new Form { StartPosition = FormStartPosition.CenterParent };
        List<string> order = [];
        using var observation = new ShowObservation(dialog, () => order.Add("NativeShow"));
        dialog.Load += (_, _) => {
            Require(!observation.Observed && !IsWindowVisible(dialog.PlatformHandle.Handle), "Modal Load ran after native show.");
            Require(IsWindowEnabled(owner.PlatformHandle.Handle), "The owner was disabled before Load completed.");
            dialog.Size = new Size(220, 140);
            order.Add("Load");
        };
        dialog.Shown += (_, _) => {
            order.Add("Shown");
            Require(!IsWindowEnabled(owner.PlatformHandle.Handle), "Modal show did not disable its owner.");
            dialog.DialogResult = DialogResult.OK;
        };
        var result = dialog.ShowDialog(owner);
        Require(result.IsCompletedSuccessfully, "Shown did not complete the modal task.");
        Require(order.SequenceEqual(["Load", "NativeShow", "Shown"]), "Modal lifecycle diverged from modeless Load.");
        Require(IsWindowEnabled(owner.PlatformHandle.Handle), "The dialog left its owner disabled.");
        Console.WriteLine("FORM_LOAD:MODAL");
    }

    private static void CanceledDisplay(Form owner)
    {
        using var dialog = new Form();
        int loads = 0;
        using var observation = new ShowObservation(dialog, () => { });
        dialog.Load += (_, _) => { loads++; dialog.Hide(); };
        var result = dialog.ShowDialog(owner);
        Require(result.IsCompletedSuccessfully && !observation.Observed, "Hide in Load did not cancel native modal display.");
        Require(IsWindowEnabled(owner.PlatformHandle.Handle), "Hide in Load blocked the owner.");
        dialog.Show();
        Require(loads == 1 && observation.Observed, "A hidden initialized instance cannot be shown without reloading.");
        dialog.Close();
        Console.WriteLine("FORM_LOAD:HIDE");
    }

    private static void FailedLoad(Form owner)
    {
        using var dialog = new Form();
        using var observation = new ShowObservation(dialog, () => { });
        var expected = new InvalidOperationException("native Load failure");
        dialog.Load += (_, _) => throw expected;
        try { _ = dialog.ShowDialog(owner); throw new InvalidOperationException("Load failure was swallowed."); }
        catch (InvalidOperationException error) when (ReferenceEquals(error, expected)) { }
        Require(!observation.Observed && !dialog.Visible, "Failed Load displayed the native dialog.");
        Require(IsWindowEnabled(owner.PlatformHandle.Handle), "Failed Load left the owner disabled.");
        Require(!Application.OpenForms.Contains(dialog), "Failed Load registered an open form.");
        dialog.Close();
        Console.WriteLine("FORM_LOAD:FAILURE_ROLLBACK");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ShowObservation : IDisposable
    {
        private readonly nint handle;
        private readonly SubclassProc callback;
        internal bool Observed { get; private set; }

        internal ShowObservation(Form form, Action onShow)
        {
            handle = form.PlatformHandle.Handle;
            callback = (hwnd, message, wParam, lParam, id, data) => {
                // WM_SHOWWINDOW is sent before visibility changes. WM_WINDOWPOSCHANGING
                // also catches a show performed through SetWindowPos with SWP_SHOWWINDOW.
                bool showing = message == 0x0018 && wParam != 0;
                if (message == 0x0046 && lParam != 0)
                    showing |= (Marshal.PtrToStructure<WindowPosition>(lParam).Flags & 0x0040) != 0;
                if (showing && !Observed) { Observed = true; onShow(); }
                return DefSubclassProc(hwnd, message, wParam, lParam);
            };
            Require(SetWindowSubclass(handle, callback, 1, 0), "Cannot observe the native Form window.");
        }

        public void Dispose()
        {
            if (IsWindow(handle)) RemoveWindowSubclass(handle, callback, 1);
            GC.KeepAlive(callback);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPosition
    {
        public nint Hwnd, InsertAfter;
        public int X, Y, Width, Height;
        public uint Flags;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);
}
