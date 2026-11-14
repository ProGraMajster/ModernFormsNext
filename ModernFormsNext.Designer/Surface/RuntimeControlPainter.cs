using System.Drawing;
using ModernFormsNext;
using SkiaSharp;

namespace ModernFormsNext.Designer.Surface;

internal static class RuntimeControlPainter
{
    // Bound device allocation independently of authored logical coordinates, DPI and zoom.
    // At four bytes per pixel the admitted temporary bitmap uses at most 64 MiB.
    internal const int MaximumBitmapDimension = 8192;
    internal const int MaximumBitmapPixelCount = 16 * 1024 * 1024;

    public static bool TryPaint(
        PaintEventArgs target,
        Control control,
        Size logicalSize,
        Rectangle destination,
        out RuntimeControlPaintDiagnostics diagnostics,
        out string? error)
    {
        diagnostics = default;
        error = null;

        if (logicalSize.Width <= 0 || logicalSize.Height <= 0 || destination.Width <= 0 || destination.Height <= 0)
            return true;

        if (destination.Width > MaximumBitmapDimension || destination.Height > MaximumBitmapDimension
            || (long)destination.Width * destination.Height > MaximumBitmapPixelCount)
        {
            error = $"Preview bitmap exceeds {MaximumBitmapDimension} device pixels per edge or {MaximumBitmapPixelCount} pixels in total.";
            return false;
        }

        string stage = "Allocate preview bitmap";
        try
        {
            var deviceImageInfo = new SKImageInfo(destination.Width, destination.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            var logicalImageInfo = new SKImageInfo(logicalSize.Width, logicalSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            using var bitmap = new SKBitmap(deviceImageInfo);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            // Runtime preview controls are intentionally detached from a real window, so their
            // ScaleFactor is 1. Keep their entire layout and paint pass in logical 96-DPI units and
            // scale the canvas once to the preview's device-pixel destination. Passing the monitor DPI
            // through PaintEventArgs would scale fonts and paint metrics while ClientRectangle stayed
            // unscaled, producing the smaller control visible inside a larger high-DPI rectangle.
            canvas.Scale(
                destination.Width / (float)logicalSize.Width,
                destination.Height / (float)logicalSize.Height);

            var controlPaintArgs = new PaintEventArgs(logicalImageInfo, canvas, scaling: 1d);
            stage = "Apply preview clip";
            control.ApplyGeometryClip(canvas);

            // Paint into an isolated surface first. Runtime background drawing clears the
            // canvas, so doing it directly on the designer surface would erase the workspace.
            stage = "Paint background";
            control.RaisePaintBackground(controlPaintArgs);
            stage = "Paint";
            control.RaisePaint(controlPaintArgs);

            stage = "Composite preview";
            canvas.Flush();
            diagnostics = AnalyzeBitmap(bitmap);

            target.Canvas.DrawBitmap(bitmap, destination.Left, destination.Top);
            return true;
        }
        catch (Exception ex)
        {
            error = DesignerPreviewControl.Failure(stage, ex);
            return false;
        }
    }

    private static RuntimeControlPaintDiagnostics AnalyzeBitmap(SKBitmap bitmap)
    {
        const int TargetSamples = 2048;

        if (bitmap.Width <= 0 || bitmap.Height <= 0)
            return new RuntimeControlPaintDiagnostics(bitmap.Width, bitmap.Height, 0, 0, 0);

        var totalPixels = bitmap.Width * bitmap.Height;
        var step = Math.Max(1, (int)Math.Sqrt(totalPixels / (double)TargetSamples));
        var samples = 0;
        var visible = 0;
        var opaque = 0;

        for (var y = 0; y < bitmap.Height; y += step)
        {
            for (var x = 0; x < bitmap.Width; x += step)
            {
                var pixel = bitmap.GetPixel(x, y);
                samples++;

                if (pixel.Alpha > 0)
                    visible++;

                if (pixel.Alpha > 250)
                    opaque++;
            }
        }

        return new RuntimeControlPaintDiagnostics(bitmap.Width, bitmap.Height, samples, visible, opaque);
    }
}
