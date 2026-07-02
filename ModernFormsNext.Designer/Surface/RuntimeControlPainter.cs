using System.Drawing;
using System.Reflection;
using ModernFormsNext;
using ModernFormsNext.Renderers;
using SkiaSharp;

namespace ModernFormsNext.Designer.Surface;

internal static class RuntimeControlPainter
{
    private static readonly BindingFlags PaintMethodFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo? RaisePaintBackgroundMethod = typeof(Control).GetMethod("RaisePaintBackground", PaintMethodFlags);
    private static readonly MethodInfo? RaisePaintMethod = typeof(Control).GetMethod("RaisePaint", PaintMethodFlags);

    public static bool TryPaint(
        PaintEventArgs target,
        Control control,
        Rectangle destination,
        out RuntimeControlPaintDiagnostics diagnostics,
        out string? error)
    {
        diagnostics = default;
        error = null;

        if (destination.Width <= 0 || destination.Height <= 0)
            return true;

        var imageInfo = new SKImageInfo(destination.Width, destination.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        using var bitmap = new SKBitmap(imageInfo);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var controlPaintArgs = new PaintEventArgs(imageInfo, canvas, target.Scaling);

        try
        {
            // Paint into an isolated surface first. Runtime background drawing clears the
            // canvas, so doing it directly on the designer surface would erase the workspace.
            if (!TryRaiseRuntimePaint(control, controlPaintArgs, out error))
                return false;

            canvas.Flush();
            diagnostics = AnalyzeBitmap(bitmap);

            target.Canvas.DrawBitmap(bitmap, destination.Left, destination.Top);
            return true;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
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

    private static bool TryRaiseRuntimePaint(Control control, PaintEventArgs e, out string? error)
    {
        error = null;

        if (RaisePaintBackgroundMethod is null || RaisePaintMethod is null)
        {
            return TryRenderFallback(control, e, out error);
        }

        try
        {
            RaisePaintBackgroundMethod.Invoke(control, [e]);
            RaisePaintMethod.Invoke(control, [e]);
            return true;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            error = $"{ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static bool TryRenderFallback(Control control, PaintEventArgs e, out string? error)
    {
        error = null;

        var bounds = new Rectangle(0, 0, control.ScaledWidth, control.ScaledHeight);

        e.Canvas.DrawBackground(bounds, control.CurrentStyle, control.BackgroundBrush);
        e.Canvas.DrawBorder(bounds, control.CurrentStyle);

        return TryRenderControl(control, e, out error);
    }

    private static bool TryRenderControl(Control control, PaintEventArgs e, out string? error)
    {
        error = null;

        try
        {
            RenderManager.Render(control, e);
            return true;
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("No renderer found", StringComparison.Ordinal))
        {
            return TryInvokeOnPaint(control, e, out error);
        }
    }

    private static bool TryInvokeOnPaint(Control control, PaintEventArgs e, out string? error)
    {
        error = null;

        try
        {
            var method = control.GetType().GetMethod("OnPaint", PaintMethodFlags)
                ?? typeof(Control).GetMethod("OnPaint", PaintMethodFlags);

            if (method is null)
            {
                error = "No renderer or OnPaint method was found.";
                return false;
            }

            method.Invoke(control, [e]);
            return true;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            error = $"{ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }
}
