using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

// Finite native HWND messages target this host only; no global keyboard or mouse injection.
internal static class AppShellScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "AppShell routing certification", ClientSize = new(600, 420) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    Require(IsWindow(hwnd), "Actual HWND required.");
                    using var fixture = new AppShellCertification(form, Console.WriteLine);
                    fixture.ExerciseImmediate();
                    Require(form.TextInputDiagnostics.HasNativeMethod, "Actual Windows text method required.");
                    fixture.PrepareNestedBack(); Back(hwnd); fixture.VerifyInnerBack();
                    fixture.OpenFlyout(); Back(hwnd); fixture.VerifyFlyoutBack();
                    Back(hwnd); fixture.VerifyOuterBack();
                    await WaitForNavigation(fixture.Shell);
                    fixture.BeginSlow(); Back(hwnd); fixture.VerifyPendingBack();
                    await fixture.CompleteSlow(); fixture.VerifyLate();
                    var delivery = fixture.DeliverLinks(); await delivery; fixture.VerifyLinks(delivery);
                    fixture.ExerciseStateAndCycles();
                    int lifecycle = fixture.LifecycleEvents;
                    using (var secondary = new Form { Text = "Shell activation check" })
                    {
                        secondary.Show(); secondary.Activate();
                        fixture.VerifyRetainedPresentation();
                        Require(lifecycle == fixture.LifecycleEvents, "Deactivation changed retained page lifecycle.");
                        Console.WriteLine($"APP_SHELL deactivationObserved={!form.IsActive}");
                        secondary.Close(); form.Activate();
                    }
                    fixture.ReturnToRoot();
                    bool veto = true; int closing = 0;
                    form.Closing += (_, args) => { closing++; args.Cancel = veto; };
                    form.Close();
                    Require(closing == 1 && form.Visible && fixture.Shell.CurrentRoute!.Route == "/home", "Form close veto was not preserved.");
                    veto = false;
                    Console.WriteLine($"APP_SHELL:NATIVE:{hwnd}");
                    form.Close();
                    Require(closing == 2 && !form.Visible, "Normal Form close failed.");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("APP_SHELL:PASS:HWND"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Back(nint hwnd)
    {
        SendMessage(hwnd, 0x0100, (nint)(int)Keys.BrowserBack, 0);
        SendMessage(hwnd, 0x0101, (nint)(int)Keys.BrowserBack, 0);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task WaitForNavigation(AppShell shell)
    {
        // Native Back commits its page before the incoming appearance finishes. The next
        // immediate test leg must await that real queue, without changing its duration/policy.
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (shell.IsNavigating && elapsed.Elapsed < TimeSpan.FromSeconds(15)) await Task.Delay(10);
        Require(!shell.IsNavigating, "Native Back appearance did not settle.");
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint handle, uint message, nint wParam, nint lParam);
}
