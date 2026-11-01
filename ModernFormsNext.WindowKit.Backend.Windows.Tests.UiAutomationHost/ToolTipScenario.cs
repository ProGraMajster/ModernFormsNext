using System.Diagnostics;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

internal static class ToolTipScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "Passive tooltip certification", ClientSize = new(380, 440) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    nint owner = form.PlatformHandle.Handle;
                    if (!SetWindowPos(owner, new nint(-1), 0, 0, 0, 0, 0x0013))
                        throw new InvalidOperationException("Cannot expose the owned tooltip fixture.");
                    using var fixture = new ToolTipCertification(form, Console.WriteLine);
                    // This HWND belongs to the test thread/form and makes a native-child focus
                    // transfer observable independently of the framework's logical focus state.
                    nint child = CreateWindowEx(0, "EDIT", "", 0x50000000, 0, 0, 1, 1, owner, 0, 0, 0);
                    if (child == 0) throw new InvalidOperationException("Cannot create owned native focus probe.");
                    try
                    {
                        fixture.Editor.Select();
                        SetFocus(child);
                        if (GetFocus() != child) throw new InvalidOperationException("Native child did not acquire test-thread focus.");
                        fixture.Begin();
                        if (GetFocus() != child) throw new InvalidOperationException("Tooltip stole the native child's focus.");
                        nint popup = fixture.Popup.PlatformHandle.Handle;
                        byte initial = Alpha(popup);
                        bool immediate = fixture.Settled;
                        Console.WriteLine($"TOOLTIP native-immediate={immediate} alpha={initial}");
                        if (initial != (immediate ? 255 : 0)) throw new InvalidOperationException("Native entrance alpha mismatch.");
                        Paint(owner); Paint(popup);
                        VerifyPixel(fixture, opaque: immediate);
                        if (!immediate)
                        {
                            byte middle = 0;
                            await Wait(() =>
                            {
                                byte sample = Alpha(popup);
                                if (sample is > 0 and < 255) { middle = sample; return true; }
                                return fixture.Settled;
                            });
                            if (middle is 0 or 255) throw new InvalidOperationException("No intermediate native opacity was observed.");
                            Console.WriteLine($"TOOLTIP native-middle-alpha={middle}");
                        }
                        await Wait(() => fixture.Settled);
                        fixture.VerifySettled();
                        if (Alpha(popup) != 255 || GetFocus() != child) throw new InvalidOperationException("Settled tooltip changed alpha/focus.");
                        Paint(popup);
                        VerifyPixel(fixture, opaque: true);
                        fixture.BeginExit();
                        await Wait(() => !fixture.Popup.Visible);
                        if (IsWindowVisible(popup)) throw new InvalidOperationException("Native exit left a visible popup.");
                        fixture.ExercisePassiveMenu();
                        Console.WriteLine("TOOLTIP:NATIVE_ALPHA_PIXELS_FOCUS_MENU:PASS");
                    }
                    finally { DestroyWindow(child); }
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("TOOLTIP:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static byte Alpha(nint popup)
    {
        if (!GetLayeredWindowAttributes(popup, out _, out byte alpha, out uint flags) || (flags & 2) == 0)
            throw new InvalidOperationException("Native uniform popup opacity is unavailable.");
        return alpha;
    }

    private static async Task Wait(Func<bool> ready)
    {
        var elapsed = Stopwatch.StartNew();
        while (!ready() && elapsed.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(10);
        if (!ready()) throw new InvalidOperationException("Tooltip presentation did not reach its bounded native state.");
    }

    private static void Paint(nint hwnd)
    {
        if (!RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100) || DwmFlush() < 0)
            throw new InvalidOperationException("Owned tooltip frame did not commit.");
    }

    private static void VerifyPixel(ToolTipCertification fixture, bool opaque)
    {
        // Sample only the owned popup's interior over its known RoyalBlue parent surface.
        // The negative transparent state must show the parent rather than an opaque rectangle.
        var bounds = fixture.PixelBounds;
        nint dc = GetDC(0);
        if (dc == 0) throw new InvalidOperationException("Desktop capture DC unavailable.");
        try
        {
            uint pixel = GetPixel(dc, bounds.Left + 2, bounds.Top + 2);
            var expected = opaque ? SkiaSharp.SKColors.LemonChiffon : SkiaSharp.SKColors.RoyalBlue;
            int red = (int)(pixel & 255), green = (int)((pixel >> 8) & 255), blue = (int)((pixel >> 16) & 255);
            Console.WriteLine($"TOOLTIP native-pixel opaque={opaque} rgb={red},{green},{blue}");
            if (pixel == uint.MaxValue || Math.Abs(red - expected.Red) > 2 || Math.Abs(green - expected.Green) > 2 || Math.Abs(blue - expected.Blue) > 2)
                throw new InvalidOperationException("Native tooltip composition pixel mismatch.");
        }
        finally { ReleaseDC(0, dc); }
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex, string type, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint SetFocus(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetFocus();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetLayeredWindowAttributes(nint hwnd, out uint key, out byte alpha, out uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RedrawWindow(nint hwnd, nint rect, nint region, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc, int x, int y);
}
