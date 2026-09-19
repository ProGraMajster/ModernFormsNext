using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    /// <summary>Refreshes device-dependent measurements after the owning window's DPI changes.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>
    /// Called on the UI thread after the new window scale is available. Logical bounds, padding
    /// and font sizes are unchanged. The base method invalidates painting and requests layout;
    /// overrides must call base and discard any cached device-space text or geometry.
    /// </remarks>
    protected virtual void OnDpiChanged(EventArgs e)
    {
        Invalidate();
        PerformLayout();
    }

    internal void NotifyDpiChangedForSubtree()
    {
        FreeBackBuffer();
        CommonProperties.xClearPreferredSizeCache(this);
        OnDpiChanged(EventArgs.Empty);
        foreach (var child in Controls.GetAllControls().ToArray())
            child.NotifyDpiChangedForSubtree();
    }
}
