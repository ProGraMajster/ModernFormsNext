using System.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ControlZOrderTests
{
    [Fact]
    public void LastIndexReceivesPointerInputWhenSiblingsOverlap()
    {
        using var root = new VisibleRoot { Size = new Size(40, 40) };
        var back = new PaintProbe(SKColors.Red) { Bounds = new Rectangle(0, 0, 30, 30) };
        var front = new PaintProbe(SKColors.Blue) { Bounds = new Rectangle(0, 0, 30, 30) };
        root.Controls.AddRange(back, front);

        root.MouseDownForTest(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, Point.Empty));

        Assert.Equal(1, front.MouseDownCount);
        Assert.Equal(0, back.MouseDownCount);
    }

    [Fact]
    public void LastIndexIsPaintedOnTopWhenSiblingsOverlap()
    {
        using var root = new VisibleRoot { Size = new Size(40, 40) };
        var back = new PaintProbe(SKColors.Red) { Bounds = new Rectangle(0, 0, 30, 30) };
        var front = new PaintProbe(SKColors.Blue) { Bounds = new Rectangle(0, 0, 30, 30) };
        root.Controls.AddRange(back, front);
        using var bitmap = new SKBitmap(40, 40);
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);

        root.PaintForTest(new PaintEventArgs(bitmap.Info, canvas, 1d));
        canvas.Flush();

        Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void BringToFrontAndSendToBackUpdatePointerAndPaintOrder()
    {
        using var root = new VisibleRoot { Size = new Size(40, 40) };
        var first = new PaintProbe(SKColors.Red) { Bounds = new Rectangle(0, 0, 30, 30) };
        var second = new PaintProbe(SKColors.Blue) { Bounds = new Rectangle(0, 0, 30, 30) };
        root.Controls.AddRange(first, second);

        first.BringToFront();
        root.MouseDownForTest(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, Point.Empty));
        Assert.Equal(1, first.MouseDownCount);
        Assert.Equal(0, second.MouseDownCount);
        Assert.Equal(SKColors.Red, PaintPixel(root));

        first.SendToBack();
        root.MouseDownForTest(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, Point.Empty));
        Assert.Equal(1, first.MouseDownCount);
        Assert.Equal(1, second.MouseDownCount);
        Assert.Equal(SKColors.Blue, PaintPixel(root));
    }

    [Fact]
    public void SkiaControlSurfaceUsesTheSameFrontMostHitTarget()
    {
        using var root = new VisibleRoot { Size = new Size(40, 40) };
        var back = new PaintProbe(SKColors.Red) { Bounds = new Rectangle(0, 0, 30, 30) };
        var front = new PaintProbe(SKColors.Blue) { Bounds = new Rectangle(0, 0, 30, 30) };
        root.Controls.AddRange(back, front);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(40, 40);

        surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 10, 10);
        surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 10, 10);

        Assert.Equal(1, front.MouseDownCount);
        Assert.Equal(0, back.MouseDownCount);
    }

    private static SKColor PaintPixel(VisibleRoot root)
    {
        using var bitmap = new SKBitmap(40, 40);
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);
        root.PaintForTest(new PaintEventArgs(bitmap.Info, canvas, 1d));
        canvas.Flush();
        return bitmap.GetPixel(10, 10);
    }

    private sealed class VisibleRoot : Control
    {
        public override bool Visible
        {
            get => true;
            set => base.Visible = value;
        }

        public void MouseDownForTest(MouseEventArgs e) => RaiseMouseDown(e);

        public void PaintForTest(PaintEventArgs e) => RaisePaint(e);
    }

    private sealed class PaintProbe(SKColor color) : Control
    {
        public int MouseDownCount { get; private set; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            MouseDownCount++;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Canvas.Clear(color);
        }
    }
}
