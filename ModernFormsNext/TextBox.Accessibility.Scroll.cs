using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class TextBox
{
    private bool accessibleScrollObserved;
    private bool accessibleScrollPublishing;
    private AccessibleScrollInfo? accessibleScrollPublished;

    internal AccessibleScrollInfo GetAccessibleScrollInfo()
    {
        accessibleScrollObserved = true;
        var block = GetTextInputLayoutBlock();
        var viewport = PaddedClientRectangle;
        double xScale = ScaleFactor.Width, yScale = ScaleFactor.Height;
        double maxX = Math.Max(0, Math.Ceiling(block.MeasuredWidth) - viewport.Width);
        double maxY = Math.Max(0, Math.Ceiling(block.MeasuredHeight) - viewport.Height);
        // ScrollToCaret can retain a few pixels of caret margin outside the measured glyph box.
        // Include the real current origin instead of reporting a fabricated clamped position.
        return new(new(scroll_x / xScale, Math.Min(0, scroll_x) / xScale, Math.Max(maxX, scroll_x) / xScale,
                viewport.Width / xScale, Math.Max(1, CurrentFontSize) / xScale, viewport.Width / xScale),
            new(scroll_y / yScale, Math.Min(0, scroll_y) / yScale, Math.Max(maxY, scroll_y) / yScale,
                viewport.Height / yScale, Math.Max(1, CurrentFontSize) / yScale, viewport.Height / yScale),
            AccessibilityScrollGeometry.ToCanonicalDeviceBounds(this, viewport));
    }

    internal bool PerformAccessibleScroll(AccessibleScrollRequest request)
    {
        VerifyTextInputAccess();
        var lifetime = new AccessibilityControlLifetime(this);
        if (!lifetime.IsCurrent || !Enabled || !Visible || IsAccessibilitySensitive) return false;
        var info = GetAccessibleScrollInfo();
        if (!request.TryGetOffset(info.Horizontal, true, out var horizontal)
            || !request.TryGetOffset(info.Vertical, false, out var vertical)
            || !lifetime.IsCurrent || IsAccessibilitySensitive) return false;
        int x = horizontal is { } h ? (int)Math.Clamp(Math.Round(h * ScaleFactor.Width), int.MinValue, int.MaxValue) : scroll_x;
        int y = vertical is { } v ? (int)Math.Clamp(Math.Round(v * ScaleFactor.Height), int.MinValue, int.MaxValue) : scroll_y;
        if (x != scroll_x || y != scroll_y) DoScroll(x - scroll_x, y - scroll_y);
        return lifetime.IsCurrent && Enabled && Visible && !IsAccessibilitySensitive;
    }

    internal void NotifyAccessibleScrollChanged()
    {
        if (!accessibleScrollObserved || accessibleTextDepth != 0 || accessibleScrollPublishing || IsDisposed || Disposing || IsAccessibilitySensitive) return;
        accessibleScrollPublishing = true;
        try
        {
            for (int pass = 0; pass < 64; pass++)
            {
                if (IsDisposed || Disposing || IsAccessibilitySensitive) return;
                var info = GetAccessibleScrollInfo();
                if (accessibleScrollPublished == info) return;
                accessibleScrollPublished = info;
                NotifyAccessibilityClients(AccessibleEvents.ScrollChanged);
            }
            throw new InvalidOperationException("Text viewport notifications did not stabilize after 64 changes.");
        }
        finally { accessibleScrollPublishing = false; }
    }
}
