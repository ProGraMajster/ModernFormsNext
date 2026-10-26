using System.Diagnostics;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

internal static class VisualStateMetricScenario
{
    internal static int Run(bool immediate = false)
    {
        try
        {
            using var form = new Form { Text = "Visual state metric certification", ClientSize = new(360, 300) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    if (!IsWindow(hwnd)) throw new InvalidOperationException("Actual HWND required.");
                    if (immediate) AnimationScheduler.Default.Policy.DurationScale = 0;
                    using var fixture = new VisualStateMetricCertification(form.Controls, Console.WriteLine);
                    fixture.VerifySource();
                    Paint(hwnd, fixture);
                    fixture.Begin();
                    if (!fixture.Immediate)
                    {
                        await Wait(fixture.IsIntermediate, "Native intermediate metrics unavailable.");
                        fixture.VerifyIntermediate();
                        Paint(hwnd, fixture);
                    }
                    await Wait(() => fixture.IsSettled, "Native metric timeline did not settle.");
                    fixture.VerifySettled();
                    Paint(hwnd, fixture);
                    fixture.CancelAndRestore();
                    Paint(hwnd, fixture);
                    Console.WriteLine($"VISUAL_STATE_METRICS:NATIVE:{hwnd}");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("VISUAL_STATE_METRICS:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static async Task Wait(Func<bool> ready, string message)
    {
        var elapsed = Stopwatch.StartNew();
        while (!ready() && elapsed.Elapsed < TimeSpan.FromSeconds(15)) await Task.Delay(15);
        if (!ready()) throw new InvalidOperationException(message);
    }

    private static void Paint(nint hwnd, VisualStateMetricCertification fixture)
    {
        int before = fixture.PaintCount;
        // A settled style can reuse its cache. Explicitly request fresh content for this proof.
        fixture.Root.Invalidate();
        if (!RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100) || fixture.PaintCount <= before)
            throw new InvalidOperationException("Native visual-state fixture did not paint.");
        Console.WriteLine($"VISUAL_STATE_METRICS native-paint {before}->{fixture.PaintCount}");
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RedrawWindow(nint hwnd, nint rect, nint region, uint flags);
}
