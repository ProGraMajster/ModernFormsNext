using System.Globalization;
using SkiaSharp;

namespace ModernFormsNext.Diagnostics;

// This is a consumer of the recorder's bounded view, never another collector. All resources
// below belong to this draw call; the canvas, theme typeface and snapshot remain borrowed.
internal static class PerformanceOverlayRenderer
{
    internal static void Render(SKCanvas canvas, in PerformanceOverlayData snapshot,
        PerformanceOverlayOptions options, int logicalWidth, int logicalHeight, double canvasScale)
    {
        if (!options.Visible || logicalWidth <= 0 || logicalHeight <= 0) return;
        if (!double.IsFinite(canvasScale) || canvasScale <= 0 || canvasScale > float.MaxValue) return;

        int saved = canvas.Save();
        try
        {
            // WindowBase needs its current DPI scale; a borrowed surface host has already
            // applied density and passes one. Never infer current geometry from an old frame.
            canvas.Scale((float)canvasScale);
            canvas.ClipRect(new SKRect(0, 0, logicalWidth, logicalHeight));
            DrawRegions(canvas, snapshot, options);

            bool expanded = options.Mode == PerformanceOverlayMode.Expanded;
            float margin = (float)Math.Min(options.Margin, Math.Min(logicalWidth, logicalHeight) / 4d);
            float width = Math.Min(expanded ? 390 : 330, logicalWidth - margin * 2);
            float availableHeight = logicalHeight - margin * 2;
            if (width < 1 || availableHeight < 1) return;

            using var font = new SKFont(Theme.UIFont, Math.Clamp(Theme.FontSize, 10, 22));
            using var fill = new SKPaint { Color = Theme.BackgroundColor.WithAlpha(245), IsAntialias = true };
            using var ink = new SKPaint { Color = Theme.ForegroundColor, IsAntialias = true };
            using var stroke = new SKPaint { Color = Theme.BorderHighColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            float lineHeight = MathF.Ceiling(font.Metrics.Descent - font.Metrics.Ascent) + 4;
            int rows = CountRows(options, snapshot.LatestFrame is not null);
            float graphHeight = options.ShowFrameGraph ? 65 : 0;
            float height = Math.Min(availableHeight, rows * lineHeight + 16 + graphHeight);
            bool right = options.Corner is PerformanceOverlayCorner.TopRight or PerformanceOverlayCorner.BottomRight;
            bool bottom = options.Corner is PerformanceOverlayCorner.BottomLeft or PerformanceOverlayCorner.BottomRight;
            float left = right ? logicalWidth - margin - width : margin;
            float top = bottom ? logicalHeight - margin - height : margin;
            var bounds = new SKRect(left, top, left + width, top + height);
            canvas.DrawRoundRect(bounds, 5, 5, fill);
            canvas.DrawRoundRect(bounds, 5, 5, stroke);
            canvas.ClipRect(new SKRect(left + 6, top + 5, bounds.Right - 6, bounds.Bottom - 5));
            float y = top + 8 - font.Metrics.Ascent;

            void Row(string label, string value = "")
            {
                canvas.DrawText(label, left + 8, y, SKTextAlign.Left, font, ink);
                if (value.Length != 0)
                {
                    // Right alignment leaves a stable scan column. Values are numeric/metadata,
                    // not application text; a tiny viewport intentionally clips the HUD itself.
                    canvas.DrawText(value, bounds.Right - 8, y, SKTextAlign.Right, font, ink);
                }
                y += lineHeight;
            }

            Row("Performance", expanded ? "expanded · previous frame" : "previous frame");
            if (snapshot.LatestFrame is not { } frame)
            {
                Row("Waiting for a completed frame");
                return;
            }

            var work = frame.Work;
            bool Has(PerformanceOverlayMetrics group) => (options.Metrics & group) != 0;
            if (Has(PerformanceOverlayMetrics.Frame))
            {
                Row("Frame / observed FPS", $"{Ms(frame.Duration)} / {Number(frame.FramesPerSecond)}");
                if (expanded) Row($"Source {frame.SourceId} · #{frame.Sequence}", frame.Completed ? (frame.IsSlow ? "slow" : "completed") : "failed");
            }
            if (Has(PerformanceOverlayMetrics.Layout))
            {
                Row("Layout in paint / UI interval", $"{Ms(work.LayoutTime)} / {Ms(frame.ThreadWorkSincePreviousFrame.LayoutTime)}");
                if (expanded) Row("Preferred size / passes", $"{Ms(work.PreferredSizeTime)} / {work.LayoutPasses}");
            }
            if (Has(PerformanceOverlayMetrics.Render))
            {
                Row("Shared render", Ms(work.RenderTime));
                if (expanded) Row("HUD / Designer render", $"{Ms(work.OverlayTime)} / {Ms(work.DesignerRenderTime)}");
            }
            if (Has(PerformanceOverlayMetrics.InputAndAnimation))
            {
                Row("Input / animation", $"{Ms(work.InputTime)} / {Ms(work.AnimationTime)}");
                if (expanded) Row("UI interval input / animation", $"{Ms(frame.ThreadWorkSincePreviousFrame.InputTime)} / {Ms(frame.ThreadWorkSincePreviousFrame.AnimationTime)}");
            }
            if (Has(PerformanceOverlayMetrics.Controls))
            {
                Row("Repainted / visited", $"{work.ControlsRepainted} / {work.ControlsVisited}");
                if (expanded) Row("Composited / cache reuse", $"{work.ControlsComposited} / {work.ControlCacheHits}");
            }
            if (Has(PerformanceOverlayMetrics.Invalidation))
            {
                Row("Invalidation / root redraw", $"{work.InvalidationRequests} / {frame.RenderInfo.Redraw}");
                if (expanded) Row("UI interval invalidations", frame.ThreadWorkSincePreviousFrame.InvalidationRequests.ToString(CultureInfo.InvariantCulture));
            }
            if (Has(PerformanceOverlayMetrics.Memory))
            {
                Row("Managed allocation", frame.AllocatedBytes is long bytes ? $"{bytes:N0} B" : "n/a");
                if (expanded) Row("GC 0 / 1 / 2", $"{Number(frame.Gen0Collections)} / {Number(frame.Gen1Collections)} / {Number(frame.Gen2Collections)}");
            }
            if (Has(PerformanceOverlayMetrics.Backend))
            {
                Row("Backend", $"{frame.RenderInfo.Backend} / {frame.RenderInfo.Acceleration}");
                if (expanded) Row(frame.RenderInfo.Scale > 0 ? $"Scale {frame.RenderInfo.Scale:P0}" : "Scale n/a",
                    $"{frame.RenderInfo.LogicalWidth} × {frame.RenderInfo.LogicalHeight} · {frame.RenderInfo.Boundary}");
            }
            if (Has(PerformanceOverlayMetrics.Shaders)) Row("Shaders created / disposed", $"{work.ShadersCreated} / {work.ShadersDisposed}");
            if (expanded) Row("UI interval = all sources; times overlap");

            if (options.ShowFrameGraph)
                DrawGraph(canvas, snapshot, new SKRect(left + 8, y + 3, bounds.Right - 8, Math.Min(bounds.Bottom - 8, y + 58)), font, ink, stroke);
        }
        finally
        {
            canvas.RestoreToCount(saved);
        }
    }

    private static int CountRows(PerformanceOverlayOptions options, bool hasFrame)
    {
        if (!hasFrame) return 2;
        int count = 1;
        for (int bit = 1; bit <= (int)PerformanceOverlayMetrics.Shaders; bit <<= 1)
            if (((int)options.Metrics & bit) != 0)
                count += options.Mode == PerformanceOverlayMode.Expanded && bit != (int)PerformanceOverlayMetrics.Shaders ? 2 : 1;
        return count + (options.Mode == PerformanceOverlayMode.Expanded ? 1 : 0);
    }

    private static string Ms(TimeSpan value) => value.TotalMilliseconds.ToString("0.##", CultureInfo.InvariantCulture) + " ms";
    private static string Number(double? value) => value is double number ? number.ToString("0.#", CultureInfo.InvariantCulture) : "n/a";

    private static void DrawRegions(SKCanvas canvas, in PerformanceOverlayData snapshot, PerformanceOverlayOptions options)
    {
        if (snapshot.LatestFrame is not { } frame || (!options.ShowRepaintRegions && !options.ShowControlBounds && !options.ShowClipBounds)) return;
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        foreach (var region in snapshot.Regions)
        {
            if (region.SourceId != frame.SourceId) continue;
            bool show = region.Kind switch
            {
                PerformanceRegionKind.Repaint => options.ShowRepaintRegions,
                PerformanceRegionKind.ControlBounds => options.ShowControlBounds,
                PerformanceRegionKind.ClipBounds => options.ShowClipBounds,
                _ => false
            };
            if (!show) continue;
            var rectangle = region.Bounds;
            if (!float.IsFinite(rectangle.Left) || !float.IsFinite(rectangle.Top) ||
                !float.IsFinite(rectangle.Right) || !float.IsFinite(rectangle.Bottom) || rectangle.Width <= 0 || rectangle.Height <= 0) continue;
            paint.Color = region.Kind switch
            {
                PerformanceRegionKind.Repaint => Theme.WarningHighlightColor,
                PerformanceRegionKind.ControlBounds => Theme.AccentColor,
                _ => Theme.ForegroundColor
            };
            canvas.DrawRect(new SKRect(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom), paint);
        }
    }

    private static void DrawGraph(SKCanvas canvas, in PerformanceOverlayData snapshot, SKRect bounds, SKFont font, SKPaint ink, SKPaint stroke)
    {
        if (snapshot.LatestFrame is not { } latest || bounds.Width < 2 || bounds.Height < 20) return;
        int count = 0;
        double maximum = 16.67;
        bool Matches(PerformanceFrameMetrics frame) => frame.SourceId == latest.SourceId &&
            frame.RenderInfo.HostGeneration == latest.RenderInfo.HostGeneration &&
            frame.RenderInfo.BackingGeneration == latest.RenderInfo.BackingGeneration;
        for (int i = snapshot.Frames.Length - 1; i >= 0 && count < 120; i--)
            if (Matches(snapshot.Frames[i])) { maximum = Math.Max(maximum, snapshot.Frames[i].Duration.TotalMilliseconds); count++; }
        canvas.DrawText($"Frame duration · max {maximum:0.#} ms", bounds.Left, bounds.Top - font.Metrics.Ascent,
            SKTextAlign.Left, font, ink);
        bounds.Top += font.Metrics.Descent - font.Metrics.Ascent + 3;
        if (bounds.Height < 2 || count == 0) return;
        stroke.Color = Theme.BorderMidColor;
        canvas.DrawRect(bounds, stroke);
        stroke.Color = Theme.AccentColor;
        int plotted = 0;
        SKPoint? previous = null;
        for (int i = snapshot.Frames.Length - 1; i >= 0 && plotted < count; i--)
        {
            var frame = snapshot.Frames[i];
            if (!Matches(frame)) continue;
            float x = count == 1 ? bounds.Right : bounds.Right - plotted * bounds.Width / (count - 1);
            float y = bounds.Bottom - (float)Math.Clamp(frame.Duration.TotalMilliseconds / maximum, 0, 1) * bounds.Height;
            var point = new SKPoint(x, y);
            if (previous is SKPoint before) canvas.DrawLine(before, point, stroke);
            else canvas.DrawCircle(point, 1, stroke);
            previous = point;
            plotted++;
        }
    }
}
