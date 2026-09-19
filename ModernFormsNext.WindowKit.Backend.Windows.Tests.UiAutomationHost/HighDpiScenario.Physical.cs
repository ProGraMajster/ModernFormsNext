using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModernFormsNext;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit.Threading;

internal static partial class HighDpiScenario
{
    // Explicit local acceptance mode, separate from portable CI. Never changes display settings
    // or injects DPI messages: Windows generates transitions when this owned HWND changes monitor.
    internal static int RunPhysical(string evidenceDirectory, bool requireFix = true)
    {
        try {
            Directory.CreateDirectory(evidenceDirectory);
            using var form = new Form { Text = "ModernFormsNext physical monitor DPI acceptance", ClientSize = new Size(1200, 700) };
            form.Style.Border.Width = 0;
            form.TitleBar.Visible = false;
            var root = new ScenarioRoot { Dock = DockStyle.Fill };
            form.Controls.Add(root);
            var monitors = new List<MonitorInfo>();
            EnumDisplayMonitors(0, 0, (nint monitor, nint dc, ref NativeRect bounds, nint data) => {
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                Require(GetMonitorInfo(monitor, ref info), "Cannot query monitor bounds.");
                monitors.Add(info);
                return true;
            }, 0);
            var primary = monitors.Single(m => (m.Flags & 1) != 0);
            var retina = monitors.Single(m => m.Bounds.Right - m.Bounds.Left == 5120 && m.Bounds.Bottom - m.Bounds.Top == 2880);
            var results = new List<object>();
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(() => {
                try {
                    nint hwnd = form.PlatformHandle.Handle;
                    // Keep this short-lived acceptance window above unrelated desktop windows
                    // so pixel evidence captures only the scenario, without changing user focus.
                    SetWindowPos(hwnd, new nint(-1), 0, 0, 0, 0, 0x0013);
                    MoveTo(primary, 1);
                    Measure("primary-100-normal", 1);
                    MoveTo(retina, 2.25);
                    Measure("second-225-normal", 2.25);
                    form.WindowState = FormWindowState.Maximized;
                    UpdateWindow(hwnd);
                    Measure("second-225-maximized", 2.25);
                    form.WindowState = FormWindowState.Normal;
                    SetWindowPos(hwnd, 0, retina.Bounds.Left, retina.Bounds.Top, 5120, 2880, 0x0010);
                    UpdateWindow(hwnd);
                    Measure("second-225-full-5k", 2.25, exactFiveK: true);
                    MoveTo(primary, 1);
                    Measure("return-primary-100", 1);

                    void MoveTo(MonitorInfo monitor, double expectedScale)
                    {
                        form.WindowState = FormWindowState.Normal;
                        SetWindowPos(hwnd, 0, monitor.Work.Left + 80, monitor.Work.Top + 80, 800, 600, 0x0010);
                        Require(GetDpiForWindow(hwnd) == (uint)(96 * expectedScale), "Physical monitor DPI differs from the requested acceptance target.");
                        form.ClientSize = new Size(1200, 700);
                        UpdateWindow(hwnd);
                    }

                    void Measure(string name, double scale, bool exactFiveK = false)
                    {
                        Require(Math.Abs(form.Scaling - scale) < 0.0001 && GetDpiForWindow(hwnd) == (uint)(scale * 96),
                            "Framework scale disagrees with the physical HWND DPI.");
                        if (requireFix) Require(root.ClientSize == root.Size && new Rectangle(Point.Empty, root.Content.ClientSize).Contains(root.Card.Bounds),
                            "Physical monitor layout or overlay escaped the viewport.");
                        GetClientRect(hwnd, out var client);
                        if (exactFiveK) Require(client.Right == 5120 && client.Bottom == 2880, "Physical full-size buffer is not 5K.");
                        InvalidateRect(hwnd, 0, false); UpdateWindow(hwnd);
                        using var profiler = PerformanceProfiler.Start(new() { TrackAllocations = true, DetailedControls = true });
                        var areas = new List<long>();
                        for (int index = 0; index < 16; index++) {
                            var point = ButtonPoint();
                            if (index % 2 != 0) { point.X = (int)(220 * scale); point.Y = (int)(100 * scale); }
                            SendMessage(hwnd, 0x0200, 0, (nint)((point.Y << 16) | (point.X & 0xffff)));
                            GetUpdateRect(hwnd, out var damage, false);
                            areas.Add((long)(damage.Right - damage.Left) * (damage.Bottom - damage.Top));
                            UpdateWindow(hwnd);
                            Require(root.Button.IsHovering == (index % 2 == 0), "Physical monitor input misses the rendered control.");
                        }
                        var frames = profiler.Capture().Frames.Skip(2).ToArray();
                        if (requireFix) Require(frames.Length >= 12 && frames.All(frame => frame.Completed && frame.Work.LayoutPasses == 0 && frame.Work.SurfaceAllocations == 0),
                            "Physical hover layout/reallocation regression.");
                        if (requireFix) Require(frames.Select(frame => frame.RenderInfo.BackingGeneration).Distinct().Count() == 1 &&
                            areas.Skip(2).All(area => area > 0 && area < (long)client.Right * client.Bottom / 10),
                            "Physical hover lost bounded damage or recreated the framebuffer.");
                        int clicks = 0;
                        EventHandler<MouseEventArgs> clicked = (_, _) => clicks++;
                        root.Button.Click += clicked;
                        var center = ButtonPoint();
                        var location = (nint)((center.Y << 16) | (center.X & 0xffff));
                        SendMessage(hwnd, 0x0200, 0, location);
                        SendMessage(hwnd, 0x0201, 1, location);
                        SendMessage(hwnd, 0x0202, 0, location);
                        UpdateWindow(hwnd);
                        root.Button.Click -= clicked;
                        Require(clicks == 1, "Physical monitor press/release did not activate exactly once.");
                        using (var stream = File.Create(System.IO.Path.Combine(evidenceDirectory, name + ".json")))
                            profiler.WriteJson(stream);
                        var origin = new NativePoint();
                        ClientToScreen(hwnd, ref origin);
                        DwmFlush();
                        System.Threading.Thread.Sleep(100);
                        var rootBitmap = (SkiaSharp.SKBitmap)typeof(Control).GetField("back_buffer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(root)!;
                        using (var image = SkiaSharp.SKImage.FromBitmap(rootBitmap))
                        using (var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                        using (var stream = File.Create(System.IO.Path.Combine(evidenceDirectory, name + "-cache.png")))
                            data.SaveTo(stream);
                        const System.Reflection.BindingFlags privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                        var backend = typeof(WindowBase).GetField("window", privateInstance)!.GetValue(form)!;
                        var manager = backend.GetType().GetField("_framebuffer", privateInstance)!.GetValue(backend)!;
                        var backing = manager.GetType().GetField("_framebufferData", privateInstance)!.GetValue(manager)!;
                        var blob = backing.GetType().GetProperty("Data")!.GetValue(backing)!;
                        var address = (nint)blob.GetType().GetProperty("Address")!.GetValue(blob)!;
                        using (var image = SkiaSharp.SKImage.FromPixelCopy(new SkiaSharp.SKImageInfo(client.Right, client.Bottom, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul), address, client.Right * 4))
                        using (var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                        using (var stream = File.Create(System.IO.Path.Combine(evidenceDirectory, name + "-framebuffer.png")))
                            data.SaveTo(stream);
                        using (var screenshot = new Bitmap(client.Right, client.Bottom, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                            using (var graphics = Graphics.FromImage(screenshot))
                                graphics.CopyFromScreen(origin.X, origin.Y, 0, 0, screenshot.Size, CopyPixelOperation.SourceCopy);
                            screenshot.Save(System.IO.Path.Combine(evidenceDirectory, name + ".png"), ImageFormat.Png);
                            if (requireFix) {
                                using var expected = SkiaSharp.SKBitmap.Decode(System.IO.Path.Combine(evidenceDirectory, name + "-framebuffer.png"));
                                // Compare the entire displayed client, including preserved pixels.
                                // Sampling just the hovered button missed shifted ancestor regions.
                                var bits = screenshot.LockBits(new Rectangle(Point.Empty, screenshot.Size),
                                    ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                                try {
                                    var actual = new byte[bits.Stride * screenshot.Height];
                                    Marshal.Copy(bits.Scan0, actual, 0, actual.Length);
                                    byte[] pixels = expected.Bytes;
                                    for (int y = 0; y < screenshot.Height; y++)
                                        for (int x = 0; x < screenshot.Width; x++) {
                                            int source = y * expected.RowBytes + x * 4, destination = y * bits.Stride + x * 4;
                                            Require(actual[destination] == pixels[source] && actual[destination + 1] == pixels[source + 1] &&
                                                actual[destination + 2] == pixels[source + 2], "Displayed client pixels differ from the framebuffer.");
                                        }
                                }
                                finally { screenshot.UnlockBits(bits); }
                            }
                        }
                        results.Add(new { name, scale, nativeDpi = GetDpiForWindow(hwnd), width = client.Right, height = client.Bottom,
                            meanMs = frames.Average(f => f.Duration.TotalMilliseconds), maxMs = frames.Max(f => f.Duration.TotalMilliseconds),
                            meanRenderMs = frames.Average(f => f.Work.RenderTime.TotalMilliseconds),
                            meanPresentationCpuMs = frames.Average(f => f.RenderInfo.PresentationCpuTime?.TotalMilliseconds),
                            meanDamagePixels = areas.Skip(2).Average(), layout = root.ClientSize == root.Size && new Rectangle(Point.Empty, root.Content.ClientSize).Contains(root.Card.Bounds), last = frames.Last(), clicks });
                    }

                    NativePoint ButtonPoint()
                    {
                        var screen = root.Button.PointToScreen(new Point(root.Button.ScaledWidth / 2, root.Button.ScaledHeight / 2));
                        var point = new NativePoint { X = screen.X, Y = screen.Y };
                        ScreenToClient(hwnd, ref point);
                        return point;
                    }
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            string json = JsonSerializer.Serialize(new { physical = true, monitors, results }, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true });
            File.WriteAllText(System.IO.Path.Combine(evidenceDirectory, "physical-results.json"), json);
            Console.WriteLine(requireFix ? "PHYSICAL_HIGH_DPI:PASS" : "PHYSICAL_HIGH_DPI:BASELINE_MEASURED");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Bounds, Work; public uint Flags; }
    private delegate bool MonitorCallback(nint monitor, nint dc, ref NativeRect bounds, nint data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint hwnd, ref NativePoint point);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
