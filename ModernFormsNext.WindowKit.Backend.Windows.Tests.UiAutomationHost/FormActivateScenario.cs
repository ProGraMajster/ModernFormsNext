using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Threading;

// Requests use the public method and real owned HWNDs, never synthetic WM_ACTIVATE or focus hacks.
internal static class FormActivateScenario
{
    internal static int Run()
    {
        try {
            using var main = new Form { StartPosition = FormStartPosition.Manual };
            Exception? failure = null;
            main.Shown += (_, _) => Dispatcher.UIThread.Post(() => {
                try {
                    foreach (bool chrome in new[] { false, true }) {
                        Transitions(chrome);
                        Modal(chrome);
                        foreach (string operation in new[] { "activate", "hide", "close" })
                            Reentrancy(chrome, operation);
                    }
                }
                catch (Exception error) { failure = error; }
                finally { main.Close(); }
            });
            Application.Run(main);
            if (failure is not null) throw failure;
            Console.WriteLine("FORM_ACTIVATE:PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Transitions(bool chrome)
    {
        using var first = NewForm(chrome);
        using var second = NewForm(chrome);
        try {
            first.Show(); second.Show();
            nint one = first.PlatformHandle.Handle, two = second.PlatformHandle.Handle;
            Require(GetWindowThreadProcessId(one, out uint process) == GetCurrentThreadId() && process == Environment.ProcessId,
                "Activation must target this process's own UI-thread HWND.");
            int activated = 0, deactivated = 0;
            bool committed = true;
            first.Activated += (_, _) => { activated++; committed &= first.IsActive; };
            second.Deactivated += (_, _) => { deactivated++; committed &= !second.IsActive; };
            bool wasActive = first.IsActive, otherWasActive = second.IsActive;
            first.Activate();
            Require(first.IsActive == (GetActiveWindow() == one), "First activity differs from native confirmation.");
            Require(second.IsActive == (GetActiveWindow() == two), "Second activity differs from native confirmation.");
            Require(committed, "Observer preceded its activity state commit.");
            bool observed = !wasActive && first.IsActive;
            if (observed) {
                Require(activated == 1, "Activation transition missing or duplicated.");
                if (otherWasActive) Require(!second.IsActive && deactivated == 1, "Previous window did not deactivate.");
            }
            else Require(activated == 0, "Refused/no-op activation fabricated a transition.");
            int before = activated;
            first.Activate();
            if (first.IsActive && (wasActive || observed)) Require(activated == before, "Already-active request notified twice.");
            Console.WriteLine($"FORM_ACTIVATE:NATIVE:{chrome}:" + (observed ? "OBSERVED" : "NOT_OBSERVED (platform foreground policy)"));

            first.Hide();
            Expect<InvalidOperationException>(first.Activate);
            Require(!first.Visible && !first.IsActive && !IsWindowVisible(one), "Hidden request showed or activated HWND.");
            first.Close();
            Expect<ObjectDisposedException>(first.Activate);
            Require(!IsWindow(one) && !first.IsActive && !Application.OpenForms.Contains(first), "Closed request resurrected HWND.");
            Console.WriteLine($"FORM_ACTIVATE:HIDDEN_CLOSED:{chrome}:PASS");
        }
        finally { first.Close(); second.Close(); }
    }

    private static void Modal(bool chrome)
    {
        using var owner = NewForm(chrome);
        using var dialog = NewForm(chrome);
        try {
            owner.Show();
            var completion = dialog.ShowDialog(owner);
            nint ownerHandle = owner.PlatformHandle.Handle;
            owner.Activate();
            dialog.Activate();
            Require(!IsWindowEnabled(ownerHandle), "Activation enabled modal owner input.");
            Require(!owner.IsActive, "Modal-disabled owner received false activation.");
            Require(!completion.IsCompleted && dialog.DialogResult == DialogResult.None, "Activation completed modal operation.");
            dialog.Close();
            Require(completion.IsCompletedSuccessfully && IsWindowEnabled(ownerHandle), "Normal modal cleanup failed.");
            Console.WriteLine($"FORM_ACTIVATE:MODAL:{chrome}:PASS");
        }
        finally { dialog.Close(); owner.Close(); }
    }

    private static void Reentrancy(bool chrome, string operation)
    {
        using var target = NewForm(chrome);
        using var other = NewForm(chrome);
        try {
            target.Show(); other.Show();
            if (target.IsActive) {
                Console.WriteLine($"FORM_ACTIVATE:REENTRANCY:{chrome}:{operation}:NOT_OBSERVED (platform foreground policy)");
                return;
            }
            nint hwnd = target.PlatformHandle.Handle;
            int events = 0, later = 0;
            target.Activated += (_, _) => {
                events++;
                if (operation == "activate") target.Activate();
                else if (operation == "hide") target.Hide();
                else target.Close();
            };
            target.Activated += (_, _) => later++;
            target.Activate();
            if (events == 0) {
                Require(!target.IsActive && GetActiveWindow() != hwnd, "Native activation was lost by the framework.");
                Console.WriteLine($"FORM_ACTIVATE:REENTRANCY:{chrome}:{operation}:NOT_OBSERVED (platform foreground policy)");
                return;
            }
            Require(events == 1, "Reentrant request recursively activated.");
            Require(later == (operation == "activate" ? 1 : 0), "Obsolete activation observers continued.");
            if (operation != "activate")
                Require(!target.Visible && !target.IsActive && !IsWindowVisible(hwnd), "Activation resurrected hidden/closed window.");
            if (operation == "close") Require(!IsWindow(hwnd), "Closed HWND survived activation handler.");
            Console.WriteLine($"FORM_ACTIVATE:REENTRANCY:{chrome}:{operation}:OBSERVED");
        }
        finally { target.Close(); other.Close(); }
    }

    private static Form NewForm(bool chrome) => new() { UseSystemDecorations = chrome, StartPosition = FormStartPosition.Manual };
    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll")] private static extern nint GetActiveWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowEnabled(nint hwnd);
}
