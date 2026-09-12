using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class TreeView
{
    private bool updatingTreeScroll;
    private bool treeScrollPending;
    private bool publishingTreeScroll;
    private AccessibleScrollInfo? publishedTreeScroll;
    internal void UpdateViewportAfterItemChange()
    {
        if (!IsDisposed && !Disposing && root_item is not null) UpdateVerticalScrollBar();
    }
    internal AccessibleScrollInfo GetAccessibleScrollInfo() => ItemViewportAccessibility.Read(this, vscrollbar, ScaledItemHeight);
    internal bool PerformAccessibleScroll(AccessibleScrollRequest request)
        => ItemViewportAccessibility.Perform(this, vscrollbar, ScaledItemHeight, request);

    private void NotifyTreeScrollChanged()
    {
        if (updatingTreeScroll || publishingTreeScroll || IsDisposed || Disposing) return;
        publishingTreeScroll = true;
        try
        {
            for (int pass = 0; pass < 64; pass++)
            {
                if (IsDisposed || Disposing) return;
                var info = GetAccessibleScrollInfo();
                if (publishedTreeScroll == info) return;
                publishedTreeScroll = info;
                NotifyAccessibilityClients(AccessibleEvents.ScrollChanged);
            }
            throw new InvalidOperationException("Tree viewport notifications did not stabilize after 64 changes.");
        }
        finally { publishingTreeScroll = false; }
    }
}
