using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

internal static class ShadowTypographyScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "Shadow and typography certification", ClientSize = new(380, 440) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    if (!IsWindow(hwnd)) throw new InvalidOperationException("Actual HWND required.");
                    using var fixture = new ShadowTypographyCertification(form.Controls, Console.WriteLine);
                    fixture.Exercise();
                    Paint(hwnd, fixture);
                    fixture.PrepareAlternative(true);
                    Paint(hwnd, fixture);
                    fixture.PrepareAlternative(false);
                    Paint(hwnd, fixture);
                    Console.WriteLine($"SHADOW_TYPOGRAPHY:NATIVE:{hwnd}");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("SHADOW_TYPOGRAPHY:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint handle);

    private static void Paint(nint hwnd, ShadowTypographyCertification fixture)
    {
        int before = fixture.PaintCount;
        if (!RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100) || fixture.PaintCount <= before)
            throw new InvalidOperationException("The native HWND did not paint the appearance fixture.");
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(nint hwnd, nint updateRectangle, nint updateRegion, uint flags);
}
