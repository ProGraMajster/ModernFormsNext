using System.Drawing;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerDpiCoordinateConverterTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    public void DevicePanelPointerMapsToContainingLogicalPixel(double dpiScale)
    {
        const int logicalX = 64;
        const int logicalY = 72;
        var deviceX = DesignerDpiCoordinateConverter.LogicalToDevice(logicalX, dpiScale);
        var deviceY = DesignerDpiCoordinateConverter.LogicalToDevice(logicalY, dpiScale);

        var logicalPoint = DesignerDpiCoordinateConverter.DeviceToLogicalPoint(deviceX, deviceY, dpiScale);

        Assert.Equal(new Point(logicalX, logicalY), logicalPoint);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    public void PointerAndControlBoundsShareOneDpiBoundary(double dpiScale)
    {
        var mapper = new DesignerCoordinateMapper();
        var view = new DesignerSurfaceView(
            Scale: 0.8f,
            FormX: 13,
            FormY: 21,
            TitleHeight: 28,
            Border: 3,
            ClientWidth: 720,
            ClientHeight: 480);
        var designBounds = new DesignBounds(40, 50, 120, 40);

        var logicalSurfaceBounds = mapper.ToSurfaceBounds(designBounds, view);
        var deviceBounds = DesignerDpiCoordinateConverter.LogicalToDevice(logicalSurfaceBounds, dpiScale);
        var deviceCenterX = DesignerDpiCoordinateConverter.LogicalToDevice(logicalSurfaceBounds.Left + (logicalSurfaceBounds.Width / 2), dpiScale);
        var deviceCenterY = DesignerDpiCoordinateConverter.LogicalToDevice(logicalSurfaceBounds.Top + (logicalSurfaceBounds.Height / 2), dpiScale);
        var logicalPointer = DesignerDpiCoordinateConverter.DeviceToLogical(deviceCenterX, deviceCenterY, dpiScale);
        var documentPointer = mapper.MapToDocument(view, logicalPointer.X, logicalPointer.Y);

        Assert.True(deviceBounds.Width > 0);
        Assert.True(deviceBounds.Height > 0);
        Assert.Equal(new DesignPoint(100, 70), documentPointer);
        Assert.True(designBounds.Contains(documentPointer.X, documentPointer.Y));
        Assert.Equal(new DesignBounds(40, 50, 120, 40), designBounds);

        var logicalHandle = DesignerHitTestService.GetHandleBounds(logicalSurfaceBounds, DesignerResizeHandle.BottomRight);
        var deviceHandleSize = DesignerDpiCoordinateConverter.LogicalToDevice(DesignerHitTestService.ResizeHandleSize, dpiScale);
        var deviceHandle = DesignerHitTestService.GetHandleBounds(deviceBounds, DesignerResizeHandle.BottomRight, deviceHandleSize);

        Assert.Equal(logicalSurfaceBounds.Right, logicalHandle.Left + (logicalHandle.Width / 2));
        Assert.Equal(deviceBounds.Right, deviceHandle.Left + (deviceHandle.Width / 2));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    public void RepeatedDeviceRoundTripsDoNotAccumulateScale(double dpiScale)
    {
        const int logicalX = 96;
        const int logicalY = 108;
        var deviceX = DesignerDpiCoordinateConverter.LogicalToDevice(logicalX, dpiScale);
        var deviceY = DesignerDpiCoordinateConverter.LogicalToDevice(logicalY, dpiScale);

        for (var index = 0; index < 10; index++)
        {
            var logical = DesignerDpiCoordinateConverter.DeviceToLogical(deviceX, deviceY, dpiScale);
            deviceX = DesignerDpiCoordinateConverter.LogicalToDevice((int)logical.X, dpiScale);
            deviceY = DesignerDpiCoordinateConverter.LogicalToDevice((int)logical.Y, dpiScale);
        }

        Assert.Equal(DesignerDpiCoordinateConverter.LogicalToDevice(logicalX, dpiScale), deviceX);
        Assert.Equal(DesignerDpiCoordinateConverter.LogicalToDevice(logicalY, dpiScale), deviceY);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    public void DevicePointerMovementProducesLogicalDragDelta(double dpiScale)
    {
        var deviceStartX = DesignerDpiCoordinateConverter.LogicalToDevice(96, dpiScale);
        var deviceStartY = DesignerDpiCoordinateConverter.LogicalToDevice(108, dpiScale);
        var deviceEndX = DesignerDpiCoordinateConverter.LogicalToDevice(128, dpiScale);
        var deviceEndY = DesignerDpiCoordinateConverter.LogicalToDevice(132, dpiScale);

        var logicalStart = DesignerDpiCoordinateConverter.DeviceToLogical(deviceStartX, deviceStartY, dpiScale);
        var logicalEnd = DesignerDpiCoordinateConverter.DeviceToLogical(deviceEndX, deviceEndY, dpiScale);

        Assert.Equal(32f, logicalEnd.X - logicalStart.X);
        Assert.Equal(24f, logicalEnd.Y - logicalStart.Y);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    public void AdjacentLogicalRectanglesKeepOneRoundedDeviceEdge(double dpiScale)
    {
        var left = DesignerDpiCoordinateConverter.LogicalToDevice(new Rectangle(1, 2, 7, 5), dpiScale);
        var right = DesignerDpiCoordinateConverter.LogicalToDevice(new Rectangle(8, 2, 9, 5), dpiScale);

        Assert.Equal(left.Right, right.Left);
    }
}
