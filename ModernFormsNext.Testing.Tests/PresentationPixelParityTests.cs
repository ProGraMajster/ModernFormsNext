using System.Drawing;
using ModernFormsNext.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class PresentationPixelParityTests
{
    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void NestedUntransformedGeometryMatchesRasterOriginAndHitTesting(double scale)
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red);
        var parent = new ColorControl(SKColors.Blue) { Bounds = new Rectangle(3, 3, 30, 30) };
        var child = new ColorControl(SKColors.Lime) { Bounds = new Rectangle(1, 1, 11, 11) };
        parent.Controls.Add(child);
        root.Controls.Add(parent);
        var window = host.Show(root, new TestViewport(50, 50, scale));
        using var snapshot = window.CaptureRenderedSnapshot();
        Point rootOrigin = root.PointToScreen(Point.Empty);
        Point childOrigin = child.PointToScreen(Point.Empty);
        int start = (int)(3 * scale) + (int)scale;
        // The existing buffer uses rounded absolute edges, not a separately rounded width.
        int size = child.ScaledBounds.Width;

        Assert.Equal(new Point(rootOrigin.X + start, rootOrigin.Y + start), childOrigin);
        Assert.Equal(new Rectangle(childOrigin, new Size(size, size)), child.AccessibilityObject.Bounds);
        Assert.Equal(SKColors.Lime, snapshot.GetPixel(start, start));
        Assert.Equal(SKColors.Blue, snapshot.GetPixel(start - 1, start));
        Assert.Equal(SKColors.Lime, snapshot.GetPixel(start + size - 1, start + size - 1));
        Assert.Equal(SKColors.Blue, snapshot.GetPixel(start + size, start));
        Assert.Same(child.AccessibilityObject, root.AccessibilityObject.HitTest(childOrigin.X, childOrigin.Y));
        Assert.NotSame(child.AccessibilityObject, root.AccessibilityObject.HitTest(childOrigin.X - 1, childOrigin.Y));
    }

    [Fact]
    public void RenderTranslationStillUsesTransformedGeometryAndInput()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new ColorControl(SKColors.Red);
        var child = new ColorControl(SKColors.Lime)
        {
            Bounds = new Rectangle(3, 3, 11, 11), TranslationX = 4, TranslationY = 5
        };
        root.Controls.Add(child);
        var window = host.Show(root, 50, 50);
        using var snapshot = window.CaptureRenderedSnapshot();
        Point origin = root.PointToScreen(Point.Empty);
        Assert.Equal(new Point(origin.X + 7, origin.Y + 8), child.PointToScreen(Point.Empty));
        Assert.Equal(SKColors.Lime, snapshot.GetPixel(8, 9));
        Assert.Equal(SKColors.Red, snapshot.GetPixel(3, 3));
        Assert.Same(child.AccessibilityObject, root.AccessibilityObject.HitTest(origin.X + 8, origin.Y + 9));
    }

    private sealed class ColorControl(SKColor color) : Control
    {
        protected override void OnPaintBackground(PaintEventArgs e) => e.Canvas.Clear(color);
    }
}
