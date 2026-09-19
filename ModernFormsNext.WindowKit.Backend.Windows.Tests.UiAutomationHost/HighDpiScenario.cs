using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModernFormsNext;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit.Threading;

// Runs on an owned HWND. Injected WM_DPICHANGED exercises the real conversion/resize path;
// it deliberately does not claim that Windows or a physical monitor changed its DPI.
internal static partial class HighDpiScenario
{
    internal static int Run(bool requireFix = true)
    {
        try {
            using var form = new Form { Text = "ModernFormsNext high DPI regression", ClientSize = new Size(1200, 700) };
            // Windows limits an unconstrained top-level HWND to the virtual desktop's
            // default maximum tracking size. CI desktops can be smaller than the raster
            // cases below. Explicitly permit the fixture's offscreen 5K buffer without
            // changing monitor settings or production defaults.
            form.MaximumSize = new Size(8192, 8192);
            form.Style.Border.Width = 0;
            form.TitleBar.Visible = false;
            var root = new ScenarioRoot { Dock = DockStyle.Fill };
            form.Controls.Add(root);
            var results = new List<object>();
            Exception? failure = null;
            bool maximized = false, restored = false;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(() => {
                try {
                    nint hwnd = form.PlatformHandle.Handle;
                    // This case deliberately does not pre-size the HWND: the handler must apply
                    // the suggested physical rectangle, not merely store the new scale.
                    SetWindowPos(hwnd, 0, 40, 40, 1200, 700, 0x0014);
                    ApplySuggestedDpi(hwnd, 1.25, new NativeRect { Left = 60, Top = 70, Right = 1560, Bottom = 970 });
                    GetWindowRect(hwnd, out var suggestedResult);
                    Require(suggestedResult.Left == 60 && suggestedResult.Top == 70 && suggestedResult.Right == 1560 && suggestedResult.Bottom == 970,
                        $"WM_DPICHANGED did not apply the physical suggested rectangle exactly once: {suggestedResult.Left},{suggestedResult.Top}–{suggestedResult.Right},{suggestedResult.Bottom}; scale {form.Scaling}.");
                    Require(form.ClientSize == new Size(1200, 720), "Suggested rectangle did not update logical client size.");
                    foreach (double scale in new[] { 1d, 1.25, 1.5, 1.75, 2, 2.25, 2.5 })
                        Measure(scale, (int)(1200 * scale), (int)(700 * scale), false);
                    Measure(2.25, 5120, 2880, true);
                    form.WindowState = FormWindowState.Maximized;
                    UpdateWindow(hwnd);
                    maximized = root.Width == form.ClientSize.Width && root.Height == form.ClientSize.Height;
                    form.WindowState = FormWindowState.Normal;
                    SetDpiAndClient(hwnd, 1.5, 1500, 1050);
                    UpdateWindow(hwnd);
                    restored = root.Width == form.ClientSize.Width && root.Height == form.ClientSize.Height;
                    if (requireFix) Require(maximized && restored, "Maximize/restore lost logical dock layout.");

                    void Measure(double scale, int width, int height, bool fiveK)
                    {
                        SetDpiAndClient(hwnd, scale, width, height);
                        InvalidateRect(hwnd, 0, false); UpdateWindow(hwnd);
                        GetClientRect(hwnd, out var client);
                        Require(client.Right == width && client.Bottom == height,
                            $"Suggested physical size changed unexpectedly at {scale}: {client.Right}x{client.Bottom}, expected {width}x{height}; framework scale {form.Scaling}, HWND DPI {GetDpiForWindow(hwnd)}.");
                        Require(Math.Abs(form.Scaling - scale) < 0.0001, "WM_DPICHANGED scale was not retained.");
                        GetWindowRect(hwnd, out var outer);
                        Require(outer.Left == 40 && outer.Top == 40, "Suggested physical origin was scaled again.");
                        bool layout = root.ClientSize == root.Size && new Rectangle(Point.Empty, root.Content.Size).Contains(root.Card.Bounds);
                        if (requireFix) Require(layout, "ClientSize or setup layout uses device units.");
                        using var profiler = PerformanceProfiler.Start(new() { TrackAllocations = true, DetailedControls = true });
                        var damageAreas = new List<long>();
                        var hoverResponses = new List<double>();
                        for (int i = 0; i < 14; i++) {
                            var point = root.Button.PointToScreen(new Point(root.Button.ScaledWidth / 2, root.Button.ScaledHeight / 2));
                            var nativePoint = new NativePoint { X = point.X, Y = point.Y };
                            ScreenToClient(hwnd, ref nativePoint);
                            if (i % 2 != 0) { nativePoint.X = (int)(220 * scale); nativePoint.Y = (int)(100 * scale); }
                            long start = System.Diagnostics.Stopwatch.GetTimestamp();
                            SendMessage(hwnd, 0x0200, 0, (nint)((nativePoint.Y << 16) | (nativePoint.X & 0xffff)));
                            GetUpdateRect(hwnd, out var damage, false);
                            damageAreas.Add((long)(damage.Right - damage.Left) * (damage.Bottom - damage.Top));
                            UpdateWindow(hwnd);
                            hoverResponses.Add(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                            if (requireFix) Require(root.Button.IsHovering == (i % 2 == 0), "Native input misses the rendered button.");
                        }
                        var snapshot = profiler.Capture();
                        var frames = snapshot.Frames.Skip(2).ToArray();
                        Require(frames.Length >= 10, "Hover did not produce native frames.");
                        Require(frames.All(f => f.Completed && f.RenderInfo.PixelWidth == width && f.RenderInfo.PixelHeight == height &&
                            f.RenderInfo.RowBytes == width * 4), "Native framebuffer dimensions or completion mismatch.");
                        bool stableBacking = frames.Select(f => f.RenderInfo.BackingGeneration).Distinct().Count() == 1;
                        if (requireFix) Require(stableBacking && frames.All(f => f.Work.SurfaceAllocations == 0 && f.Work.LayoutPasses == 0) &&
                            damageAreas.Skip(2).All(area => area > 0 && area < (long)width * height / 10),
                            "Hover recreated surfaces, reran layout, or invalidated the entire window.");
                        results.Add(new { scale, fiveK, width, height, rowBytes = width * 4, backingBytes = (long)width * height * 4,
                            logicalClient = form.ClientSize, layout, stableBacking,
                            nativeDpi = GetDpiForWindow(hwnd), injectedDpi = (int)(96 * scale),
                            meanFrameMs = frames.Average(f => f.Duration.TotalMilliseconds), maxFrameMs = frames.Max(f => f.Duration.TotalMilliseconds),
                            meanRenderMs = frames.Average(f => f.Work.RenderTime.TotalMilliseconds),
                            meanHoverMs = hoverResponses.Skip(2).Average(), maxHoverMs = hoverResponses.Skip(2).Max(),
                            meanDamagePixels = damageAreas.Skip(2).Average(),
                            meanAllocatedBytes = frames.Average(f => (double)(f.AllocatedBytes ?? 0)),
                            last = frames.Last(), unframed = snapshot.UnframedWork });
                    }
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("HIGH_DPI:" + JsonSerializer.Serialize(new { results, maximized, restored,
                physicalValidation = "NOT EXECUTED â€” exact physical 5K / 225% environment unavailable" }));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void SetDpiAndClient(nint hwnd, double scale, int width, int height)
    {
        // Size first so Windows can settle any genuine monitor transition before synthetic
        // DPI is injected. Otherwise a large rectangle crossing monitors can legitimately
        // cause a nested OS WM_DPICHANGED that supersedes the injected scale.
        for (int attempt = 0; attempt < 3; attempt++) {
            GetWindowRect(hwnd, out var currentOuter); GetClientRect(hwnd, out var currentClient);
            if (currentClient.Right == width && currentClient.Bottom == height) break;
            SetWindowPos(hwnd, 0, 40, 40, width + currentOuter.Right - currentOuter.Left - currentClient.Right,
                height + currentOuter.Bottom - currentOuter.Top - currentClient.Bottom, 0x0014);
        }
        GetWindowRect(hwnd, out var outer); GetClientRect(hwnd, out var client);
        var suggested = new NativeRect { Left = 40, Top = 40,
            Right = 40 + width + outer.Right - outer.Left - client.Right,
            Bottom = 40 + height + outer.Bottom - outer.Top - client.Bottom };
        ApplySuggestedDpi(hwnd, scale, suggested);
    }

    private static void ApplySuggestedDpi(nint hwnd, double scale, NativeRect suggested)
    {
        nint memory = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRect>());
        try {
            Marshal.StructureToPtr(suggested, memory, false);
            int dpi = (int)Math.Round(96 * scale);
            SendMessage(hwnd, 0x02E0, (nint)((dpi << 16) | dpi), memory);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private sealed class ScenarioRoot : Panel
    {
        internal Panel Content { get; } = new() { Dock = DockStyle.Fill };
        internal Panel Card { get; } = new() { Size = new Size(660, 380) };
        internal Button Button { get; } = new() { Bounds = new Rectangle(18, 18, 192, 46), Text = "Downloads",
            Padding = new Padding(48, 0, 10, 0), TextAlign = ModernFormsNext.ContentAlignment.MiddleLeft };
        internal ScenarioRoot()
        {
            Controls.Add(Content);
            var sidebar = Controls.Add(new Panel { Dock = DockStyle.Left, Width = 236 });
            sidebar.Controls.Add(Button);
            Button.Controls.Add(new Panel { Bounds = new Rectangle(15, 11, 24, 24) });
            var wide = Content.Controls.Add(new Panel { Height = 100, Top = 160 });
            Content.Controls.Add(Card);
            Card.Controls.Add(new Label { Text = "First-run setup", Dock = DockStyle.Top, Height = 40 });
            Content.Layout += (_, _) => {
                Card.Left = Math.Max(24, (Content.ClientSize.Width - Card.Width) / 2);
                Card.Top = Math.Max(24, (Content.ClientSize.Height - Card.Height) / 2);
                wide.Width = Math.Max(1, Content.ClientSize.Width - 32);
            };
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { internal int X, Y; }
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern bool GetUpdateRect(nint hwnd, out NativeRect rectangle, bool erase);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(nint hwnd, nint rectangle, bool erase);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(nint hwnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
}
