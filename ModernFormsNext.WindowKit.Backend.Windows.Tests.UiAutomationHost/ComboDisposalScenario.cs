using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Threading;

// An unhandled GC-finalizer exception terminates this isolated native host. The parent test
// requires both a successful exit and evidence that the abandoned ComboBoxes were finalized.
internal static class ComboDisposalScenario
{
    internal static int Run()
    {
        using var main = new Form { Text = "ComboBox disposal regression" };
        using var timer = new ModernFormsNext.Timer { Interval = 50 };
        WeakReference? abandoned = null;
        int attempts = 0;
        Exception? failure = null;
        main.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            try { abandoned = AbandonAfterOwnerClose(); timer.Start(); }
            catch (Exception error) { failure = error; main.Close(); }
        });
        timer.Tick += (_, _) =>
        {
            try
            {
                // Let native paint/dispatcher work release its temporary tree references
                // between collections; do not make collection depend on JIT local lifetimes.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                if (abandoned!.IsAlive && ++attempts < 20) return;
                Require(!abandoned.IsAlive, "The native popup tree still retains the abandoned ComboBox.");
                Require(FinalizerProbe.Finalized > 0, "The actual Control finalizer was not exercised.");
                Require(FinalizerProbe.FinalizerThread != Environment.CurrentManagedThreadId,
                    "Finalization must run off the owning UI thread.");
                Console.WriteLine("COMBO_DISPOSAL:FINALIZED");
                ExplicitDisposal(main);
            }
            catch (Exception error) { failure = error; }
            timer.Stop();
            main.Close();
        };
        Application.Run(main);
        if (failure is not null)
        {
            Console.Error.WriteLine(failure);
            return 1;
        }
        Console.WriteLine("COMBO_DISPOSAL:PASS");
        return 0;
    }

    private static void ExplicitDisposal(Form owner)
    {
        foreach (bool hideFirst in new[] { false, true })
        {
            var combo = owner.Controls.Add(new ComboBox());
            combo.Items.Add("Alpha");
            combo.Items.Add("Beta");
            combo.DroppedDown = true;
            PopupWindow popup = Popup(combo);
            var list = (ListBox)popup.Controls[0];
            var popupRoot = list.Parent!;
            list.SelectedIndex = 1;
            Require(combo.SelectedIndex == 1 && !combo.DroppedDown, "Popup selection did not update the ComboBox.");
            combo.DroppedDown = true;
            var editor = list.Controls.Add(new TextBox());
            editor.Select();
            var client = popup.TextInputClient ?? throw new InvalidOperationException("Missing native popup text session.");
            Require(client.SetComposingText("composition"), "The native popup cannot start text composition.");
            nint hwnd = popup.PlatformHandle.Handle;
            Require(IsWindow(hwnd), "The popup HWND was not created.");
            int disposed = 0, closed = 0;
            list.Disposed += (_, _) => disposed++;
            popup.Closed += (_, _) => closed++;
            if (hideFirst) combo.DroppedDown = false;
            combo.Dispose();
            combo.Dispose();
            Require(IsDisposed(list) && disposed == 1, "The popup list must be disposed exactly once.");
            Require(IsDisposed(popupRoot), "Explicit disposal retained the owned popup adapter resources.");
            Require(closed == 1 && !IsWindow(hwnd), "Explicit disposal did not destroy the popup HWND exactly once.");
            Require(client.GetState() is null, "Explicit disposal retained a live text session.");
            owner.Controls.Remove(combo);
        }
        Console.WriteLine("COMBO_DISPOSAL:EXPLICIT");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AbandonAfterOwnerClose()
    {
        using var owner = new Form { Text = "Abandoned ComboBox owner" };
        var combo = owner.Controls.Add(new FinalizerProbe());
        combo.Items.Add("Alpha");
        owner.Show();
        combo.DroppedDown = true;
        PopupWindow popup = Popup(combo);
        var editor = popup.Controls[0].Controls.Add(new TextBox());
        editor.Select();
        var client = popup.TextInputClient ?? throw new InvalidOperationException("Missing abandoned popup text session.");
        Require(client.SetComposingText("pending"), "The abandoned popup cannot start text composition.");
        combo.DroppedDown = false;
        popup.Close();
        owner.Close();
        Require(client.GetState() is null, "Closing the native owner left the old session alive.");
        // Native close retires the window and text host, but it does not own/dispose caller
        // controls. The retained ListBox.Parent still leads to that owning-thread text host.
        Require(!IsDisposed(combo), "The scenario must abandon, rather than explicitly dispose, the ComboBox.");
        return new WeakReference(combo);
    }

    private static bool IsDisposed(Control control) => (bool)typeof(Control)
        .GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control)!;

    private static PopupWindow Popup(ComboBox combo) => (PopupWindow)typeof(ComboBox)
        .GetField("popup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(combo)!;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FinalizerProbe : ComboBox
    {
        internal static int Finalized;
        internal static int FinalizerThread;
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
            {
                Interlocked.Increment(ref Finalized);
                FinalizerThread = Environment.CurrentManagedThreadId;
            }
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);
}
