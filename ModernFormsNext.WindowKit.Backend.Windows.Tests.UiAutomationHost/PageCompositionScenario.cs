using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

// Finite native certification addresses only this process's HWND, without global input injection.
internal static class PageCompositionScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "Page composition certification", ClientSize = new(600, 420) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    Require(IsWindow(hwnd), "Actual HWND required.");
                    using var fixture = new PageCompositionCertification(form, Console.WriteLine);
                    fixture.ExerciseImmediate();
                    Require(form.TextInputDiagnostics.HasNativeMethod, "Actual Windows text method required.");
                    var opening = fixture.BeginAnimatedOpen();
                    await opening;
                    fixture.VerifyAnimatedOpen(opening);
                    fixture.PrepareBack(cancel: true);
                    int validations = fixture.ValidationCount;
                    Back(hwnd);
                    fixture.VerifyVetoedBack(validations);
                    fixture.PrepareBack(cancel: false);
                    Back(hwnd);
                    fixture.VerifyClosed();
                    fixture.Flyout.TransitionDuration = TimeSpan.FromSeconds(2);
                    opening = fixture.BeginAnimatedOpen();
                    Require(!opening.IsCompleted, "Finite native busy-open case required.");
                    Back(hwnd);
                    await opening;
                    fixture.VerifySupersededOpening(opening);
                    fixture.Flyout.TransitionDuration = TimeSpan.FromMilliseconds(180);
                    fixture.ExerciseModesAndOwnership();
                    int lifecycle = fixture.LifecycleEvents;
                    using (var secondary = new Form { Text = "Composition focus check" })
                    {
                        secondary.Show(); secondary.Activate();
                        fixture.VerifyRetainedPresentation();
                        Require(fixture.LifecycleEvents == lifecycle, "Native deactivation changed page lifecycle.");
                        Console.WriteLine($"PAGE_COMPOSITION deactivationObserved={!form.IsActive}");
                        secondary.Close(); form.Activate();
                    }
                    Console.WriteLine($"PAGE_COMPOSITION:NATIVE:{hwnd}");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("PAGE_COMPOSITION:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Back(nint hwnd)
    {
        SendMessage(hwnd, 0x0100, (nint)(int)Keys.BrowserBack, 0);
        SendMessage(hwnd, 0x0101, (nint)(int)Keys.BrowserBack, 0);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint handle, uint message, nint wParam, nint lParam);
}
