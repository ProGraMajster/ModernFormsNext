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
        foreach (var handle in resizeHandles)
        {
            var rect = DesignerHitTestService.GetHandleBounds(bounds, handle);
            e.Canvas.FillRectangle(rect, SKColors.White);
            e.Canvas.DrawRectangle(rect, DesignerColors.Accent);
        }
    }
}
