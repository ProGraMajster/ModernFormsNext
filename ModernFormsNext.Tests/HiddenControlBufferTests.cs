using System.Drawing;
using ModernFormsNext.WindowKit.Controls;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(InputBindingCollectionTests.Name)]
public sealed class HiddenControlBufferTests
{
    [Fact]
    public void HidingAnAncestorReleasesBuffersAndShowingRepaintsTheSameControls()
    {
        using var root = new Control();
        using var page = new Control { Bounds = new Rectangle(0, 0, 80, 60) };
        using var child = new Control { Bounds = new Rectangle(4, 4, 30, 20), Text = "preserved" };
        child.BackColor = SKColors.Crimson;
        root.Controls.Add(page);
        page.Controls.Add(child);
        using var host = new SkiaControlSurface(root);
        host.Resize(80, 60);
        using var surface = SKSurface.Create(new SKImageInfo(80, 60));
        host.Render(surface.Canvas);
        var pageBuffer = page.GetBackBuffer();
        var childBuffer = child.GetBackBuffer();
        using var before = surface.Snapshot();
        page.Visible = false;
        Assert.Equal(IntPtr.Zero, pageBuffer.Handle);
        Assert.Equal(IntPtr.Zero, childBuffer.Handle);
        page.Visible = true;
        host.Render(surface.Canvas);
        using var after = surface.Snapshot();
        using var beforePixels = SKBitmap.FromImage(before);
        using var afterPixels = SKBitmap.FromImage(after);
        Assert.Equal(beforePixels.Bytes, afterPixels.Bytes);
        Assert.Equal("preserved", child.Text);
        Assert.Same(page, child.Parent);
        Assert.NotSame(pageBuffer, page.GetBackBuffer());
    }
}
