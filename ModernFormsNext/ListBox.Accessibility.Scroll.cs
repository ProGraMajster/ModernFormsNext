using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class ListBox
{
    private bool updatingListScroll;
    private bool listScrollPending;
    private bool publishingListScroll;
    private AccessibleScrollInfo? publishedListScroll;
    internal AccessibleScrollInfo GetAccessibleScrollInfo() => ItemViewportAccessibility.Read(this, vscrollbar, ScaledItemHeight);
    internal bool PerformAccessibleScroll(AccessibleScrollRequest request)
        => ItemViewportAccessibility.Perform(this, vscrollbar, ScaledItemHeight, request);

    private void NotifyListScrollChanged()
    {
        if (updatingListScroll || publishingListScroll || IsDisposed || Disposing) return;
        publishingListScroll = true;
        try
        {
            for (int pass = 0; pass < 64; pass++)
            {
                if (IsDisposed || Disposing) return;
                var info = GetAccessibleScrollInfo();
                if (publishedListScroll == info) return;
                publishedListScroll = info;
                NotifyAccessibilityClients(AccessibleEvents.ScrollChanged);
            }
            throw new InvalidOperationException("List viewport notifications did not stabilize after 64 changes.");
        }
        finally { publishingListScroll = false; }
    }
}
