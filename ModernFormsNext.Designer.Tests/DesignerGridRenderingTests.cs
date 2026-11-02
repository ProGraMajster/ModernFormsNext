using System.Drawing;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerGridRenderingTests
{
    [Theory]
    [InlineData(1d, 1000, 800)]
    [InlineData(1.5d, 440, 400)]
    [InlineData(2d, 440, 400)]
    public void GridDotsFollowTheCanonicalNodeCoordinateMappingAndCanBeHidden(double dpi, int width, int height)
    {
        using var session = new DesignerSession();
        session.LoadDocument(new DesignDocument { Size = new(800, 500) });
        var view = new DesignerCoordinateMapper().GetView(session, width, height);
        var point = new DesignerCoordinateMapper().ToSurfaceBounds(new DesignBounds(48, 48, 1, 1), view);
        var pixel = DesignerDpiCoordinateConverter.LogicalToDevice(point, dpi).Location;
        var info = new SKImageInfo((int)(width * dpi), (int)(height * dpi));
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        var args = new PaintEventArgs(info, canvas, dpi);
        session.SnappingOptions = new DesignerSnappingOptions { GridSize = 16 };
        new DesignerSurfaceRenderer().Render(args, session, width, height);
        Assert.Equal(new SKColor(225, 225, 225), bitmap.GetPixel(pixel.X, pixel.Y));
        session.SnappingOptions = new DesignerSnappingOptions { GridSize = 16, ShowGrid = false };
        new DesignerSurfaceRenderer().Render(args, session, width, height);
        Assert.Equal(new SKColor(245, 245, 245), bitmap.GetPixel(pixel.X, pixel.Y));
        Assert.False(session.IsDirty);
        Assert.False(session.Transactions.CanUndo);
    }

    [Theory]
    [InlineData(1, 0.01)]
    [InlineData(1, 2)]
    [InlineData(4096, 0.01)]
    public void GridDisplayAdmitsABoundedMultipleWithoutChangingConfiguredSpacing(int spacing, double scale)
    {
        var bounds = new Rectangle(0, 0, 8192, 8192);
        double step = DesignerGridPainter.GetDeviceStep(bounds, spacing, scale);
        Assert.True(step > 0 && double.IsFinite(step));
        Assert.True(Math.Ceiling(bounds.Width / step) * Math.Ceiling(bounds.Height / step) <= DesignerGridPainter.MaximumDots);
        Assert.Equal(Math.Round(step / (spacing * scale)), step / (spacing * scale), precision: 8);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void InvalidSpacingCannotReplaceSessionPreferencesOrDirtyTheDocument(int spacing)
    {
        using var session = new DesignerSession();
        var previous = session.SnappingOptions;
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SnappingOptions = new DesignerSnappingOptions { GridSize = spacing });
        Assert.Same(previous, session.SnappingOptions);
        Assert.False(session.IsDirty);
        Assert.False(session.Transactions.CanUndo);
    }
}
