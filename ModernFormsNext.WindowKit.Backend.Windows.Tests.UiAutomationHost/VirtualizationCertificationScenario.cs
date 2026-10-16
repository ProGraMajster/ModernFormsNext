using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

/// <summary>Runs the shared paced certification workload inside an actual Windows HWND.</summary>
internal static class VirtualizationCertificationScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "Virtualization certification", ClientSize = new Size(420, 460) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    if (hwnd == 0 || !IsWindow(hwnd)) throw new InvalidOperationException("Actual HWND required.");
                    using var fixture = new VirtualizationCertification(form, Console.WriteLine);
                    fixture.Exercise(key => SendMessage(hwnd, 0x0100, (nint)(int)key, 0));
                    foreach (int dpi in new[] { 96, 144, 192, 96 })
                    {
                        ApplyDpi(hwnd, dpi);
                        fixture.View.ScrollToItem(70_000);
                        var row = fixture.View.GetItemContainer(70_000)!;
                        var item = fixture.View.Items[70_000];
                        if (item.Bounds.X != fixture.View.LogicalToDeviceUnits(row.Left) ||
                            item.Bounds.Y != fixture.View.LogicalToDeviceUnits(row.Top))
                            throw new InvalidOperationException("DPI converted item position more than once.");
                        Console.WriteLine($"PASS native-dpi-{dpi} scale={fixture.View.ScaleFactor}");
                    }
                    for (int warm = 0; warm < 10; warm++)
                    {
                        fixture.WarmBatch();
                        await Task.Delay(50);
                    }
                    fixture.CompleteWarmup();
                    for (int batch = 0; batch < VirtualizationCertification.BatchCount; batch++)
                    {
                        fixture.Batch(batch);
                        await Task.Delay(1000); // Returns to the real native input/render loop.
                    }
                    fixture.Complete();
                    if (fixture.PaintCount < 10) throw new InvalidOperationException($"Native paint loop did not advance: {fixture.PaintCount}.");
                    Console.WriteLine($"VIRTUALIZATION_CERTIFICATION:NATIVE:{hwnd}:paints={fixture.PaintCount}");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("VIRTUALIZATION_CERTIFICATION:PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void ApplyDpi(nint hwnd, int dpi)
    {
        if (!GetWindowRect(hwnd, out var rectangle)) throw new InvalidOperationException("Cannot read native bounds.");
        nint memory = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRect>());
        try
        {
            Marshal.StructureToPtr(rectangle, memory, false);
            SendMessage(hwnd, 0x02E0, (dpi << 16) | dpi, memory);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
