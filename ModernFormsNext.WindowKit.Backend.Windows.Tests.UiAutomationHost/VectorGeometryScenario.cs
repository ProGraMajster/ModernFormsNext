using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Drawing;
using ModernFormsNext.Validation;
using ModernFormsNext.WindowKit.Threading;

internal static class VectorGeometryScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "Vector geometry certification", ClientSize = new(380, 480) };
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    Require(IsWindow(hwnd), "An actual HWND is required.");
                    Require(SetWindowPos(hwnd, new nint(-1), 40, 40, 0, 0, 0x0011), "Cannot expose the owned window.");
                    using (var fixture = new VectorGeometryCertification(form.Controls, Console.WriteLine))
                    {
                        fixture.Exercise();
                        Paint(hwnd, fixture);
                        Pixels(fixture, false);
                        var area = fixture.ClipPixelBounds;
                        Pointer(hwnd, new(area.Left + area.Width / 4, area.Top + area.Height / 2), 0x0201, 1);
                        Pointer(hwnd, new(area.Left + area.Width / 4, area.Top + area.Height / 2), 0x0202);
                        Require(fixture.Clicks == 1, "The included child did not receive native pointer input.");
                        Pointer(hwnd, new(area.Left + 3 * area.Width / 4, area.Top + area.Height / 2), 0x0201, 1);
                        Pointer(hwnd, new(area.Left + 3 * area.Width / 4, area.Top + area.Height / 2), 0x0202);
                        Require(fixture.Clicks == 1, "The excluded child received native pointer input.");
                        fixture.PrepareAlternative(true);
                        Paint(hwnd, fixture);
                        Pixels(fixture, true);
                        fixture.PrepareAlternative(false);
                        Paint(hwnd, fixture);
                        Pixels(fixture, false);
                    }
                    var source = PathGeometry.Parse("M10 10 C30 10 70 10 90 10");
                    using var dialog = new DesignerPathGeometryDialog(source);
                    Exception? dialogFailure = null;
                    dialog.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            var canvas = dialog.Controls.OfType<DesignerPathCanvas>().Single();
                            nint handle = dialog.PlatformHandle.Handle;
                            Require(IsWindow(handle), "The path dialog requires its own actual HWND.");
                            Require(RedrawWindow(handle, 0, 0, 0x0001 | 0x0100), "The path dialog did not paint.");
                            float scale = (canvas.Width - 40) / 80f;
                            var controlPoint = new Point((int)MathF.Round(canvas.Width / 2f - 20 * scale), canvas.Height / 2);
                            Point pixel = canvas.PointToScreen(new Point(canvas.LogicalToDeviceUnits(controlPoint.X), canvas.LogicalToDeviceUnits(controlPoint.Y)));
                            Pointer(handle, pixel, 0x0201, 1);
                            Pointer(handle, new(pixel.X, pixel.Y - 20), 0x0200, 1);
                            Require(canvas.Selected && dialog.Geometry.ToPathData() != source.ToPathData(), "Native Bezier drag did not edit the selected draft.");
                            SendMessage(handle, 0x0100, 0x1B, 1);
                            SendMessage(handle, 0x0101, 0x1B, unchecked((nint)0xC0000001));
                            Require(dialog.Geometry.ToPathData() == source.ToPathData() && !canvas.Capture, "Native Escape did not cancel the draft drag.");
                            Console.WriteLine("VECTOR_GEOMETRY:DESIGNER_NATIVE_DRAG_ESCAPE:PASS");
                        }
                        catch (Exception error) { dialogFailure = error; }
                        finally { dialog.DialogResult = DialogResult.Cancel; }
                    });
                    Require(await dialog.ShowDialog(form) == DialogResult.Cancel, "The draft dialog did not cancel.");
                    if (dialogFailure is not null) throw dialogFailure;
                    Require(source.ToPathData() == "M 10 10 C 30 10 70 10 90 10", "The dialog mutated its source.");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("VECTOR_GEOMETRY:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Paint(nint hwnd, VectorGeometryCertification fixture)
    {
        int before = fixture.PaintCount;
        fixture.Root.Invalidate();
        Require(RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100) && fixture.PaintCount > before, "The native window did not paint the vector fixture.");
        Require(DwmFlush() >= 0, "The desktop compositor did not commit the frame.");
    }
    private static void Pixels(VectorGeometryCertification fixture, bool alternative)
    {
        nint dc = GetDC(0);
        Require(dc != 0, "The actual desktop DC is unavailable.");
        try
        {
            var bounds = fixture.ClipPixelBounds;
            var area = fixture.AreaCenterPixel;
            uint left = GetPixel(dc, bounds.Left + bounds.Width / 4, bounds.Top + bounds.Height / 2);
            uint right = GetPixel(dc, bounds.Left + 3 * bounds.Width / 4, bounds.Top + bounds.Height / 2);
            uint middle = GetPixel(dc, area.X, area.Y);
            const uint gold = 0x0000D7FF, white = 0x00FFFFFF;
            Console.WriteLine($"VECTOR_GEOMETRY:NATIVE_PIXELS alternative={alternative} left={left:X8} right={right:X8} middle={middle:X8}");
            Require(left == (alternative ? white : gold) && right == (alternative ? gold : white) && middle == (alternative ? gold : white), "The actual composed vector pixels differ from the model.");
        }
        finally { ReleaseDC(0, dc); }
    }
    private static void Pointer(nint hwnd, Point point, uint message, nint buttons = 0)
    {
        Require(ScreenToClient(hwnd, ref point), "Cannot map native pointer coordinates.");
        SendMessage(hwnd, message, buttons, (nint)((point.Y << 16) | (point.X & 0xFFFF)));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RedrawWindow(nint hwnd, nint rect, nint region, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ScreenToClient(nint hwnd, ref Point point);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc, int x, int y);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
