using System.Drawing;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit.Input;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class PerformanceOverlayTests
{
    public static TheoryData<PerformanceOverlayCorner, double> CornersAndScales
    {
        get
        {
            var data = new TheoryData<PerformanceOverlayCorner, double>();
            foreach (var corner in Enum.GetValues<PerformanceOverlayCorner>())
                foreach (double scale in new[] { 1d, 1.25d, 1.5d, 2d }) data.Add(corner, scale);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CornersAndScales))]
    public void HeadlessPaintPlacesInputTransparentHudInTheRequestedLogicalCorner(PerformanceOverlayCorner corner, double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new SolidControl();
        var window = host.Show(root, new TestViewport(500, 420, scale));
        using (window.CaptureRenderedSnapshot()) { }
        int baselineLayout = root.LayoutCalls;
        using (window.CaptureRenderedSnapshot()) { }
        int baselinePaintLayouts = root.LayoutCalls - baselineLayout;
        using var profiler = PerformanceProfiler.Start(new PerformanceProfilerOptions
        {
            Overlay = new PerformanceOverlayOptions { Visible = true, Metrics = PerformanceOverlayMetrics.None, Corner = corner, Margin = 10 }
        });
        using (window.CaptureRenderedSnapshot()) { }
        int layout = root.LayoutCalls;
        using var pixels = window.CaptureRenderedSnapshot();
        Rectangle changed = FindChangedBounds(pixels);

        Assert.False(changed.IsEmpty);
        // TestHost stabilizes layout before every capture even without diagnostics.
        // The HUD must not add layout work to that established capture path.
        Assert.Equal(baselinePaintLayouts, root.LayoutCalls - layout);
        Assert.InRange(changed.Left, (int)(9 * scale), pixels.PixelWidth - (int)(9 * scale));
        Assert.InRange(changed.Top, (int)(9 * scale), pixels.PixelHeight - (int)(9 * scale));
        Assert.InRange(changed.Right, 1, pixels.PixelWidth - (int)(9 * scale));
        Assert.InRange(changed.Bottom, 1, pixels.PixelHeight - (int)(9 * scale));
        if (corner is PerformanceOverlayCorner.TopLeft or PerformanceOverlayCorner.BottomLeft)
            Assert.InRange(changed.Left, (int)(9 * scale), (int)(11 * scale));
        else
            Assert.InRange(changed.Right, pixels.PixelWidth - (int)(11 * scale), pixels.PixelWidth - (int)(9 * scale));
        if (corner is PerformanceOverlayCorner.TopLeft or PerformanceOverlayCorner.TopRight)
            Assert.InRange(changed.Top, (int)(9 * scale), (int)(11 * scale));
        else
            Assert.InRange(changed.Bottom, pixels.PixelHeight - (int)(11 * scale), pixels.PixelHeight - (int)(9 * scale));
        Assert.Empty(root.Controls);
    }

    [Theory]
    [InlineData(PerformanceOverlayMode.Compact, false)]
    [InlineData(PerformanceOverlayMode.Compact, true)]
    [InlineData(PerformanceOverlayMode.Expanded, false)]
    [InlineData(PerformanceOverlayMode.Expanded, true)]
    public void ModesAndGraphClipOnlyTheirOwnDrawingAndDoNotCreateIdleWork(PerformanceOverlayMode mode, bool graph)
    {
        using var host = ModernFormsTestHost.Create();
        using var profiler = PerformanceProfiler.Start(new PerformanceProfilerOptions
        {
            Overlay = new PerformanceOverlayOptions { Visible = true, Mode = mode, ShowFrameGraph = graph, Metrics = PerformanceOverlayMetrics.All }
        });
        var root = new SolidControl();
        var window = host.Show(root, 210, 170);
        using (window.CaptureRenderedSnapshot()) { }
        host.ProcessPendingWork();
        var before = host.GetDiagnostics();
        using var image = window.CaptureRenderedSnapshot();
        var after = host.GetDiagnostics();

        Assert.Equal(SolidControl.Background, image.GetPixel(0, 0));
        Assert.Equal(SolidControl.Background, image.GetPixel(209, 169));
        Assert.False(FindChangedBounds(image).IsEmpty);
        Assert.Equal(before.PendingDispatcherWorkCount, after.PendingDispatcherWorkCount);
        Assert.Equal(before.PendingInvalidationCount, after.PendingInvalidationCount);
        Assert.Equal(before.ActiveAnimationCount, after.ActiveAnimationCount);
        Assert.Empty(after.DispatcherExceptions);
    }

    [Fact]
    public void SurfaceWithExistingDensityTransformIsNotScaledTwiceAndCanvasStateIsRestored()
    {
        using var host = ModernFormsTestHost.Create();
        using var profiler = PerformanceProfiler.Start(new PerformanceProfilerOptions
        {
            Overlay = new PerformanceOverlayOptions { Visible = true, Metrics = PerformanceOverlayMetrics.None, Corner = PerformanceOverlayCorner.BottomRight, Margin = 10 }
        });
        using var surface = new SkiaControlSurface(new SolidControl());
        surface.Resize(500, 420);
        using var bitmap = new SKBitmap(1000, 840);
        using var canvas = new SKCanvas(bitmap);
        canvas.Scale(2);
        var matrix = canvas.TotalMatrix;
        int saves = canvas.SaveCount;
        surface.Render(canvas);
        surface.Render(canvas);

        Assert.Equal(matrix, canvas.TotalMatrix);
        Assert.Equal(saves, canvas.SaveCount);
        Assert.Equal(SolidControl.Background, bitmap.GetPixel(998, 838));
        Assert.NotEqual(SolidControl.Background, bitmap.GetPixel(975, 810));
        Assert.Equal(SolidControl.Background, bitmap.GetPixel(500, 400));
    }

    [Fact]
    public void HudDoesNotInterceptPointerOrReplaceFocusedCompositionAndExistingBindings()
    {
        using var host = ModernFormsTestHost.Create();
        using var profiler = PerformanceProfiler.Start(new PerformanceProfilerOptions
        {
            Overlay = new PerformanceOverlayOptions { Visible = true, Corner = PerformanceOverlayCorner.TopLeft }
        });
        var root = new Panel();
        var button = root.Controls.Add(new Button { Text = "Behind HUD", Bounds = new Rectangle(15, 15, 160, 35) });
        var editor = root.Controls.Add(new TextBox { Bounds = new Rectangle(15, 65, 160, 35) });
        int clicked = 0, existingCalls = 0;
        button.Click += (_, _) => clicked++;
        var existing = new KeyBinding(new DelegateCommand(() => existingCalls++), new KeyGesture(Keys.F2));
        var toggle = new KeyBinding(new DelegateCommand(() => profiler.OverlayOptions = profiler.OverlayOptions with { Visible = !profiler.OverlayOptions.Visible }),
            new KeyGesture(Keys.F12, KeyModifiers.Control));
        root.InputBindings.Add(existing);
        root.InputBindings.Add(toggle);
        var window = host.Show(root, 500, 420);
        using (window.CaptureRenderedSnapshot()) { }
        host.Input.Click(button);
        Assert.Equal(1, clicked);
        Assert.True(host.Input.Focus(editor));
        var client = host.Input.TextInputClient!;
        Assert.True(client.SetComposingText("zaż😀"));
        profiler.OverlayOptions = profiler.OverlayOptions with { Mode = PerformanceOverlayMode.Expanded };
        using (window.CaptureRenderedSnapshot()) { }
        Assert.Same(client, host.Input.TextInputClient);
        Assert.True(client.GetState()!.HasComposition);
        Assert.True(client.FinishComposition());
        host.Input.PressKey(Keys.Control | Keys.F12);
        Assert.False(profiler.OverlayOptions.Visible);
        root.InputBindings.Remove(toggle);
        host.Input.PressKey(Keys.F2);
        Assert.Equal(1, existingCalls);
        Assert.Single(root.InputBindings);
        Assert.Equal("zaż😀", editor.Text);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidVisualOptionsAreRejectedBeforeChangingTheSession(double margin)
    {
        using var host = ModernFormsTestHost.Create();
        using var profiler = PerformanceProfiler.Start();
        var original = profiler.OverlayOptions;
        Assert.Throws<ArgumentOutOfRangeException>(() => { profiler.OverlayOptions = original with { Visible = true, Margin = margin }; });
        Assert.Same(original, profiler.OverlayOptions);
        Assert.Throws<ArgumentOutOfRangeException>(() => { profiler.OverlayOptions = original with { Mode = (PerformanceOverlayMode)99 }; });
        Assert.Throws<ArgumentOutOfRangeException>(() => { profiler.OverlayOptions = original with { Corner = (PerformanceOverlayCorner)99 }; });
        Assert.Throws<ArgumentOutOfRangeException>(() => { profiler.OverlayOptions = original with { Metrics = (PerformanceOverlayMetrics)int.MaxValue }; });
    }

    [Fact]
    public void HiddenOverlayRecordsFramesAndDisposedSessionLeavesNoVisualsOrClock()
    {
        using var host = ModernFormsTestHost.Create();
        using var profiler = PerformanceProfiler.Start();
        var window = host.Show(new SolidControl(), 500, 420);
        using (var hidden = window.CaptureRenderedSnapshot()) Assert.True(FindChangedBounds(hidden).IsEmpty);
        Assert.NotEmpty(profiler.Capture().Frames);
        profiler.OverlayOptions = profiler.OverlayOptions with { Visible = true };
        using (var visible = window.CaptureRenderedSnapshot()) Assert.False(FindChangedBounds(visible).IsEmpty);
        profiler.Dispose();
        host.ProcessPendingWork();
        using (var disposed = window.CaptureRenderedSnapshot()) Assert.True(FindChangedBounds(disposed).IsEmpty);
        Assert.Equal(0, host.GetDiagnostics().ActiveAnimationCount);
        using var next = PerformanceProfiler.Start();
        Assert.False(next.OverlayOptions.Visible);
    }

    private static Rectangle FindChangedBounds(RenderedSnapshot snapshot)
    {
        int left = snapshot.PixelWidth, top = snapshot.PixelHeight, right = -1, bottom = -1;
        for (int y = 0; y < snapshot.PixelHeight; y++)
            for (int x = 0; x < snapshot.PixelWidth; x++)
                if (snapshot.GetPixel(x, y) != SolidControl.Background)
                {
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private sealed class SolidControl : Control
    {
        internal static readonly SKColor Background = SKColors.Magenta;
        internal int LayoutCalls { get; private set; }
        protected override void OnPaintBackground(PaintEventArgs e) => e.Canvas.Clear(Background);
        protected override void OnLayout(LayoutEventArgs e) { LayoutCalls++; base.OnLayout(e); }
    }
}
