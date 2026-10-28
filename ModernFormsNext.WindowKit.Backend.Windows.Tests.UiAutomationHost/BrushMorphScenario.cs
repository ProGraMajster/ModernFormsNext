using System.Diagnostics;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

internal static class BrushMorphScenario
{
    internal static int Run(bool immediate = false)
    {
        try
        {
            using var form = new Form { Text = "Brush morph certification", ClientSize = new(360, 200) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    if (!IsWindow(hwnd)) throw new InvalidOperationException("Actual HWND required.");
                    // Desktop pixels must belong to this fixture even when Windows denies foreground activation.
                    // Only the owned test HWND is promoted; closing the form retires that state.
                    if (!SetWindowPos(hwnd, new nint(-1), 0, 0, 0, 0, 0x0013))
                        throw new InvalidOperationException("Cannot expose native brush fixture.");
                    if (immediate) AnimationScheduler.Default.Policy.DurationScale = 0;
                    using var fixture = new BrushMorphCertification(form.Controls, Console.WriteLine);
                    fixture.VerifySource();
                    Paint(hwnd, fixture);
                    VerifyPixels(fixture, "source");
                    fixture.Begin();
                    if (!fixture.Immediate)
                    {
                        await Wait(() => fixture.IsIntermediate);
                        fixture.VerifyIntermediate();
                        Paint(hwnd, fixture);
                        VerifyPixels(fixture, "middle");
                    }
                    await Wait(() => fixture.IsSettled);
                    fixture.VerifySettled();
                    Paint(hwnd, fixture);
                    VerifyPixels(fixture, "target");
                    fixture.Restore();
                    Paint(hwnd, fixture);
                    VerifyPixels(fixture, "restored");
                    Console.WriteLine($"BRUSH_MORPH:NATIVE:{hwnd}");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("BRUSH_MORPH:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static async Task Wait(Func<bool> ready)
    {
        var elapsed = Stopwatch.StartNew();
        while (!ready() && elapsed.Elapsed < TimeSpan.FromSeconds(15)) await Task.Delay(15);
        if (!ready()) throw new InvalidOperationException("Native brush timeline did not reach its expected state.");
    }

    private static void Paint(nint hwnd, BrushMorphCertification fixture)
    {
        int before = fixture.PaintCount;
        fixture.Root.Invalidate();
        if (!RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100) || fixture.PaintCount <= before)
            throw new InvalidOperationException("Native brush fixture did not paint.");
        // Wait for the desktop compositor before sampling the independently visible frame.
        if (DwmFlush() < 0) throw new InvalidOperationException("Desktop frame did not commit.");
        Console.WriteLine($"BRUSH_MORPH native-paint {before}->{fixture.PaintCount}");
    }

    private static void VerifyPixels(BrushMorphCertification fixture, string stage)
    {
        // Read actual desktop pixels after the native paint, independently of framework brushes.
        // This native scenario therefore requires an unobscured, reserved desktop.
        nint dc = GetDC(0);
        if (dc == 0) throw new InvalidOperationException("Desktop DC unavailable.");
        try
        {
            var a = fixture.EncodedCenter; var b = fixture.LinearCenter; var c = fixture.GlassCenter;
            uint encoded = GetPixel(dc, a.X, a.Y), linear = GetPixel(dc, b.X, b.Y), glass = GetPixel(dc, c.X, c.Y);
            if (encoded == uint.MaxValue || linear == uint.MaxValue || glass == uint.MaxValue)
                throw new InvalidOperationException("Native brush pixel unavailable.");
            int x = (int)(encoded & 255), y = (int)(linear & 255), z = (int)(glass & 255);
            bool valid = stage switch
            {
                "source" or "restored" => x < 8 && y < 8 && z is > 100 and < 240,
                "middle" => x is > 40 and < 230 && y > x + 25 && z is > 211 and < 255,
                "target" => x > 247 && y > 247 && z > 247,
                _ => false
            };
            Console.WriteLine($"BRUSH_MORPH native-pixels {stage} encoded={x} linear={y} glass={z}");
            if (!valid) throw new InvalidOperationException("Native brush pixel mismatch at " + stage);
        }
        finally { ReleaseDC(0, dc); }
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc, int x, int y);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RedrawWindow(nint hwnd, nint rect, nint region, uint flags);
}
