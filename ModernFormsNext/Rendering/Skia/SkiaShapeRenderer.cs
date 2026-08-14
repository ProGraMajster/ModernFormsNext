using ModernFormsNext.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Rendering.Skia;

/// <summary>Renders every built-in Shape through one fill-and-stroke pipeline.</summary>
internal static class SkiaShapeRenderer
{
    public static void Render(
        SKCanvas canvas,
        SKPath path,
        SKRect paintBounds,
        Brush? fill,
        Brush? stroke,
        float strokeThickness,
        StrokeLineCap lineCap,
        StrokeLineJoin lineJoin,
        float miterLimit)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(path);

        if (SkiaBrushPaintFactory.CanRender(fill))
        {
            if (fill is GlassBrush)
            {
                canvas.Save();
                canvas.ClipPath(path, SKClipOperation.Intersect, antialias: true);
                SkiaExtensions.RenderBrushBackground(canvas, paintBounds, fill, SKColors.Transparent);
                canvas.Restore();
            }
            else
            {
                using SkiaBrushPaint? fillPaint = SkiaBrushPaintFactory.Create(fill, paintBounds, SKPaintStyle.Fill);
                if (fillPaint is not null)
                    canvas.DrawPath(path, fillPaint.Paint);
            }
        }

        if (strokeThickness <= 0f || !SkiaBrushPaintFactory.CanRender(stroke))
            return;

        using SkiaBrushPaint? strokePaint = SkiaBrushPaintFactory.Create(stroke, paintBounds, SKPaintStyle.Stroke);
        if (strokePaint is null)
            return;

        ConfigureStroke(strokePaint.Paint, strokeThickness, lineCap, lineJoin, miterLimit);
        canvas.DrawPath(path, strokePaint.Paint);
    }

    public static void ConfigureStroke(
        SKPaint paint,
        float strokeThickness,
        StrokeLineCap lineCap,
        StrokeLineJoin lineJoin,
        float miterLimit)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = strokeThickness;
        paint.StrokeCap = MapLineCap(lineCap);
        paint.StrokeJoin = MapLineJoin(lineJoin);
        paint.StrokeMiter = miterLimit;
    }

    public static SKStrokeCap MapLineCap(StrokeLineCap lineCap)
        => lineCap switch
        {
            StrokeLineCap.Flat => SKStrokeCap.Butt,
            StrokeLineCap.Round => SKStrokeCap.Round,
            StrokeLineCap.Square => SKStrokeCap.Square,
            _ => throw new ArgumentOutOfRangeException(nameof(lineCap), lineCap, "The stroke line cap is not defined.")
        };

    public static SKStrokeJoin MapLineJoin(StrokeLineJoin lineJoin)
        => lineJoin switch
        {
            StrokeLineJoin.Miter => SKStrokeJoin.Miter,
            StrokeLineJoin.Round => SKStrokeJoin.Round,
            StrokeLineJoin.Bevel => SKStrokeJoin.Bevel,
            _ => throw new ArgumentOutOfRangeException(nameof(lineJoin), lineJoin, "The stroke line join is not defined.")
        };
}
