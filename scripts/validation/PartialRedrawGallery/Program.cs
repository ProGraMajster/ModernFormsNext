using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Threading;
using SkiaSharp;
using Path = System.IO.Path;

// Explicit local acceptance: actual Gallery pages and native displays, not part of portable CI.
// Only this process's windows are moved. No display configuration or desktop input is changed.
internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<object> Results = [];
    private static string output = "";
    private static bool observe;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Supply an evidence directory.");
        output = Path.GetFullPath(args[0]);
        observe = args.Contains("--observe");
        Directory.CreateDirectory(output);
        using var form = new ControlGallery.MainForm { ClientSize = new Size(1150, 800) };
        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(0, 0, (nint monitor, nint dc, ref NativeRect bounds, nint data) => {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) throw new InvalidOperationException("Cannot read display.");
            monitors.Add(info);
            return true;
        }, 0);
        var primary = monitors.Single(m => (m.Flags & 1) != 0);
        if (primary.Bounds.Right - primary.Bounds.Left != 1920 || primary.Bounds.Bottom - primary.Bounds.Top != 1080)
            throw new InvalidOperationException("This acceptance matrix requires the connected FullHD primary display.");
        var second = monitors.Single(m => m.Bounds.Right - m.Bounds.Left == 5120 && m.Bounds.Bottom - m.Bounds.Top == 2880);
        var steps = new Queue<Action>();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        Exception? failure = null;
        var tree = Field<TreeView>(form, "tree");
        Panel panel = null!, target = null!;
        DataGridView grid = null!;
        Panel overlay = null!;
        var scheduler = AnimationScheduler.Default;
        double priorDuration = scheduler.Policy.DurationScale;
        // Keep real intermediate frames visible despite synchronous PNG/capture work.
        scheduler.Policy.DurationScale = 12;
        var cases = new[] { ("primary-normal", primary, 1d, 0), ("primary-max", primary, 1d, 1),
            ("second-normal", second, 2.25, 0), ("second-max", second, 2.25, 1),
            ("second-full-5k", second, 2.25, 2), ("return-primary", primary, 1d, 0) };
        foreach (var (name, monitor, scale, mode) in cases) {
            if (args.Contains("--primary-only") && name != "primary-normal") continue;
            string? selectedCase = args.FirstOrDefault(argument => argument.StartsWith("--case=", StringComparison.Ordinal));
            if (selectedCase is not null && name != selectedCase["--case=".Length..]) continue;
            steps.Enqueue(() => {
                form.WindowState = FormWindowState.Normal;
                SetWindowPos(form.PlatformHandle.Handle, new nint(-1), monitor.Work.Left + 40, monitor.Work.Top + 40, 1000, 700, 0x0010);
                form.ClientSize = new Size(1150, 800);
                if (mode == 1) form.WindowState = FormWindowState.Maximized;
                if (mode == 2) SetWindowPos(form.PlatformHandle.Handle, 0, monitor.Bounds.Left, monitor.Bounds.Top, 5120, 2880, 0x0010);
                if (GetDpiForWindow(form.PlatformHandle.Handle) != (uint)(96 * scale) || form.Scaling != scale)
                    throw new InvalidOperationException("Native monitor DPI and framework scale disagree.");
                Select("DataGridView");
                grid = panel.Controls.OfType<DataGridView>().First();
                Paint(); Compare(form, name + "-grid-initial");
            });
            foreach (int row in new[] { 1, 3, 6, 0 })
                steps.Enqueue(() => { grid.FirstDisplayedScrollingRowIndex = row; Paint(); Compare(form, name + "-grid-scroll-" + row); });
            steps.Enqueue(() => { ScrollNavigation(); Paint(); Compare(form, name + "-grid-navigation-scroll"); });
            steps.Enqueue(() => {
                Select("Animations and Interaction Effects");
                target = Field<Panel>(panel, "animationTarget");
                Paint(); Compare(form, name + "-animation-initial");
                ScrollNavigation(); Paint(); Compare(form, name + "-animation-navigation-scroll");
                panel.Controls.OfType<Button>().First(b => b.Text == "Parallel").PerformClick();
            });
            for (int index = 0; index < 4; index++) {
                int frame = index;
                steps.Enqueue(() => {
                    Paint(); Compare(form, name + "-animation-frame-" + frame);
                    Record(new { name, frame, target.TranslationX, target.TranslationY, target.Rotation, target.ScaleX, target.Opacity });
                });
            }
            steps.Enqueue(() => {
                panel.Controls.OfType<Button>().First(b => b.Text == "Cancel").PerformClick();
                panel.Controls.OfType<Button>().First(b => b.Text == "Reset transforms").PerformClick();
                Paint(); Compare(form, name + "-animation-reset");
                panel.VerticalScrollProperties.Value = 60;
                Paint(); Compare(form, name + "-panel-scroll");
                panel.VerticalScrollProperties.Value = 0;
                Paint();
                overlay = panel.Controls.Add(new Panel { Bounds = new Rectangle(40, 210, 140, 65), BackColor = new SKColor(240, 20, 30, 180) });
                Paint(); Compare(form, name + "-overlap");
            });
            steps.Enqueue(() => {
                overlay.Location = new Point(210, 245); overlay.Size = new Size(75, 30);
                Paint(); Compare(form, name + "-move-shrink");
                overlay.Rotation = 25; Paint(); Compare(form, name + "-rotate");
                overlay.Visible = false; Paint(); Compare(form, name + "-hide");
                overlay.Visible = true; Paint(); Compare(form, name + "-show");
                panel.Controls.Remove(overlay); Paint(); Compare(form, name + "-remove"); overlay.Dispose();
            });
            steps.Enqueue(() => {
                var combo = Field<ComboBox>(panel, "ripplePolicyCombo");
                combo.DroppedDown = true;
                var popup = Field<PopupWindow>(combo, "popup");
                SetWindowPos(popup.PlatformHandle.Handle, new nint(-1), 0, 0, 0, 0, 0x0013);
                UpdateWindow(popup.PlatformHandle.Handle);
                using (Capture(popup, name + "-popup")) { }
                combo.DroppedDown = false;
                Paint(); Compare(form, name + "-popup-closed");
            });
            if (mode == 0)
                steps.Enqueue(() => {
                    form.ClientSize = new Size(1050, 720); Paint(); Compare(form, name + "-resize-smaller");
                    form.ClientSize = new Size(1150, 800); Paint(); Compare(form, name + "-resize-larger");
                });
        }
        if (steps.Count == 0) throw new ArgumentException("No matching display case.");
        timer.Tick += (_, _) => {
            try {
                if (steps.TryDequeue(out var step)) {
                    // Another app may show a topmost popup while this long capture runs.
                    // Raise only our owned window before the mutation; never repaint to
                    // repair a failed pixel comparison.
                    SetWindowPos(form.PlatformHandle.Handle, new nint(-1), 0, 0, 0, 0, 0x0013);
                    step();
                }
                else { timer.Stop(); Application.Exit(); }
            }
            catch (Exception error) { failure = error; timer.Stop(); Application.Exit(); }
        };
        form.Shown += (_, _) => timer.Start();
        try { Application.Run(form); }
        finally { timer.Stop(); scheduler.Policy.DurationScale = priorDuration; }
        if (failure is not null) { Console.Error.WriteLine(failure); return 1; }
        Console.WriteLine($"GALLERY_PARTIAL_REDRAW:{(observe ? "OBSERVED" : "PASS")} ({Results.Count} records)");
        return 0;

        void Select(string page) {
            tree.SelectedItem = tree.Items.First(item => item.Text == page);
            panel = Field<Panel>(form, "current_panel");
        }
        void ScrollNavigation() {
            var bar = Field<VerticalScrollBar>(tree, "vscrollbar");
            bar.Value = bar.Value == 0 ? Math.Min(bar.Maximum, 8) : 0;
        }
        void Paint() => UpdateWindow(form.PlatformHandle.Handle);
    }

    private static T Field<T>(object instance, string name)
    {
        for (Type? type = instance.GetType(); type is not null; type = type.BaseType)
            if (type.GetField(name, Private) is { } field) return (T)field.GetValue(instance)!;
        throw new MissingFieldException(instance.GetType().FullName, name);
    }

    private static void Compare(Form form, string name)
    {
        using var partial = Capture(form, name + "-partial");
        foreach (var control in form.Controls) Dirty(control);
        form.Invalidate(); UpdateWindow(form.PlatformHandle.Handle);
        using var full = Capture(form, name + "-full");
        int difference = Difference(partial, full);
        Record(new { name, partialVsFull = difference });
        if (!observe && difference != 0) throw new InvalidOperationException(name + ": stale framebuffer pixels.");
    }

    private static void Dirty(Control control) { control.Invalidate(); foreach (var child in control.Controls) Dirty(child); }

    private static SKBitmap Capture(WindowBase window, string name)
    {
        nint hwnd = window.PlatformHandle.Handle;
        GetClientRect(hwnd, out var client); var origin = new NativePoint(); ClientToScreen(hwnd, ref origin);
        var backend = typeof(WindowBase).GetField("window", Private)!.GetValue(window)!;
        var manager = Field<object>(backend, "_framebuffer");
        var backing = Field<object>(manager, "_framebufferData");
        var blob = backing.GetType().GetProperty("Data")!.GetValue(backing)!;
        var address = (nint)blob.GetType().GetProperty("Address")!.GetValue(blob)!;
        using var image = SKImage.FromPixelCopy(new SKImageInfo(client.Right, client.Bottom, SKColorType.Bgra8888, SKAlphaType.Premul), address, client.Right * 4);
        using (var encoded = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.Create(Path.Combine(output, name + "-buffer.png"))) encoded.SaveTo(stream);
        DwmFlush(); Thread.Sleep(60);
        using (var screenshot = new Bitmap(client.Right, client.Bottom)) {
            using (var graphics = Graphics.FromImage(screenshot))
                graphics.CopyFromScreen(origin.X, origin.Y, 0, 0, screenshot.Size, CopyPixelOperation.SourceCopy);
            screenshot.Save(Path.Combine(output, name + "-screen.png"));
        }
        var pixels = SKBitmap.FromImage(image);
        using var screen = SKBitmap.Decode(Path.Combine(output, name + "-screen.png"));
        int difference = Difference(screen, pixels);
        Record(new { name, screenVsBuffer = difference, width = client.Right, height = client.Bottom,
            originX = origin.X, originY = origin.Y, scale = window.Scaling });
        if (!observe && difference != 0) {
            bool occluded = false;
            for (int y = 4; y < client.Bottom; y += 32)
                for (int x = 4; x < client.Right; x += 32)
                    if (GetAncestor(WindowFromPoint(new NativePoint { X = origin.X + x, Y = origin.Y + y }), 2) != hwnd)
                        occluded = true;
            pixels.Dispose();
            throw new InvalidOperationException(name + (occluded ? ": capture occluded by another native window; rerun with the client unobstructed."
                : ": displayed pixels differ from framebuffer."));
        }
        return pixels;
    }

    private static int Difference(SKBitmap a, SKBitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height) throw new InvalidOperationException("Different image sizes.");
        byte[] left = a.Bytes, right = b.Bytes;
        int count = 0;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++) {
                int i = y * a.RowBytes + x * 4, j = y * b.RowBytes + x * 4;
                if (left[i] != right[j] || left[i + 1] != right[j + 1] || left[i + 2] != right[j + 2]) count++;
            }
        return count;
    }

    private static void Record(object record)
    {
        Results.Add(record);
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Bounds, Work; public uint Flags; }
    private delegate bool MonitorCallback(nint monitor, nint dc, ref NativeRect bounds, nint data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint hwnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
