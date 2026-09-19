using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.Diagnostics;
using SkiaSharp;
using Xunit;
using Rect = ModernFormsNext.WindowKit.Rect;
using PersistentSurface = ModernFormsNext.Testing.Tests.HighDpiRegressionTests.PersistentSurface;

namespace ModernFormsNext.Testing.Tests;

public sealed class PartialRedrawPixelTests
{
    public static TheoryData<double> Scales => HighDpiRegressionTests.Scales;

    [Theory]
    [MemberData(nameof(Scales))]
    public void GeometryVisibilityRemovalAndReparentRestoreExposedPixels(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorPanel(SKColors.Navy);
        var parent = root.Controls.Add(new ColorPanel(SKColors.Beige) { Bounds = new(10, 10, 300, 230) });
        var other = root.Controls.Add(new ColorPanel(SKColors.Green) { Bounds = new(320, 10, 130, 230) });
        var child = parent.Controls.Add(new Button { Bounds = new(20, 20, 110, 60), Text = "Move me" });
        var window = host.Show(root, new TestViewport(470, 280, scale));
        using var surface = new PersistentSurface(window);
        surface.PaintFull();
        Check(() => child.Location = new(170, 100));
        Assert.Equal(SKColors.Beige, Pixel(50, 50)); // The old button interior is now parent background.
        Assert.NotEqual(SKColors.Beige, Pixel(200, 130));
        Check(() => child.Size = new(60, 30));
        Assert.Equal(SKColors.Beige, Pixel(270, 155)); // Area exposed by shrinking.
        Check(() => child.Visible = false);
        Assert.Equal(SKColors.Beige, Pixel(200, 130));
        Check(() => child.Visible = true);
        Check(() => parent.Controls.Remove(child));
        Assert.Equal(SKColors.Beige, Pixel(200, 130));
        Check(() => parent.Controls.Add(child));
        Check(() => { child.Location = new(20, 20); other.Controls.Add(child); });
        Assert.Equal(SKColors.Beige, Pixel(200, 130));
        Assert.NotEqual(SKColors.Green, Pixel(360, 50));
        Check(() => child.Bounds = new(40, 80, 70, 50));
        Check(() => child.Anchor = AnchorStyles.Right | AnchorStyles.Bottom);
        Check(() => other.Size = new(140, 240));
        Check(() => child.Dock = DockStyle.Bottom);
        Check(() => other.Size = new(130, 230));

        void Check(Action change) => CheckMutation(root, window, surface, change);
        SKColor Pixel(int x, int y) => surface.Bitmap.GetPixel((int)(x * scale), (int)(y * scale));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void MovingTranslucentTransformedSiblingRestoresUnderlyingContent(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorPanel(SKColors.Navy);
        var parent = root.Controls.Add(new ColorPanel(SKColors.Beige) { Bounds = new(10, 10, 350, 230) });
        parent.Controls.Add(new ColorPanel(SKColors.Gold) { Bounds = new(20, 20, 230, 160) });
        var moving = parent.Controls.Add(new ColorPanel(new SKColor(255, 0, 0, 160)) { Bounds = new(40, 40, 90, 60) });
        var window = host.Show(root, new TestViewport(400, 280, scale));
        using var surface = new PersistentSurface(window);
        surface.PaintFull();
        foreach (Action change in new Action[] {
            () => moving.Location = new(180, 130), () => moving.Size = new(45, 30),
            () => moving.Rotation = 29, () => moving.ScaleX = 1.5f,
            () => moving.TranslationX = -240, () => moving.TranslationX = 600,
            () => { moving.TranslationX = 0; moving.Rotation = 0; moving.ScaleX = 1; },
            () => moving.Visible = false, () => moving.Visible = true })
            CheckMutation(root, window, surface, change);
        Assert.Equal(SKColors.Gold, surface.Bitmap.GetPixel((int)(75 * scale), (int)(75 * scale)));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TestClockIntermediateLayoutAndTransformFramesLeaveNoOldGeometry(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorPanel(SKColors.Beige);
        var target = root.Controls.Add(new ColorPanel(SKColors.Crimson) { Bounds = new(20, 20, 80, 50) });
        var window = host.Show(root, new TestViewport(400, 280, scale));
        using var surface = new PersistentSurface(window);
        surface.PaintFull();
        var animation = AnimationScheduler.Default.Start(target, "pixel-movement", progress => {
            target.TranslationX = 210 * progress;
            target.TranslationY = 80 * progress;
            target.Rotation = 35 * progress;
        }, new AnimationOptions { Duration = TimeSpan.FromSeconds(1), Easing = Easings.Linear });
        for (int frame = 0; frame < 4; frame++)
            CheckMutation(root, window, surface, () => host.Clock.Advance(TimeSpan.FromMilliseconds(250)));
        Assert.Equal(AnimationState.Completed, animation.State);
        CheckMutation(root, window, surface, () => { target.TranslationX = 0; target.TranslationY = 0; target.Rotation = 0; });
        target.LayoutTransition = new LayoutTransition { Duration = TimeSpan.FromSeconds(1), Easing = Easings.Linear };
        CheckMutation(root, window, surface, () => target.Bounds = new(230, 160, 40, 30));
        for (int frame = 0; frame < 4; frame++)
            CheckMutation(root, window, surface, () => host.Clock.Advance(TimeSpan.FromMilliseconds(250)));
        Assert.Equal(SKColors.Beige, surface.Bitmap.GetPixel((int)(50 * scale), (int)(45 * scale)));
        Assert.Equal(SKColors.Crimson, surface.Bitmap.GetPixel((int)(245 * scale), (int)(175 * scale)));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void GridAndScrollablePanelRepaintRowsAndExposedViewport(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorPanel(SKColors.Navy);
        var grid = root.Controls.Add(new DataGridView { Bounds = new(200, 150, 250, 150) });
        grid.Columns.Add("Row", 130); grid.Columns.Add("Value", 150);
        for (int row = 0; row < 30; row++) grid.Rows.Add($"Unique row {row:00}", $"Value {row * 17}");
        var flow = root.Controls.Add(new FlowLayoutPanel { Bounds = new(10, 10, 160, 240), AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false });
        for (int row = 0; row < 12; row++) flow.Controls.Add(new Button { Size = new(120, 40), Text = $"Item {row}" });
        var window = host.Show(root, new TestViewport(480, 340, scale));
        window.LayoutUntilStable();
        using var surface = new PersistentSurface(window);
        surface.PaintFull();
        byte[] before = surface.Bitmap.Bytes;
        foreach (int row in new[] { 1, 6, 17, 3, 0 })
            CheckMutation(root, window, surface, () => grid.FirstDisplayedScrollingRowIndex = row);
        Assert.Equal(before, surface.Bitmap.Bytes);
        foreach (int value in new[] { 40, 110, 180, 20, 0 })
            CheckMutation(root, window, surface, () => flow.VerticalScrollProperties.Value = value);
        Assert.Equal(before, surface.Bitmap.Bytes);
    }

    [Fact]
    public void FiveKHoverKeepsBoundedDamageAndReusesFramebuffer()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorPanel(SKColors.Beige);
        var button = root.Controls.Add(new Button { Bounds = new(200, 150, 180, 40), Text = "Hover" });
        var window = host.Show(root, new TestViewport(2275, 1280, 2.25));
        window.Backend.Resize(new ModernFormsNext.WindowKit.Size(5120 / 2.25, 2880 / 2.25));
        using var surface = new PersistentSurface(window);
        surface.PaintFull();
        window.Backend.ConsumePendingInvalidations();
        window.Input.Move(button);
        var damage = Assert.IsType<Rect>(window.Backend.PendingInvalidationRegion);
        Assert.True(damage.Width * damage.Height < window.Backend.ClientSize.Width * window.Backend.ClientSize.Height / 100);
        using var profiler = PerformanceProfiler.Start();
        surface.Paint(damage);
        var frame = Assert.Single(profiler.Capture().Frames);
        Assert.Equal(PerformanceRedraw.PartialSurface, frame.RenderInfo.Redraw);
        Assert.Equal(0, frame.Work.SurfaceAllocations);
        Assert.Equal(0, frame.Work.LayoutPasses);
        Assert.Equal(5120, surface.Bitmap.Width);
        Assert.Equal(2880, surface.Bitmap.Height);
        AssertMatchesFull(root, surface);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ShadowRippleBorderAndFocusStayInsideTheirCompositedDamage(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorPanel(SKColors.Beige);
        var group = root.Controls.Add(new GroupBox { Bounds = new(20, 20, 160, 120), Text = "Shadow",
            ShowShadow = true, ShadowBlur = 5, ShadowOffset = new Point(4, 6) });
        var button = root.Controls.Add(new Button { Bounds = new(210, 150, 130, 45), Text = "Ripple",
            Ripple = new RippleEffect { Duration = TimeSpan.FromSeconds(1), Color = Color.FromArgb(140, 255, 0, 0) } });
        button.StyleFocused.Border.Width = 2;
        button.StyleFocused.Border.Color = SKColors.Blue;
        var window = host.Show(root, new TestViewport(400, 280, scale));
        using var surface = new PersistentSurface(window);
        surface.PaintFull();
        CheckMutation(root, window, surface, () => group.Location = new(50, 45));
        CheckMutation(root, window, surface, () => group.Size = new(120, 80));
        CheckMutation(root, window, surface, () => window.Input.Click(button));
        for (int frame = 0; frame < 4; frame++)
            CheckMutation(root, window, surface, () => host.Clock.Advance(TimeSpan.FromMilliseconds(250)));
        CheckMutation(root, window, surface, () => group.Visible = false);
    }

    private static void CheckMutation(Control root, TestWindowHost window, PersistentSurface surface, Action change)
    {
        window.Backend.ConsumePendingInvalidations();
        change();
        // Paint precisely the requested region into the same backing as the previous frame.
        // Comparing dirty rectangles alone cannot detect stale cached/presented pixels.
        if (window.Backend.PendingInvalidationRegion is { } damage)
            surface.Paint(damage);
        AssertMatchesFull(root, surface);
    }

    private static void AssertMatchesFull(Control root, PersistentSurface surface)
    {
        byte[] partial = surface.Bitmap.Bytes;
        Dirty(root);
        surface.PaintFull();
        Assert.Equal(surface.Bitmap.Bytes, partial);
    }

    private static void Dirty(Control control)
    {
        control.Invalidate();
        foreach (var child in control.Controls) Dirty(child);
    }

    private sealed class ColorPanel(SKColor color) : Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e) => e.Canvas.Clear(color);
    }
}
