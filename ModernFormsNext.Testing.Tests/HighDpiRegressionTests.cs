using System.Drawing;
using System.Reflection;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit.Controls.Platform.Surfaces;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;
using Xunit;
using Rect = ModernFormsNext.WindowKit.Rect;

namespace ModernFormsNext.Testing.Tests;

public sealed class HighDpiRegressionTests
{
    public static TheoryData<double> Scales => new() { 1, 1.25, 1.5, 1.75, 2, 2.25, 2.5 };

    [Theory]
    [MemberData(nameof(Scales))]
    public void LogicalClientLayoutTextSpacingInputAndFramebufferAgree(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var content = root.Controls.Add(new Panel { Dock = DockStyle.Fill });
        var sidebar = root.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 236 });
        var button = sidebar.Controls.Add(new Button { Bounds = new Rectangle(18, 18, 192, 46),
            Padding = new Padding(48, 0, 10, 0), Text = "Downloads", TextAlign = ContentAlignment.MiddleLeft });
        using var image = new SKBitmap(16, 16);
        image.Erase(SKColors.Crimson);
        var imageButton = sidebar.Controls.Add(new Button { Bounds = new Rectangle(18, 85, 192, 46), Text = "Image spacing",
            Image = image, ImageAlign = ContentAlignment.MiddleLeft, TextAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText });
        var icon = button.Controls.Add(new Control { Bounds = new Rectangle(15, 11, 24, 24) });
        var card = content.Controls.Add(new Panel { Size = new Size(660, 380) });
        content.Layout += (_, _) => card.Location = new Point((content.ClientSize.Width - card.Width) / 2,
            (content.ClientSize.Height - card.Height) / 2);
        var window = host.Show(root, new TestViewport(1200, 700, scale));
        window.LayoutUntilStable();
        Assert.Equal(new Size(1200, 700), root.ClientSize);
        Assert.Equal(new Rectangle(236, 0, 964, 700), content.Bounds);
        Assert.Equal(new Point(152, 160), card.Location);
        Assert.True(new Rectangle(Point.Empty, content.ClientSize).Contains(card.Bounds));
        var text = typeof(Control).Assembly.GetType("ModernFormsNext.Layout.TextImageLayoutEngine")!
            .GetMethod("Layout")!.Invoke(null, [button])!;
        var textBounds = (Rectangle)text.GetType().GetField("TextBounds")!.GetValue(text)!;
        Assert.True(textBounds.Left > icon.ScaledBounds.Right);
        Assert.Equal(button.LogicalToDeviceUnits(48), button.PaddedClientRectangle.Left - button.ClientRectangle.Left);
        var imageLayout = typeof(Control).Assembly.GetType("ModernFormsNext.Layout.TextImageLayoutEngine")!
            .GetMethod("Layout")!.Invoke(null, [imageButton])!;
        var imageBounds = (Rectangle)imageLayout.GetType().GetField("ImageBounds")!.GetValue(imageLayout)!;
        var imageTextBounds = (Rectangle)imageLayout.GetType().GetField("TextBounds")!.GetValue(imageLayout)!;
        Assert.Equal((int)(16 * scale), imageBounds.Width);
        Assert.True(imageBounds.Right <= imageTextBounds.Left);
        bool clicked = false;
        button.Click += (_, _) => clicked = true;
        window.Input.Move(button);
        Assert.True(button.IsHovering);
        window.Input.Click(button);
        Assert.True(clicked);
        using var pixels = window.CaptureRenderedSnapshot();
        Assert.Equal((int)(1200 * scale), pixels.PixelWidth);
        Assert.Equal((int)(700 * scale), pixels.PixelHeight);
        Assert.Equal(pixels.PixelWidth * 4, pixels.RowBytes);
        window.Resize(1400, 800);
        Assert.Equal(new Point(252, 210), card.Location);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void LocalDamageMatchesFullPaintWithOverlapAndRetainedAncestorBuffers(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorPanel(SKColors.Navy);
        var parent = root.Controls.Add(new ColorPanel(SKColors.Beige) { Bounds = new Rectangle(10, 10, 350, 250) });
        var changing = parent.Controls.Add(new ColorPanel(SKColors.Red) { Bounds = new Rectangle(30, 20, 80, 60) });
        parent.Controls.Add(new ColorPanel(new SKColor(0, 0, 255, 120)) { Bounds = new Rectangle(65, 45, 70, 50) });
        var untouched = parent.Controls.Add(new ColorPanel(SKColors.Yellow) { Bounds = new Rectangle(220, 150, 60, 40) });
        var window = host.Show(root, new TestViewport(400, 300, scale));
        window.LayoutUntilStable();
        using var surface = new PersistentSurface(window);
        surface.PaintFull();
        int rootPaints = root.PaintCalls;
        surface.PaintFull();
        Assert.Equal(rootPaints, root.PaintCalls); // Hidden implicit scrollbars must not keep ancestors dirty.
        window.Backend.ConsumePendingInvalidations();
        int prior = untouched.PaintCalls;
        changing.Color = SKColors.Green;
        changing.Invalidate();
        var damage = Assert.IsType<Rect>(window.Backend.PendingInvalidationRegion);
        Assert.True(damage.Width * damage.Height < 400 * 300 / 10);
        using var profiler = PerformanceProfiler.Start();
        surface.Paint(damage);
        var frame = Assert.Single(profiler.Capture().Frames);
        Assert.Equal(PerformanceRedraw.PartialSurface, frame.RenderInfo.Redraw);
        Assert.Equal(0, frame.Work.SurfaceAllocations);
        Assert.Equal(0, frame.Work.LayoutPasses);
        Assert.Equal(prior, untouched.PaintCalls);
        var partial = surface.Bitmap.Bytes;
        InvalidateTree(root);
        surface.PaintFull();
        Assert.Equal(surface.Bitmap.Bytes, partial);
        // Old pixels must also be removed when the control moves outside the parent,
        // becomes hidden, or a render transform returns to identity.
        foreach (Action mutation in new Action[] { () => changing.Rotation = 25,
            () => changing.Rotation = 0, () => changing.Left = 600,
            () => changing.Left = 30, () => changing.Visible = false, () => changing.Visible = true }) {
            window.Backend.ConsumePendingInvalidations();
            mutation();
            surface.Paint(Assert.IsType<Rect>(window.Backend.PendingInvalidationRegion));
            partial = surface.Bitmap.Bytes;
            InvalidateTree(root);
            surface.PaintFull();
            Assert.Equal(surface.Bitmap.Bytes, partial);
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void AnchorFlowAutoSizeScrollCaptureAndDpiTransitionPreserveLogicalLayout(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel { Size = new Size(400, 300) };
        var anchored = root.Controls.Add(new Button { Bounds = new Rectangle(280, 260, 100, 30), Anchor = AnchorStyles.Right | AnchorStyles.Bottom });
        var flow = root.Controls.Add(new FlowLayoutPanel { Bounds = new Rectangle(10, 10, 220, 140), AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false });
        var label = flow.Controls.Add(new Label { Size = new Size(140, 24), Text = "Logical label" });
        for (int index = 0; index < 8; index++) flow.Controls.Add(new Button { Size = new Size(170, 35), Text = $"Item {index}" });
        var autoSize = root.Controls.Add(new Panel { AutoSize = true, Bounds = new Rectangle(250, 60, 120, 40) });
        var autoChild = autoSize.Controls.Add(new Label { Size = new Size(130, 24), Margin = new Padding(0), Text = "Growing content" });
        var text = root.Controls.Add(new TextBox { Bounds = new Rectangle(10, 170, 180, 34), Text = "DPI text cache" });
        var window = host.Show(root, new TestViewport(400, 300, 1));
        window.LayoutUntilStable();
        Assert.Equal(130, autoSize.Width);
        var labelSize = label.Size;
        var itemBounds = flow.Controls[1].Bounds;
        using var before = window.CaptureRenderedSnapshot();
        window.SetRenderScale(scale);
        window.LayoutUntilStable();
        Assert.Equal(new Size(400, 300), root.ClientSize);
        Assert.InRange(Math.Abs(label.Width - labelSize.Width), 0, 2); // Font hinting may round one logical pixel differently.
        Assert.Equal(itemBounds.Size, flow.Controls[1].Size);
        Assert.Equal(130, autoSize.Width);
        autoChild.Width = 180;
        window.LayoutUntilStable();
        Assert.Equal(180, autoSize.Width);
        window.Resize(500, 400);
        Assert.Equal(new Rectangle(380, 360, 100, 30), anchored.Bounds);
        int scroll = flow.VerticalScrollProperties.Value;
        window.Input.Wheel(new Point(30, 40), new Point(0, -1));
        Assert.True(flow.VerticalScrollProperties.Value > scroll);
        window.Input.Move(anchored);
        window.Input.PointerDown(new Point(400, 370));
        Assert.True(anchored.Capture);
        window.Input.Move(new Point(520, 410));
        window.Input.PointerUp(new Point(520, 410));
        Assert.False(anchored.Capture);
        using var after = window.CaptureRenderedSnapshot();
        Assert.Equal((int)(500 * scale), after.PixelWidth);
        window.SetRenderScale(1);
        window.LayoutUntilStable();
        Assert.Equal(labelSize, label.Size);
        Assert.Equal(180, autoSize.Width);
        Assert.Equal("DPI text cache", text.Text);
        window.Input.Click(anchored);
    }

    [Fact]
    public void FiveKEquivalentUsesExactPhysicalBufferAt225Percent()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new Panel();
        var window = host.Show(root, new TestViewport(2275, 1280, 2.25));
        window.Backend.Resize(new ModernFormsNext.WindowKit.Size(5120 / 2.25, 2880 / 2.25));
        using var snapshot = window.Backend.CaptureRenderedSnapshot(16_777_216);
        Assert.Equal(5120, snapshot.PixelWidth);
        Assert.Equal(2880, snapshot.PixelHeight);
        Assert.Equal(20480, snapshot.RowBytes);
        Assert.Equal(58_982_400, (long)snapshot.RowBytes * snapshot.PixelHeight);
    }

    private static void InvalidateTree(Control control)
    {
        control.Invalidate();
        foreach (var child in control.Controls) InvalidateTree(child);
    }

    private sealed class ColorPanel(SKColor color) : Panel
    {
        internal SKColor Color = color;
        internal int PaintCalls;
        protected override void OnPaintBackground(PaintEventArgs e) { PaintCalls++; e.Canvas.Clear(Color); }
    }

    // TestHost's public snapshots intentionally allocate fresh backing. This test instead
    // lends one persistent surface to its existing production Paint callback to verify that
    // damaged pixels can change without losing pixels outside the native-like dirty region.
    private sealed class PersistentSurface : IFramebufferPlatformSurface, IDisposable
    {
        private static readonly FieldInfo SurfaceField = typeof(HeadlessWindowImpl).GetField("renderingSurfaces", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly TestWindowHost window;
        internal SKBitmap Bitmap { get; }
        internal PersistentSurface(TestWindowHost window)
        {
            this.window = window;
            Bitmap = new SKBitmap((int)(window.Backend.ClientSize.Width * window.Backend.RenderScaling),
                (int)(window.Backend.ClientSize.Height * window.Backend.RenderScaling), SKColorType.Bgra8888, SKAlphaType.Premul);
            SurfaceField.SetValue(window.Backend, new object[] { this });
        }
        public ILockedFramebuffer Lock() => new LockedFramebuffer(Bitmap.GetPixels(),
            new ModernFormsNext.WindowKit.PixelSize(Bitmap.Width, Bitmap.Height), Bitmap.RowBytes,
            new ModernFormsNext.WindowKit.Vector(96 * window.Backend.RenderScaling, 96 * window.Backend.RenderScaling),
            PixelFormat.Bgra8888, () => { });
        internal void Paint(Rect damage) => window.Backend.Paint!(damage);
        internal void PaintFull() => Paint(new Rect(window.Backend.ClientSize));
        public void Dispose() { SurfaceField.SetValue(window.Backend, Array.Empty<object>()); Bitmap.Dispose(); }
    }
}
