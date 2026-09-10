using System.Drawing;
using ModernFormsNext.WindowKit;
using SkiaSharp;
using Xunit;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;

namespace ModernFormsNext.Tests;

public sealed class SkiaControlSurfaceInsetsTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void InsetsRejectInvalidSides(double side)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowInsets(new Thickness(side, 0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowInsets(default, new Thickness(0, 0, 0, side)));
    }

    [Fact]
    public void SafeAreaUsesExistingRootAndPreservesPaddingAndDockedChildren()
    {
        using var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(3, 4, 5, 6) };
        var child = new Panel { Dock = DockStyle.Fill };
        root.Controls.Add(child);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(200, 120);
        surface.Insets = new WindowInsets(new Thickness(10.1, 20, 8, 6));
        Assert.Equal(new Rectangle(11, 20, 181, 94), root.Bounds);
        Assert.Equal(new Padding(3, 4, 5, 6), root.Padding);
        Assert.Equal(root.DisplayRectangle, child.Bounds);
        surface.Resize(100, 80);
        Assert.Equal(new Rectangle(11, 20, 81, 54), root.Bounds);
        surface.Insets = default;
        Assert.Equal(new Rectangle(0, 0, 100, 80), root.Bounds);
        Assert.Same(root, surface.Root);
    }

    [Fact]
    public void OversizedInsetsClampWithoutOverflowAndKeyboardDoesNotResizeContent()
    {
        using var root = new Panel();
        using var surface = new SkiaControlSurface(root);
        surface.Insets = new WindowInsets(new Thickness(double.MaxValue));
        surface.Resize(100, 80);
        Assert.Equal(new Rectangle(100, 80, 0, 0), root.Bounds);
        surface.Insets = new WindowInsets(default, new Thickness(0, 0, 0, 60));
        Assert.Equal(new Rectangle(0, 0, 100, 80), root.Bounds);
        Assert.Equal(60, surface.Insets.Ime.Bottom);
        Assert.Equal(new Size(100, 80), surface.LogicalSize);
    }

    [Fact]
    public void InsetLayoutPaintAndPointerShareSurfaceCoordinates()
    {
        using var root = new PaintedRoot();
        var button = new Button { Bounds = new Rectangle(5, 5, 40, 30), Text = "Go" };
        root.Controls.Add(button);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(140, 100);
        surface.Insets = new WindowInsets(new Thickness(20, 30, 10, 10));
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        surface.ProcessPointer(ControlSurfacePointerAction.Down, 30, 40);
        surface.ProcessPointer(ControlSurfacePointerAction.Up, 30, 40);
        Assert.Equal(1, clicks);
        surface.ProcessPointer(ControlSurfacePointerAction.Down, 10, 10);
        surface.ProcessPointer(ControlSurfacePointerAction.Up, 10, 10);
        Assert.Equal(1, clicks);
        using var bitmap = new SKBitmap(140, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        surface.Render(canvas);
        Assert.Equal(SKColors.Crimson, bitmap.GetPixel(100, 80));
        Assert.NotEqual(SKColors.Crimson, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void InsetsAreDeduplicatedAndCannotMutateDisposedSurface()
    {
        using var root = new Panel();
        var surface = new SkiaControlSurface(root);
        surface.Resize(100, 80);
        var calls = 0;
        surface.InsetsChanged += (_, args) =>
        {
            calls++;
            Assert.Equal(args.Insets, surface.Insets);
            Assert.Equal(10, root.Left);
        };
        var inset = new WindowInsets(new Thickness(10, 0, 0, 0));
        surface.Insets = inset;
        surface.Insets = inset;
        Assert.Equal(1, calls);
        surface.Dispose();
        Assert.Throws<ObjectDisposedException>(() => surface.Insets = default);
        Assert.False(root.IsDisposed);
    }

    private sealed class PaintedRoot : Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e) => e.Canvas.Clear(SKColors.Crimson);
    }
}
