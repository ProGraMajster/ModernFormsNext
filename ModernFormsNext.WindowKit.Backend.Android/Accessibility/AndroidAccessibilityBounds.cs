using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

/// <summary>Converts surface logical coordinates to physical pixels exactly once.</summary>
internal static class AndroidAccessibilityBounds
{
    internal static Rect Intersect(Rect a, Rect b)
    {
        double x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y);
        return new(x, y, Math.Max(0, Math.Min(a.Right, b.Right) - x),
            Math.Max(0, Math.Min(a.Bottom, b.Bottom) - y));
    }

    internal static Rect ToScreen(Rect bounds, double density, double screenX, double screenY)
    {
        if (!double.IsFinite(density) || density <= 0 || !Valid(bounds)
            || !double.IsFinite(screenX) || !double.IsFinite(screenY)) return default;
        double left = Clamp(Math.Floor(bounds.X * density + screenX));
        double top = Clamp(Math.Floor(bounds.Y * density + screenY));
        double right = Clamp(Math.Ceiling(bounds.Right * density + screenX));
        double bottom = Clamp(Math.Ceiling(bounds.Bottom * density + screenY));
        return new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    internal static Rect Clip(IPlatformAccessibleObject node, IPlatformAccessibleObject root, Rect viewport)
    {
        Rect clipped = Intersect(node.Bounds, viewport);
        var current = node;
        for (int depth = 0; depth < 512; depth++)
        {
            bool logicalRowAncestor = !ReferenceEquals(current, node) && current.GetControlType() is 13 or 15 or 17 or 19;
            int excludedStates = AndroidAccessibilityMapper.Invisible
                | (logicalRowAncestor ? 0 : AndroidAccessibilityMapper.Offscreen);
            if (current.GetAccessibilityView() == 4 || (current.State & excludedStates) != 0)
                return default;
            // A tree/menu item's rectangle describes its own row, not the viewport containing
            // expanded children. Clip against structural ancestors, not these logical rows.
            if (ReferenceEquals(current, node) || current.GetControlType() is not (13 or 15 or 17 or 19))
                clipped = Intersect(clipped, current.Bounds);
            if (ReferenceEquals(current, root)) return Valid(clipped) ? clipped : default;
            if (current.Parent is not { } parent) return default;
            current = parent;
        }
        return default; // Malformed custom parent cycle or unreasonably deep hierarchy.
    }

    internal static bool Valid(Rect rect) => double.IsFinite(rect.X) && double.IsFinite(rect.Y)
        && double.IsFinite(rect.Right) && double.IsFinite(rect.Bottom) && rect.Width > 0 && rect.Height > 0;

    private static double Clamp(double value) => Math.Clamp(value, int.MinValue, int.MaxValue);
}
