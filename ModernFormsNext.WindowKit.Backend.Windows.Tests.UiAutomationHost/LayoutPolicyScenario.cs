using System.Diagnostics;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

internal static class LayoutPolicyScenario
{
    internal static int Run(bool immediate = false)
    {
        try
        {
            using var form = new Form { Text = "Inherited layout certification", ClientSize = new(360, 280) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    if (!IsWindow(hwnd)) throw new InvalidOperationException("Actual HWND required.");
                    // Exercise the same immediate-completion path on hosts whose native motion policy allows animation.
                    if (immediate) AnimationScheduler.Default.Policy.DurationScale = 0;
                    using var fixture = new LayoutPolicyCertification(form.Controls, Console.WriteLine);
                    fixture.Begin();
                    if (!fixture.Immediate)
                    {
                        var elapsed = Stopwatch.StartNew();
                        while (!fixture.IsIntermediate() && elapsed.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(15);
                        fixture.VerifyIntermediate();
                    }
                    Paint(hwnd, fixture);
                    fixture.SettleAndVerify();
                    Paint(hwnd, fixture);
                    fixture.CollapseAndVerify();
                    Paint(hwnd, fixture);
                    fixture.RestoreAndVerify();
                    Paint(hwnd, fixture);
                    Console.WriteLine($"LAYOUT_POLICY:NATIVE:{hwnd}");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("LAYOUT_POLICY:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Paint(nint hwnd, LayoutPolicyCertification fixture)
    {
        int before = fixture.PaintCount;
        // Immediate policy can settle without changing any geometry. RedrawWindow alone may
        // legitimately reuse a clean control cache, so request fresh fixture content explicitly.
        fixture.Root.Invalidate();
        if (!RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100) || fixture.PaintCount <= before)
            throw new InvalidOperationException("Native layout fixture did not paint.");
        Console.WriteLine($"LAYOUT_POLICY native-paint {before}->{fixture.PaintCount}");
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RedrawWindow(nint hwnd, nint rectangle, nint region, uint flags);
}
