using System.Drawing;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class ScrollableControl
{
    private static int ScrollMaximum(int minimum, int content, int viewport)
        => (int)Math.Clamp((long)minimum + Math.Max(0L, (long)content - viewport), int.MinValue, int.MaxValue);
    private int scroll_update_depth;
    private bool publishing_scroll;
    private AccessibleScrollInfo? published_scroll;

    private void HandleScrollMetadata(object? sender, EventArgs e)
    {
        scroll_update_depth++;
        try { HandleScroll(sender, e); }
        finally { EndScrollUpdate(); }
    }

    internal Rectangle LogicalScrollViewport
    {
        get
        {
            var rect = base.DisplayRectangle;
            if (vscrollbar.Visible) rect.Width -= vscrollbar.Width;
            if (hscrollbar.Visible) rect.Height -= hscrollbar.Height;
            return new(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));
        }
    }

    internal AccessibleScrollInfo GetAccessibleScrollInfo()
    {
        var viewport = LogicalScrollViewport;
        static AccessibleScrollAxis Axis(ScrollBar bar, int length)
            => bar.Visible
                ? new(bar.Value, bar.Minimum, bar.Maximum, length, bar.SmallChange, bar.LargeChange)
                : new(0, 0, 0, length, 0, 0);
        return new(Axis(hscrollbar, viewport.Width), Axis(vscrollbar, viewport.Height),
            AccessibilityScrollGeometry.ToCanonicalBounds(this, viewport));
    }

    internal bool PerformAccessibleScroll(AccessibleScrollRequest request)
    {
        if (IsDisposed || !Enabled || !Visible || !AutoScroll) return false;
        var lifetime = new AccessibilityControlLifetime(this);
        var info = GetAccessibleScrollInfo();
        if (!request.TryGetOffset(info.Horizontal, true, out var horizontal)
            || !request.TryGetOffset(info.Vertical, false, out var vertical)
            || horizontal.HasValue && !hscrollbar.Enabled || vertical.HasValue && !vscrollbar.Enabled) return false;
        scroll_update_depth++;
        try {
            if (horizontal.HasValue && !SetAxis(hscrollbar, horizontal.Value)) return false;
            if (!lifetime.IsCurrent || !Enabled || !Visible || !AutoScroll) return false;
            if (vertical.HasValue && !SetAxis(vscrollbar, vertical.Value)) return false;
            return lifetime.IsCurrent && Enabled && Visible;
        } finally { EndScrollUpdate(); }

        bool SetAxis(ScrollBar bar, double value)
        {
            if (!lifetime.IsCurrent || bar.IsDisposed || !bar.Visible || !bar.Enabled) return false;
            int target = (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), bar.Minimum, bar.Maximum);
            if (bar.Value == target) return true;
            bar.Value = target;
            if (!lifetime.IsCurrent || !Enabled || !Visible) return false;
            OnScroll(new ScrollEventArgs(ScrollEventType.ThumbTrack, target));
            return lifetime.IsCurrent;
        }
    }

    private void EndScrollUpdate()
    {
        if (--scroll_update_depth != 0 || publishing_scroll || IsDisposed) return;
        publishing_scroll = true;
        try {
            for (int pass = 0; pass < 64; pass++) {
                if (IsDisposed) return;
                var current = GetAccessibleScrollInfo();
                if (published_scroll == current) return;
                published_scroll = current;
                NotifyAccessibilityClients(AccessibleEvents.ScrollChanged);
            }
            throw new InvalidOperationException("Viewport notifications did not stabilize after 64 changes.");
        } finally { publishing_scroll = false; }
    }
}

internal static class AccessibilityScrollGeometry
{
    internal static Rectangle ToCanonicalDeviceBounds(Control owner, Rectangle device)
    {
        var a = owner.PointToScreen(device.Location);
        var b = owner.PointToScreen(new(device.Right, device.Top));
        var c = owner.PointToScreen(new(device.Left, device.Bottom));
        var d = owner.PointToScreen(new(device.Right, device.Bottom));
        return Rectangle.FromLTRB(Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)),
            Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)), Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)),
            Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)));
    }
    // PointToScreen takes the same scaled local coordinates used by ControlAccessibleObject.Bounds.
    internal static Rectangle ToCanonicalBounds(Control owner, Rectangle logical)
    {
        float x = owner.ScaleFactor.Width, y = owner.ScaleFactor.Height;
        // Preserve outward rounding and transformed corner extrema without allocating an
        // array and enumeration delegates for every semantic State or viewport query.
        return ToCanonicalDeviceBounds(owner, Rectangle.FromLTRB(
            (int)Math.Floor(logical.Left * x), (int)Math.Floor(logical.Top * y),
            (int)Math.Ceiling(logical.Right * x), (int)Math.Ceiling(logical.Bottom * y)));
    }
}
