using System.Drawing;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerGuideRenderingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContainerEdgeGuidesRemainVisibleInsideTheDesignClient(bool right)
    {
        using var session = new DesignerSession();
        session.LoadDocument(new DesignDocument { Size = new(800, 500) });
        var first = session.Document.Controls.AddNode("Button", "moving", new(20, 100, 100, 30));
        first.Properties["Margin"] = ModernFormsNext.Designer.Properties.DesignerPropertyValueEditor.ToDesignPropertyValue(Padding.Empty, typeof(Padding));
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        context.Move(right ? 675 : -15, 0, 1, false);
        var info = new SKImageInfo(1000, 800);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        new DesignerSurfaceRenderer().Render(new PaintEventArgs(info, canvas, 1), session, 1000, 800, guides: context.Guides);
        var view = new DesignerCoordinateMapper().GetView(session, 1000, 800);
        int x = right ? view.ClientX + view.ClientWidth - 1 : view.ClientX;
        Assert.Equal(DesignerColors.Accent, bitmap.GetPixel(x, view.ClientY + 50));
    }

    [Theory]
    [InlineData(1d, 1000, 800)]
    [InlineData(1.5d, 440, 400)]
    [InlineData(2d, 440, 400)]
    public void WinningEdgeIsPaintedAtTheSameDpiAndZoomBoundaryAsTheControl(double dpi, int width, int height)
    {
        using var session = new DesignerSession();
        session.LoadDocument(new DesignDocument { Size = new(800, 500) });
        var first = session.Document.Controls.AddNode("Button", "moving", new(10, 20, 100, 30));
        session.Document.Controls.AddNode("Button", "target", new(200, 120, 100, 30));
        session.SelectNode(first);
        var mapper = new DesignerCoordinateMapper();
        var view = mapper.GetView(session, width, height);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        var delta = context.Move(185, 0, view.Scale, false);
        first.Bounds = first.Bounds with { X = (int)(first.Bounds.X + delta.X) };
        var info = new SKImageInfo((int)(width * dpi), (int)(height * dpi));
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        new DesignerSurfaceRenderer().Render(new PaintEventArgs(info, canvas, dpi), session, width, height, guides: context.Guides);
        var probe = mapper.ToSurfaceBounds(new DesignBounds(200, 80, 1, 1), view);
        var point = DesignerDpiCoordinateConverter.LogicalToDevice(probe, dpi).Location;
        Assert.Equal(DesignerColors.Accent, bitmap.GetPixel(point.X, point.Y));
    }

    [Theory]
    [InlineData(110, 100)]
    [InlineData(490, 100)]
    [InlineData(110, 398)]
    [InlineData(1000, -100)]
    public void GapCaptionsStayInsideTheVisibleDesignClient(int x, int y)
    {
        var clip = new Rectangle(100, 100, 400, 300);
        var bounds = DesignerGuidePainter.GetLabelBounds(clip, new Point(x, y), 70, 18, 6);
        Assert.True(clip.Contains(bounds));
    }
}
