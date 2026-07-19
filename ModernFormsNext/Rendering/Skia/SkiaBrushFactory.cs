using System;
using System.Numerics;
using ModernFormsNext.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Rendering.Skia;

/// <summary>
/// Converts shared framework brushes into short-lived, bounds-specific Skia shaders.
/// </summary>
/// <remarks>
/// Callers own and must dispose the returned shader. The factory intentionally does not cache
/// native shaders because their coordinates depend on current bounds and a brush can be shared by
/// controls of different sizes. Gradient stop ordering is cached by <see cref="GradientBrush"/>.
/// </remarks>
internal static class SkiaBrushFactory
{
    public static SKShader? CreateGradientShader(GradientBrush brush, SKRect bounds)
    {
        ArgumentNullException.ThrowIfNull(brush);
        GradientStop[] stops = brush.GetOrderedStops();
        if (stops.Length < 2 || bounds.Width <= 0f || bounds.Height <= 0f)
            return null;

        SKColor[] colors = new SKColor[stops.Length];
        float[] positions = new float[stops.Length];
        for (int index = 0; index < stops.Length; index++)
        {
            colors[index] = ApplyOpacity(stops[index].Color, brush.Opacity);
            positions[index] = stops[index].Offset;
        }

        SKShaderTileMode tileMode = MapSpreadMode(brush.SpreadMode);
        SKShader? shader = brush switch
        {
            LinearGradientBrush linear => CreateLinearGradient(linear, bounds, colors, positions, tileMode),
            RadialGradientBrush radial => CreateRadialGradient(radial, bounds, colors, positions, tileMode),
            SweepGradientBrush sweep => CreateSweepGradient(sweep, bounds, colors, positions, tileMode),
            _ => null
        };

        return shader is null ? null : ApplyTransform(shader, brush.Transform);
    }

    public static SKColor ApplyOpacity(SKColor color, float opacity)
    {
        byte alpha = (byte)Math.Clamp((int)MathF.Round(color.Alpha * opacity), byte.MinValue, byte.MaxValue);
        return new SKColor(color.Red, color.Green, color.Blue, alpha);
    }

    public static SKShaderTileMode MapSpreadMode(GradientSpreadMode spreadMode)
        => spreadMode switch
        {
            GradientSpreadMode.Pad => SKShaderTileMode.Clamp,
            GradientSpreadMode.Repeat => SKShaderTileMode.Repeat,
            GradientSpreadMode.Reflect => SKShaderTileMode.Mirror,
            _ => throw new ArgumentOutOfRangeException(nameof(spreadMode), spreadMode, "The gradient spread mode is not defined.")
        };

    public static SKPoint ResolveRelativePoint(System.Drawing.PointF point, SKRect bounds)
        => new(bounds.Left + (bounds.Width * point.X), bounds.Top + (bounds.Height * point.Y));

    public static SKShader ApplyTransform(SKShader shader, Matrix3x2 transform)
    {
        ArgumentNullException.ThrowIfNull(shader);
        if (transform.IsIdentity)
            return shader;

        var matrix = new SKMatrix(
            transform.M11,
            transform.M21,
            transform.M31,
            transform.M12,
            transform.M22,
            transform.M32,
            0f,
            0f,
            1f);

        SKShader transformed = shader.WithLocalMatrix(matrix);
        shader.Dispose();
        return transformed;
    }

    private static SKShader CreateLinearGradient(
        LinearGradientBrush brush,
        SKRect bounds,
        SKColor[] colors,
        float[] positions,
        SKShaderTileMode tileMode)
        => SKShader.CreateLinearGradient(
            ResolveRelativePoint(brush.Start, bounds),
            ResolveRelativePoint(brush.End, bounds),
            colors,
            positions,
            tileMode);

    private static SKShader? CreateRadialGradient(
        RadialGradientBrush brush,
        SKRect bounds,
        SKColor[] colors,
        float[] positions,
        SKShaderTileMode tileMode)
    {
        float radius = MathF.Min(bounds.Width, bounds.Height) * brush.Radius;
        if (radius <= 0f)
            return null;

        SKPoint center = ResolveRelativePoint(brush.CenterPoint, bounds);
        SKPoint origin = ResolveRelativePoint(brush.GradientOrigin, bounds);
        if (center == origin)
            return SKShader.CreateRadialGradient(center, radius, colors, positions, tileMode);

        return SKShader.CreateTwoPointConicalGradient(origin, 0f, center, radius, colors, positions, tileMode);
    }

    private static SKShader CreateSweepGradient(
        SweepGradientBrush brush,
        SKRect bounds,
        SKColor[] colors,
        float[] positions,
        SKShaderTileMode tileMode)
        => SKShader.CreateSweepGradient(
            ResolveRelativePoint(brush.CenterPoint, bounds),
            colors,
            positions,
            tileMode,
            brush.StartAngle,
            brush.EndAngle);
}
