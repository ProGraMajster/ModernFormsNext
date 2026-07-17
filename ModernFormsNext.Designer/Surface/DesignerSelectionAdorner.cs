using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using SkiaSharp;

namespace ModernFormsNext.Designer.Surface;

internal static class DesignerSelectionAdorner
{
    public static void Draw(PaintEventArgs e, System.Drawing.Rectangle bounds, bool showResizeHandle = true)
        => Draw(e, bounds, showResizeHandle ? DesignerHitTestService.GetHandles() : []);

    public static void Draw(PaintEventArgs e, System.Drawing.Rectangle bounds, IEnumerable<DesignerResizeHandle> resizeHandles)
    {
        using var paint = new SKPaint
        {
            Color = DesignerColors.Accent,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = e.LogicalToDeviceUnits(1)
        };

        e.Canvas.DrawRect(bounds.ToSKRect(), paint);
        DrawResizeHandles(e, bounds, resizeHandles);
    }

    private static void DrawResizeHandles(PaintEventArgs e, System.Drawing.Rectangle bounds, IEnumerable<DesignerResizeHandle> resizeHandles)
    {
        var handleSize = Math.Max(1, e.LogicalToDeviceUnits(DesignerHitTestService.ResizeHandleSize));

        foreach (var handle in resizeHandles)
        {
            // Bounds are device pixels during rendering, so the logical handle size must cross
            // the same DPI boundary as the selection rectangle. Hit testing uses the unscaled
            // logical size after pointer coordinates have been converted back to logical units.
            var rect = DesignerHitTestService.GetHandleBounds(bounds, handle, handleSize);
            e.Canvas.FillRectangle(rect, SKColors.White);
            e.Canvas.DrawRectangle(rect, DesignerColors.Accent);
        }
    }
}
