using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class DocumentViewer
{
    private bool documentScrollPublishing;
    private AccessibleScrollInfo? publishedDocumentScroll;

    internal AccessibleScrollInfo GetAccessibleScrollInfo()
    {
        // Consume the canonical layout's committed scrollbar metrics. A semantic read does not
        // start document/image layout or loading; layout changes publish refreshed geometry.
        var viewport = PaddedClientRectangle;
        double scale = ScaleFactor.Height;
        return new(new(0, 0, 0, viewport.Width / (double)ScaleFactor.Width, 0, 0),
            new(scrollY / scale, 0, Math.Max(scrollY, VerticalScrollBar.Enabled ? VerticalScrollBar.Maximum : 0) / scale,
                viewport.Height / scale, VerticalScrollBar.SmallChange / scale, viewport.Height / scale),
            AccessibilityScrollGeometry.ToCanonicalDeviceBounds(this, viewport));
    }

    internal bool PerformAccessibleScroll(AccessibleScrollRequest request)
    {
        var lifetime = new AccessibilityControlLifetime(this);
        if (!lifetime.IsCurrent || !Enabled || !Visible) return false;
        var info = GetAccessibleScrollInfo();
        if (!request.TryGetOffset(info.Horizontal, true, out _)
            || !request.TryGetOffset(info.Vertical, false, out var vertical)) return false;
        if (vertical is { } offset)
        {
            if (!VerticalScrollBar.Enabled) return false;
            VerticalScrollBar.Value = (int)Math.Clamp(Math.Round(offset * ScaleFactor.Height), 0, VerticalScrollBar.Maximum);
        }
        return lifetime.IsCurrent && Enabled && Visible;
    }

    private void NotifyAccessibleScrollChanged()
    {
        if (documentScrollPublishing || IsDisposed || Disposing) return;
        documentScrollPublishing = true;
        try
        {
            for (int pass = 0; pass < 64; pass++)
            {
                if (IsDisposed || Disposing) return;
                var info = GetAccessibleScrollInfo();
                if (publishedDocumentScroll == info) return;
                publishedDocumentScroll = info;
                NotifyAccessibilityClients(AccessibleEvents.ScrollChanged);
            }
            throw new InvalidOperationException("Document viewport notifications did not stabilize after 64 changes.");
        }
        finally { documentScrollPublishing = false; }
    }
}
