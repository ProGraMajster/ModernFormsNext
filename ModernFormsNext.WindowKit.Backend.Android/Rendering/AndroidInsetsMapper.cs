using ModernFormsNext.WindowKit;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

internal static class AndroidInsetsMapper
{
    // WindowInsets describes root-window edges. Intersect that occlusion with the actual
    // Skia view before converting density: decor-fitted views must not inset their content twice.
    internal static Thickness ToSurface(Thickness pixels, Rect surface, Size rootSize, double density)
    {
        if (!double.IsFinite(density) || density <= 0) throw new ArgumentOutOfRangeException(nameof(density));
        if (surface.Width <= 0 || surface.Height <= 0 || rootSize.Width <= 0 || rootSize.Height <= 0) return default;
        return new Thickness(
            Math.Clamp(pixels.Left - surface.X, 0, surface.Width) / density,
            Math.Clamp(pixels.Top - surface.Y, 0, surface.Height) / density,
            Math.Clamp(surface.Right - (rootSize.Width - pixels.Right), 0, surface.Width) / density,
            Math.Clamp(surface.Bottom - (rootSize.Height - pixels.Bottom), 0, surface.Height) / density);
    }
}
