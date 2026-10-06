using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Text.Json;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.Drawing;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

// Opt-in file output makes the identical fixture usable against an exact pre-refactor checkout.
// Ordinary CI still checks repeatability without machine-dependent committed font goldens.
public sealed class RenderingParityTests
{
    [Theory]
    [InlineData(1d, false)]
    [InlineData(1.25d, false)]
    [InlineData(1.5d, false)]
    [InlineData(2d, false)]
    [InlineData(1d, true)]
    [InlineData(1.25d, true)]
    [InlineData(2d, true)]
    public void RepresentativeWindowAndPopupRemainPixelIdentical(double scale, bool overlay)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { Text = "Software rendering parity" };
        var panel = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        panel.Controls.Add(new Button { Text = "Save", Bounds = new Rectangle(12, 12, 130, 36) });
        panel.Controls.Add(new TextBox { Text = "Zażółć 123", Bounds = new Rectangle(12, 60, 210, 36) });
        panel.Controls.Add(new Label { Text = "CPU / fractional DPI", Bounds = new Rectangle(12, 106, 270, 32) });
        var gradient = new LinearGradientBrush();
        gradient.GradientStops.Add(new GradientStop(Color.CornflowerBlue, 0));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(95, 255, 100, 40), 1));
        panel.Controls.Add(new Ellipse { Bounds = new Rectangle(255, 12, 140, 100), Fill = gradient, Stroke = null });
        var scroll = panel.Controls.Add(new ScrollableControl { Bounds = new Rectangle(12, 152, 225, 120), AutoScroll = true });
        scroll.Controls.Add(new Label { Text = "Clipped scroll content", Bounds = new Rectangle(5, 3, 270, 36) });
        scroll.Controls.Add(new Button { Text = "Beyond viewport", Bounds = new Rectangle(10, 140, 220, 36) });
        var combo = panel.Controls.Add(new ComboBox { Bounds = new Rectangle(255, 160, 150, 32) });
        combo.Items.Add("Alpha");
        combo.Items.Add("Beta");
        var window = host.Show(form, new TestViewport(480, 360, scale));
        using var profiler = overlay ? PerformanceProfiler.Start(new PerformanceProfilerOptions {
            Overlay = new PerformanceOverlayOptions { Visible = true, Metrics = PerformanceOverlayMetrics.None }
        }) : null;
        using (window.CaptureRenderedSnapshot()) { }
        using var first = window.CaptureRenderedSnapshot();
        using var second = window.CaptureRenderedSnapshot();
        Assert.Equal(first.CopyPixels(), second.CopyPixels());
        string key = $"scale-{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}-overlay-{overlay}";
        Save(key + "-window", first);
        window.Resize(503, 381);
        using var resized = window.CaptureRenderedSnapshot();
        Save(key + "-resized", resized);
        window.Input.Click(combo);
        var popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        // Cold popup text was not stable across baseline processes at 150% DPI. Warm the same
        // production path on both revisions, then require exact equality (no pixel tolerance).
        using (popup.CaptureRenderedSnapshot()) { }
        using var popupImage = popup.CaptureRenderedSnapshot();
        using var repeatedPopup = popup.CaptureRenderedSnapshot();
        Assert.Equal(popupImage.CopyPixels(), repeatedPopup.CopyPixels());
        Save(key + "-popup", popupImage);
        Assert.Equal(scale, popupImage.RenderScale);
    }

    [Fact]
    public void RepresentativeCaptureMeasurements()
    {
        // Includes TestHost allocation/copy/layout costs; this is a bounded comparison, not
        // a native display benchmark. Warm-up, viewport and iterations are identical on both SHAs.
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        root.Controls.Add(new Button { Text = "CPU timing", Bounds = new Rectangle(10, 10, 150, 40) });
        root.Controls.Add(new TextBox { Text = "Frame", Bounds = new Rectangle(10, 65, 240, 36) });
        var window = host.Show(root, new TestViewport(480, 360, 1.25));
        for (int i = 0; i < 30; i++) using (window.CaptureRenderedSnapshot()) { }
        var samples = new List<object>();
        for (int sample = 0; sample < 5; sample++) {
            window.Resize(480, 360);
            using (window.CaptureRenderedSnapshot()) { }
            const int count = 100;
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++) using (window.CaptureRenderedSnapshot()) { }
            double milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds / count;
            long allocated = (GC.GetAllocatedBytesForCurrentThread() - bytes) / count;
            start = Stopwatch.GetTimestamp();
            for (int i = 0; i < 20; i++) {
                window.Resize(480 + i % 2, 360 + i % 2);
                using (window.CaptureRenderedSnapshot()) { }
            }
            samples.Add(new { milliseconds, allocatedBytes = allocated,
                resizeMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds / 20,
                backingBytes = 600 * 450 * 4 });
        }
        if (Environment.GetEnvironmentVariable("MFN_RENDER_OUTPUT") is { Length: > 0 } output) {
            Directory.CreateDirectory(output);
            File.WriteAllText(System.IO.Path.Combine(output, "capture-measurements.json"), JsonSerializer.Serialize(samples));
            using var profiler = PerformanceProfiler.Start();
            for (int i = 0; i < 30; i++) using (window.CaptureRenderedSnapshot()) { }
            var frames = profiler.Capture().Frames;
            File.WriteAllText(System.IO.Path.Combine(output, "paint-measurements.json"), JsonSerializer.Serialize(new {
                frames = frames.Count, meanPaintCpuMilliseconds = frames.Average(frame => frame.Work.RenderTime.TotalMilliseconds)
            }));
        }
    }

    private static void Save(string key, RenderedSnapshot image)
    {
        byte[] pixels = image.CopyPixels();
        string hash = Convert.ToHexString(SHA256.HashData(pixels));
        if (Environment.GetEnvironmentVariable("MFN_RENDER_BASELINE") is { Length: > 0 } baseline)
            Assert.Equal(File.ReadAllBytes(System.IO.Path.Combine(baseline, key + ".bgra")), pixels);
        if (Environment.GetEnvironmentVariable("MFN_RENDER_OUTPUT") is { Length: > 0 } output) {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(System.IO.Path.Combine(output, key + ".bgra"), pixels);
            File.WriteAllBytes(System.IO.Path.Combine(output, key + ".png"), image.EncodePng());
            File.WriteAllText(System.IO.Path.Combine(output, key + ".json"), JsonSerializer.Serialize(new {
                image.PixelWidth, image.PixelHeight, image.RowBytes, image.RenderScale, sha256 = hash }));
        }
    }
}
