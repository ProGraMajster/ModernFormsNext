using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Designer.Surface;

internal static class DesignerGridPainter
{
    internal const int MaximumDots = 65_536;

    internal static double GetDeviceStep(Rectangle bounds, int spacing, double scale)
    {
        double original = spacing * scale;
        if (!double.IsFinite(original) || original <= 0) return double.PositiveInfinity;
        // Only the displayed multiple changes. A one-unit authored snap grid cannot force
        // millions of draw calls when the surface is large or the preview is zoomed out.
        double desired = Math.Max(6, Math.Sqrt(Math.Max(0, (double)bounds.Width * bounds.Height) / (MaximumDots / 4d)));
        return original * Math.Max(1, Math.Ceiling(desired / original));
    }

    internal static void Draw(PaintEventArgs e, Rectangle bounds, DesignerSurfaceView view, DesignerSnappingOptions options)
    {
        if (!options.ShowGrid || bounds.Width <= 0 || bounds.Height <= 0) return;
        double combinedScale = e.Scaling * view.Scale;
        double step = GetDeviceStep(bounds, options.GridSize, combinedScale);
        if (!double.IsFinite(step)) return;
        using var paint = new SKPaint { Color = new SKColor(225, 225, 225), StrokeWidth = 1, IsAntialias = false };
        double documentStep = options.GridSize * Math.Max(1, Math.Round(step / (options.GridSize * combinedScale)));
        double originX = view.ClientX * e.Scaling, originY = view.ClientY * e.Scaling;
        double firstX = Math.Max(1, Math.Floor((bounds.Left - originX) / step) - 1) * documentStep;
        double firstY = Math.Max(1, Math.Floor((bounds.Top - originY) / step) - 1) * documentStep;
        int painted = 0;
        int visited = 0;
        for (double x = firstX; ; x += documentStep)
        {
            if (++visited > MaximumDots * 2) return;
            double deviceX = Math.Round((view.ClientX + Math.Round(x * view.Scale)) * e.Scaling);
            if (deviceX >= (long)bounds.X + bounds.Width) break;
            if (deviceX < bounds.Left) continue;
            for (double y = firstY; ; y += documentStep)
            {
                if (++visited > MaximumDots * 2) return;
                double deviceY = Math.Round((view.ClientY + Math.Round(y * view.Scale)) * e.Scaling);
                if (deviceY >= (long)bounds.Y + bounds.Height) break;
                if (deviceY < bounds.Top) continue;
                if (++painted > MaximumDots) return;
                e.Canvas.DrawPoint((float)deviceX, (float)deviceY, paint);
            }
        }
    }
}
