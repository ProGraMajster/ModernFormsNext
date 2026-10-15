using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Renderers;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class VirtualizedListViewRenderingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalPaintCompositesEachItemImageOnce(bool virtualized)
    {
        using var root = new VisibleRoot();
        var view = root.Controls.Add(new PaintListView { Size = new(160, 120) });
        using var image = new SKBitmap(32, 32);
        image.Erase(new SKColor(255, 0, 0, 128));
        view.Items.Add("", image);
        view.VirtualizationEnabled = virtualized;
        using var pixels = new SKBitmap(160, 120);
        using var canvas = new SKCanvas(pixels);
        var paint = new PaintEventArgs(pixels.Info, canvas, 1);
        view.PaintForTest(paint);
        canvas.Clear(SKColors.Transparent);
        view.PaintForTest(paint);

        int painted = 0;
        for (int y = 0; y < pixels.Height; y++)
            for (int x = 0; x < pixels.Width; x++)
                if (pixels.GetPixel(x, y).Alpha is byte alpha && alpha > 0)
                {
                    painted++;
                    Assert.InRange(alpha, 1, 128);
                }
        Assert.True(painted > 0);
    }

    [Fact]
    public void StackTextIsVisibleInsideShortRowsAndRendererVisitsOnlyRealizedItems()
    {
        using var root = new VisibleRoot();
        var view = root.Controls.Add(new ListView { Size = new(240, 120), ItemSize = new(220, 32), ItemsLayout = ItemsLayout.Stack });
        view.SuspendLayout();
        for (int index = 0; index < 100_000; index++) view.Items.Add($"Entry {index}");
        view.VirtualizationEnabled = true;
        view.ResumeLayout();
        view.ScrollToItem(50_000);
        using var pixels = new SKBitmap(240, 120);
        using var canvas = new SKCanvas(pixels);
        canvas.Clear(SKColors.Transparent);
        var renderer = new CountingRenderer();
        renderer.Render((object)view, new PaintEventArgs(pixels.Info, canvas, 1));
        Assert.Equal(view.RealizedRange.Count, renderer.Visited);
        Assert.InRange(renderer.Visited, 1, 10);
        var bounds = Rectangle.Intersect(view.Items[50_000].Bounds, new Rectangle(0, 0, 240, 120));
        int painted = 0;
        for (int y = bounds.Top; y < bounds.Bottom; y++)
            for (int x = bounds.Left; x < bounds.Right; x++) if (pixels.GetPixel(x, y).Alpha > 0) painted++;
        Assert.True(painted > 0, "The short stack row must display its text, not place it below the cell.");
    }

    [Fact]
    public void EnablingVirtualizationDoesNotExposeLegacyBoundsForAnUnrealizedLogicalPeer()
    {
        using var root = new VisibleRoot();
        var view = root.Controls.Add(new PaintListView { Size = new(450, 450) });
        for (int index = 0; index < 40; index++) view.Items.Add("");
        using var pixels = new SKBitmap(450, 450);
        using var canvas = new SKCanvas(pixels);
        view.PaintForTest(new PaintEventArgs(pixels.Info, canvas, 1));
        var peer = view.AccessibilityObject.GetChild(20)!;
        Assert.False(peer.Bounds.IsEmpty);

        view.ItemsLayout = ItemsLayout.Stack;
        view.ItemSize = new(400, 60);
        view.VirtualizationEnabled = true;

        Assert.True(view.RealizedRange.EndIndex <= 20);
        Assert.Same(peer, view.AccessibilityObject.GetChild(20));
        Assert.True(peer.Bounds.IsEmpty);
        Assert.True((peer.State & AccessibleStates.Offscreen) != 0);
        Assert.True(peer.PerformAction(AccessibleActions.ScrollIntoView));
        Assert.False(peer.Bounds.IsEmpty);
        Assert.Equal(AccessibleStates.None, peer.State & AccessibleStates.Offscreen);
    }

    [Fact]
    public void InlineImageAndCustomRendererAreClippedToTheirSmallCell()
    {
        using var root = new VisibleRoot();
        var view = root.Controls.Add(new ListView { Size = new(120, 100), ItemSize = new(100, 20), ItemsLayout = ItemsLayout.Stack });
        using var image = new SKBitmap(32, 32);
        image.Erase(SKColors.Red);
        view.Items.Add("", image);
        view.VirtualizationEnabled = true;
        using var pixels = new SKBitmap(120, 100);
        using var canvas = new SKCanvas(pixels);
        canvas.Clear(SKColors.Transparent);
        new CountingRenderer().Render((object)view, new PaintEventArgs(pixels.Info, canvas, 1));
        int painted = 0;
        for (int y = 0; y < pixels.Height; y++)
            for (int x = 0; x < pixels.Width; x++)
                if (pixels.GetPixel(x, y).Alpha > 0) { painted++; Assert.True(view.Items[0].Bounds.Contains(x, y)); }
        Assert.True(painted > 0);
    }

    private sealed class PaintListView : ListView
    {
        internal void PaintForTest(PaintEventArgs e) => OnPaint(e);
    }

    private sealed class CountingRenderer : ListViewRenderer
    {
        internal int Visited;
        protected override void RenderItem(ListView view, ListViewItem item, PaintEventArgs e)
        { Visited++; base.RenderItem(view, item, e); }
    }

    private sealed class VisibleRoot : Control
    {
        public override bool Visible { get => true; set => base.Visible = value; }
    }
}
