using ModernFormsNext;
using ControlGallery.Examples;

namespace ControlGallery.Panels;

/// <summary>Displays the optional generated page composition without changing the default app template.</summary>
public sealed class PageDesignerPanel : BasePanel
{
    /// <summary>Creates a Gallery host for the authored UserControl and its normal production page tree.</summary>
    public PageDesignerPanel() => Controls.Add(new DesignedPagesView { Dock = DockStyle.Fill });
}
