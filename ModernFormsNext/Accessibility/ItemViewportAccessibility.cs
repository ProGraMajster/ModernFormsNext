using System.Drawing;

namespace ModernFormsNext.Accessibility;

// Adapts the existing item-index scrollbar; there is no additional position or item model.
internal static class ItemViewportAccessibility
{
    internal static AccessibleScrollInfo Read(Control owner, ScrollBar bar, int itemHeight)
    {
        var viewport = owner.ClientRectangle;
        if (bar.Visible) viewport.Width = Math.Max(0, viewport.Width - bar.ScaledWidth);
        double row = Math.Max(1, itemHeight) / (double)owner.ScaleFactor.Height;
        double height = Math.Max(0, viewport.Height) / (double)owner.ScaleFactor.Height;
        Point a = owner.PointToScreen(viewport.Location), b = owner.PointToScreen(new(viewport.Right, viewport.Top)),
            c = owner.PointToScreen(new(viewport.Left, viewport.Bottom)), d = owner.PointToScreen(new(viewport.Right, viewport.Bottom));
        var bounds = Rectangle.FromLTRB(Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)),
            Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)), Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)),
            Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)));
        return new(new(0, 0, 0, viewport.Width / (double)owner.ScaleFactor.Width, 0, 0),
            new(bar.Value * row, 0, bar.Maximum * row, height, row, Math.Max(1, bar.LargeChange) * row), bounds);
    }

    internal static bool Perform(Control owner, ScrollBar bar, int itemHeight, AccessibleScrollRequest request)
    {
        var lifetime = new AccessibilityControlLifetime(owner);
        if (!lifetime.IsCurrent || !owner.Enabled || !owner.Visible) return false;
        var info = Read(owner, bar, itemHeight);
        if (!request.TryGetOffset(info.Horizontal, true, out _)
            || !request.TryGetOffset(info.Vertical, false, out var vertical)) return false;
        if (vertical is { } offset)
        {
            if (!bar.Enabled || !bar.Visible) return false;
            double index = offset / info.Vertical.SmallChange;
            // Round in the direction of travel so a fractional forward request can make progress.
            double target = offset > info.Vertical.Offset ? Math.Ceiling(index) : Math.Floor(index);
            bar.Value = (int)Math.Clamp(target, bar.Minimum, bar.Maximum);
        }
        return lifetime.IsCurrent && owner.Enabled && owner.Visible;
    }
}
