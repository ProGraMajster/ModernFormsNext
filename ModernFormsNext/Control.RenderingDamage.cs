using System.Drawing;
using ModernFormsNext.Diagnostics;
using SkiaSharp;

namespace ModernFormsNext;

public partial class Control
{
    private RectangleF? lastCompositedArea;
    // Cached control surfaces and input geometry are device-space. Convert all four corners
    // so rotations, negative scales and animated presentation bounds remain conservative.
    private RectangleF DamageToParent(RectangleF rectangle)
    {
        var a = ClientPointToParentPresentation(new PointF(rectangle.Left, rectangle.Top));
        var b = ClientPointToParentPresentation(new PointF(rectangle.Right, rectangle.Top));
        var c = ClientPointToParentPresentation(new PointF(rectangle.Right, rectangle.Bottom));
        var d = ClientPointToParentPresentation(new PointF(rectangle.Left, rectangle.Bottom));
        return RectangleF.FromLTRB(Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)),
            Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)),
            Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)),
            Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)));
    }

    private RectangleF DevicePresentationArea => DamageToParent(new RectangleF(0, 0, ScaledWidth, ScaledHeight));

    private static void DrawClippedBackBuffer(SKCanvas canvas, SKBitmap buffer, float x, float y)
    {
        var clip = canvas.LocalClipBounds;
        int left = Math.Max(0, (int)Math.Floor(clip.Left - x) - 1);
        int top = Math.Max(0, (int)Math.Floor(clip.Top - y) - 1);
        int right = Math.Min(buffer.Width, (int)Math.Ceiling(clip.Right - x) + 1);
        int bottom = Math.Min(buffer.Height, (int)Math.Ceiling(clip.Bottom - y) + 1);
        if (right <= left || bottom <= top) return;
        if (left == 0 && top == 0 && right == buffer.Width && bottom == buffer.Height) {
            canvas.DrawBitmap(buffer, x, y);
            return;
        }

        // SkiaSharp 3 DrawBitmap creates an immutable SKImage from the mutable bitmap,
        // copying its pixels BEFORE destination clipping. A full 5K ancestor would thus
        // still be copied on every tiny hover. ExtractSubset shares the existing pixel ref;
        // DrawBitmap then makes its safe immutable copy of only this bounded source area.
        // Keep that copy semantics for borrowed/recording/GPU canvases: never lend a mutable
        // pixel pointer to an SKImage whose native lifetime might outlive this synchronous call.
        using var subset = new SKBitmap();
        if (buffer.ExtractSubset(subset, new SKRectI(left, top, right, bottom)))
            canvas.DrawBitmap(subset, x + left, y + top);
        else
            canvas.DrawBitmap(buffer, x, y);
    }

    private bool HasPaintableDirtyChild()
    {
        var viewport = new RectangleF(0, 0, ScaledWidth, ScaledHeight);
        foreach (var child in Controls.GetAllControls())
            if (child.Visible && child.Width > 0 && child.Height > 0 &&
                viewport.IntersectsWith(child.DevicePresentationArea) && child.NeedsPaint)
                return true;
        return false;
    }

    private void InvalidateWindowRegion(Rectangle deviceRectangle)
    {
        var damage = RectangleF.Intersect(deviceRectangle, new RectangleF(0, 0, ScaledWidth, ScaledHeight));
        Control current = this;
        while (current is not ControlAdapter)
        {
            // Hidden/offscreen content keeps its dirty bitmap state until it becomes visible,
            // but must not cause repeated paints of ancestors that cannot display it.
            if (!current.Visible || damage.Width <= 0 || damage.Height <= 0) return;
            damage = current.DamageToParent(damage);
            if (current.Parent is not { } parent) return;
            var currentArea = current.DevicePresentationArea;
            if (current.lastCompositedArea is { } previous && previous != currentArea) {
                damage = RectangleF.Union(damage, previous);
                // Old transformed pixels must be erased even if the new presentation is
                // outside the viewport. Invalidate the existing composition owner cache.
                parent.SetState(States.IsDirty, true);
            }
            damage = RectangleF.Intersect(damage, new RectangleF(0, 0, parent.ScaledWidth, parent.ScaledHeight));
            current = parent;
        }
        if (current.FindWindow() is not { } window || damage.Width <= 0 || damage.Height <= 0) return;
        var border = window.CurrentStyle.Border;
        // The root adapter composites after the managed window border. Round outward at
        // the native boundary; the one-pixel fringe includes antialiased transformed edges.
        double scale = window.Scaling;
        window.InvalidateLogicalRegion(new WindowKit.Rect(
            (damage.Left - 1) / scale + border.Left.GetWidth(),
            (damage.Top - 1) / scale + border.Top.GetWidth(),
            (damage.Width + 2) / scale, (damage.Height + 2) / scale));
    }

    internal void PaintChildren(PaintEventArgs e, float offsetX = 0, float offsetY = 0)
    {
        // Snapshot preserves existing mutation-during-paint semantics and back-to-front order.
        foreach (var child in Controls.GetAllControls().Where(IsVisibleForPainting).ToArray())
        {
            if (child.Width <= 0 || child.Height <= 0) {
                PerformanceRecorder.Count(PerformanceCounterKind.ZeroSizeControlsSkipped, control: child);
                continue;
            }
            var area = child.DevicePresentationArea;
            area.Offset(offsetX, offsetY);
            var clip = e.Canvas.LocalClipBounds;
            if (!area.IntersectsWith(new RectangleF(clip.Left, clip.Top, clip.Width, clip.Height)))
                continue; // Cull before allocating an offscreen child's bitmap.

            var buffer = child.GetBackBuffer();
            if (PerformanceRecorder.ShouldRecordRegions)
                child.RecordPerformancePaintRegions(this, clip);
            if (child.NeedsPaint) {
                using var measurement = PerformanceRecorder.Measure(PerformanceActivityKind.Render, child);
                PerformanceRecorder.Count(PerformanceCounterKind.ControlsRepainted, control: child);
                child.RecordPerformanceRegion(PerformanceRegionKind.Repaint);
                using var canvas = new SKCanvas(buffer);
                // A child's own invalidation may affect its complete cached image. An unchanged
                // ancestor only recomposites the damaged descendants, retaining its other pixels.
                // Transformed children use a full cache refresh; the final composition still clips.
                if (!child.GetState(States.IsDirty) && !child.HasRenderTransform && !child.HasDistinctPresentationBounds)
                    canvas.ClipRect(new SKRect(clip.Left - offsetX - child.ScaledLeft,
                        clip.Top - offsetY - child.ScaledTop, clip.Right - offsetX - child.ScaledLeft,
                        clip.Bottom - offsetY - child.ScaledTop));
                var args = new PaintEventArgs(buffer.Info, canvas, Scaling);
                child.RaisePaintBackground(args);
                child.RaisePaint(args);
                canvas.Flush();
                measurement.Complete();
            } else {
                PerformanceRecorder.Count(PerformanceCounterKind.ControlCacheHits, control: child);
            }
            child.DrawBackBuffer(e.Canvas, buffer, offsetX, offsetY);
            child.lastCompositedArea = child.DevicePresentationArea;
            PerformanceRecorder.Count(PerformanceCounterKind.ControlsComposited, control: child);
        }
    }
}
