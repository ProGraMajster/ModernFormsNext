using ModernFormsNext.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Rendering.Skia;

/// <summary>Creates short-lived Skia paints from shared framework brushes.</summary>
internal static class SkiaBrushPaintFactory
{
    public static SkiaBrushPaint? Create(Brush? brush, SKRect bounds, SKPaintStyle style)
    {
        if (!CanRender(brush))
            return null;

        var paint = new SKPaint
        {
            IsAntialias = true,
            Style = style
        };

        SkiaShaderScope shader = default;
        try
        {
            switch (brush)
            {
                case SolidColorBrush solid:
                    paint.Color = SkiaBrushFactory.ApplyOpacity(solid.Color, solid.Opacity);
                    return new SkiaBrushPaint(paint, default);

                case GradientBrush gradient:
                    GradientStop[] stops = gradient.GetOrderedStops();
                    if (stops.Length == 1 || gradient is RadialGradientBrush { Radius: 0f })
                    {
                        paint.Color = SkiaBrushFactory.ApplyOpacity(stops[^1].Color, gradient.Opacity);
                        return new SkiaBrushPaint(paint, default);
                    }

                    shader = SkiaBrushFactory.CreateOwnedGradientShader(gradient, EnsureUsableBounds(bounds));
                    if (shader.Shader is null)
                    {
                        paint.Dispose();
                        return null;
                    }

                    paint.Shader = shader.Shader;
                    return new SkiaBrushPaint(paint, shader);

                case GlassBrush glass:
                    // A glass surface has multiple fill layers, but a stroke has only one path. Use
                    // the brush's public border color as its deterministic stroke representation.
                    paint.Color = SkiaBrushFactory.ApplyOpacity(glass.BorderColor, glass.Opacity);
                    return new SkiaBrushPaint(paint, default);

                default:
                    paint.Dispose();
                    return null;
            }
        }
        catch
        {
            // Ownership is transferred only by a successful return. A failing shader
            // factory or paint assignment must release both resources already acquired.
            try { paint.Dispose(); }
            finally { shader.Dispose(); }
            throw;
        }
    }

    public static bool CanRender(Brush? brush)
        => brush is not null and not NoBrush && brush.Opacity > 0f && brush switch
        {
            SolidColorBrush => true,
            GradientBrush gradient => gradient.GetOrderedStops().Length > 0,
            GlassBrush => true,
            _ => false
        };

    private static SKRect EnsureUsableBounds(SKRect bounds)
    {
        const float halfMinimum = 0.5f;
        if (bounds.Width <= 0f)
        {
            bounds.Left -= halfMinimum;
            bounds.Right += halfMinimum;
        }
        if (bounds.Height <= 0f)
        {
            bounds.Top -= halfMinimum;
            bounds.Bottom += halfMinimum;
        }
        return bounds;
    }
}

/// <summary>Owns a Skia paint and its optional shader for one draw operation.</summary>
internal sealed class SkiaBrushPaint : IDisposable
{
    private readonly SkiaShaderScope shader;
    private bool disposed;

    public SkiaBrushPaint(SKPaint paint, SkiaShaderScope shader)
    {
        Paint = paint;
        this.shader = shader;
    }

    public SKPaint Paint { get; }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        try { Paint.Dispose(); }
        finally { shader.Dispose(); }
    }
}
