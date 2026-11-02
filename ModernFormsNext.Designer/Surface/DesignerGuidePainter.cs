using System.Drawing;
using System.Globalization;
using ModernFormsNext.Designer.Layout;
using SkiaSharp;

namespace ModernFormsNext.Designer.Surface;

internal static class DesignerGuidePainter
{
    internal static void Draw(PaintEventArgs e, DesignerSurfaceView view, IReadOnlyList<DesignerSnapGuide> guides)
    {
        using var paint = new SKPaint { Color = DesignerColors.Accent, StrokeWidth = Math.Max(1, e.LogicalToDeviceUnits(1)), IsAntialias = false };
        var visible = e.Canvas.DeviceClipBounds;
        if (visible.Width <= 0 || visible.Height <= 0) return;
        float minimumX = visible.Left + paint.StrokeWidth / 2, minimumY = visible.Top + paint.StrokeWidth / 2;
        float maximumX = Math.Max(minimumX, visible.Right - paint.StrokeWidth / 2);
        float maximumY = Math.Max(minimumY, visible.Bottom - paint.StrokeWidth / 2);
        float X(double coordinate) => (float)Math.Round((view.ClientX + Math.Round(coordinate * view.Scale)) * e.Scaling);
        float Y(double coordinate) => (float)Math.Round((view.ClientY + Math.Round(coordinate * view.Scale)) * e.Scaling);
        // Only winning axis candidates contribute overlays. Never paint or allocate by document
        // size or by the number of sibling targets; current contexts expose at most six segments.
        for (int index = 0; index < Math.Min(6, guides.Count); index++)
        {
            var guide = guides[index];
            float x1 = guide.Vertical ? X(guide.Coordinate) : X(guide.From);
            float y1 = guide.Vertical ? Y(guide.From) : Y(guide.Coordinate);
            float x2 = guide.Vertical ? x1 : X(guide.To);
            float y2 = guide.Vertical ? Y(guide.To) : y1;
            // Boundary coordinates can equal the exclusive right/bottom client edge. Inset
            // only the rasterized stroke; authored snap coordinates remain exact.
            x1 = Math.Clamp(x1, minimumX, maximumX); x2 = Math.Clamp(x2, minimumX, maximumX);
            y1 = Math.Clamp(y1, minimumY, maximumY); y2 = Math.Clamp(y2, minimumY, maximumY);
            e.Canvas.DrawLine(x1, y1, x2, y2, paint);
            if (guide.Gap is not { } gap) continue;
            float tick = e.LogicalToDeviceUnits(3);
            if (guide.Vertical)
            {
                e.Canvas.DrawLine(x1 - tick, y1, x1 + tick, y1, paint);
                e.Canvas.DrawLine(x2 - tick, y2, x2 + tick, y2, paint);
            }
            else
            {
                e.Canvas.DrawLine(x1, y1 - tick, x1, y1 + tick, paint);
                e.Canvas.DrawLine(x2, y2 - tick, x2, y2 + tick, paint);
            }
            var clip = e.Canvas.DeviceClipBounds;
            var label = GetLabelBounds(new Rectangle(clip.Left, clip.Top, clip.Width, clip.Height),
                new Point((int)((x1 + x2) / 2), (int)((y1 + y2) / 2)),
                e.LogicalToDeviceUnits(70), e.LogicalToDeviceUnits(18), e.LogicalToDeviceUnits(6));
            e.Canvas.DrawText(gap.ToString("0.##", CultureInfo.InvariantCulture), Theme.UIFont,
                e.LogicalToDeviceUnits(11), label, DesignerColors.Accent, ContentAlignment.MiddleLeft, maxLines: 1);
        }
    }

    internal static Rectangle GetLabelBounds(Rectangle clip, Point center, int width, int height, int gap)
    {
        width = Math.Min(Math.Max(0, width), Math.Max(0, clip.Width));
        height = Math.Min(Math.Max(0, height), Math.Max(0, clip.Height));
        int top = center.Y - height - gap;
        if (top < clip.Top) top = center.Y + gap;
        return new(Math.Clamp(center.X, clip.Left, Math.Max(clip.Left, clip.Right - width)),
            Math.Clamp(top, clip.Top, Math.Max(clip.Top, clip.Bottom - height)), width, height);
    }
}
