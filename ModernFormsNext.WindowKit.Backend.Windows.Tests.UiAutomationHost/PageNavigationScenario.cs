using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

// Only this process's HWND receives native messages. No global keyboard/mouse injection.
internal static class PageNavigationScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "Page navigation certification", ClientSize = new(440, 380) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    Require(IsWindow(hwnd), "Actual HWND required.");
                    using var fixture = new PageNavigationCertification(form, Console.WriteLine);
                    fixture.ExerciseImmediate();
                    Require(form.TextInputDiagnostics.HasNativeMethod, "Actual Windows text method required.");
                    var push = fixture.BeginAnimatedPush();
                    await push;
                    fixture.VerifyAnimatedPush(push);
                    fixture.PrepareBack(cancel: true);
                    Back(hwnd);
                    await fixture.BackCompletion;
                    fixture.VerifyVetoedBack();
                    fixture.PrepareBack(cancel: false);
                    Back(hwnd);
                    await fixture.BackCompletion;
                    fixture.VerifyBack();
                    fixture.PresentDetailAgain();
                    ClickBack(fixture.HeaderBackButton, hwnd);
                    await fixture.BackCompletion;
                    fixture.VerifyBack();
                    Console.WriteLine("PASS PAGE_NAVIGATION native-header-Back-hit-test");
                    int lifecycle = fixture.LifecycleEvents;
                    using (var secondary = new Form { Text = "Page lifetime focus check" })
                    {
                        secondary.Show();
                        secondary.Activate();
                        fixture.VerifyRetainedPresentation();
                        Require(lifecycle == fixture.LifecycleEvents, "Native focus change changed page lifecycle.");
                        Console.WriteLine($"PAGE_NAVIGATION deactivationObserved={!form.IsActive}");
                        secondary.Close();
                        form.Activate();
                    }
                    fixture.ExerciseRepeatedOwnership();
                    Console.WriteLine($"PAGE_NAVIGATION:NATIVE:{hwnd}");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("PAGE_NAVIGATION:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Back(nint hwnd)
    {
        SendMessage(hwnd, 0x0100, (nint)(int)Keys.BrowserBack, 0);
        SendMessage(hwnd, 0x0101, (nint)(int)Keys.BrowserBack, 0);
    }
    private static void ClickBack(Button button, nint hwnd)
    {
        var screen = button.PointToScreen(new System.Drawing.Point(button.ScaledWidth / 2, button.ScaledHeight / 2));
        var local = new NativePoint { X = screen.X, Y = screen.Y };
        Require(ScreenToClient(hwnd, ref local), "Cannot map native header Back pointer.");
        var position = (nint)((local.X & 0xffff) | (local.Y << 16));
        SendMessage(hwnd, 0x0200, 0, position);
        SendMessage(hwnd, 0x0201, 1, position);
        SendMessage(hwnd, 0x0202, 0, position);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(nint hwnd, ref NativePoint point);

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint handle, uint message, nint wParam, nint lParam);
}
