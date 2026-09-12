using System.Drawing;
using System.Runtime.ExceptionServices;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Renderers;

namespace ModernFormsNext;

public partial class LinkLabel
{
    private bool activatingLink;
    internal bool IsAccessibilityLinkAttached(Link link)
        => !IsDisposed && !Disposing && ReferenceEquals(link.Owner, this) && Links.Contains(link)
            && TryGetAccessibilityLinkRange(link, out _, out _);

    private bool TryGetAccessibilityLinkRange(Link link, out int start, out int length)
    {
        var text = Text ?? string.Empty;
        start = Math.Clamp(link.Start, 0, text.Length);
        var end = (int)Math.Clamp((long)link.Start + link.Length, start, text.Length);
        // Link offsets are UTF-16. An invalid partial-surrogate range must not expose malformed
        // text or claim the neighboring rendered scalar as an independently actionable link.
        if (start > 0 && start < text.Length && char.IsLowSurrogate(text[start]) && char.IsHighSurrogate(text[start - 1])) start++;
        if (end > 0 && end < text.Length && char.IsLowSurrogate(text[end]) && char.IsHighSurrogate(text[end - 1])) end--;
        length = Math.Max(0, end - start);
        return length > 0;
    }

    internal string? GetAccessibilityLinkText(Link link)
        => TryGetAccessibilityLinkRange(link, out int start, out int length) ? Text.Substring(start, length) : null;

    internal void EnsureAccessibilityLinkLayout()
        => RenderManager.GetRenderer<LinkLabelRenderer>()?.EnsureLayoutCache(this);

    private bool IsCurrentAccessibilityLink(Link link, Control? parent, WindowBase? window)
        => IsAccessibilityLinkAttached(link) && Enabled && Visible && link.Enabled
            && ReferenceEquals(Parent, parent) && ReferenceEquals(FindWindow(), window)
            && window?.InputBindingsClosed != true;

    private bool IsCurrentLinkInput(AccessibilityControlLifetime lifetime)
        => lifetime.IsCurrent && !Disposing && Enabled && Visible && FindWindow()?.InputBindingsClosed != true;

    internal bool ActivateAccessibilityLink(Link link) => ActivateLink(link, MouseButtons.Left);

    internal bool FocusAccessibilityLink(Link link)
    {
        if (!IsAccessibilityLinkAttached(link) || !Enabled || !Visible || !link.Enabled) return false;
        var lifetime = new AccessibilityControlLifetime(this);
        var parent = Parent;
        var window = FindWindow();
        Select();
        if (!lifetime.IsCurrent || !IsCurrentAccessibilityLink(link, parent, window) || !Focused) return false;
        FocusLink = link;
        return lifetime.IsCurrent && IsCurrentAccessibilityLink(link, parent, window) && Focused && ReferenceEquals(FocusLink, link);
    }

    internal void NotifyAccessibilityLinkChanged(Link? link, AccessibleEvents kind)
    {
        if (!IsAccessibilityObjectCreated || IsDisposed || Disposing) return;
        var index = 0;
        foreach (var item in Links)
        {
            if (!IsAccessibilityLinkAttached(item)) continue;
            index++;
            if (ReferenceEquals(item, link)) { NotifyAccessibilityClients(kind, index); return; }
        }
        NotifyAccessibilityClients(kind);
    }

    internal void OnLinkMetadataChanged(Link? link, AccessibleEvents kind, bool layout = false, bool structure = false)
    {
        if (layout) InvalidateLayout();
        UpdateSelectability();
        Exception? failure = null;
        try { Invalidate(); }
        catch (Exception exception) { failure = exception; }
        try
        {
            if (structure) NotifyAccessibilityClients(AccessibleEvents.Reorder);
            NotifyAccessibilityLinkChanged(link, kind);
        }
        catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <inheritdoc/>
    protected override void OnFontChanged(EventArgs e)
    {
        InvalidateLayout();
        base.OnFontChanged(e);
        NotifyAccessibilityClients(AccessibleEvents.LocationChange);
    }

    /// <inheritdoc/>
    protected internal override void OnThemeChanged(EventArgs e)
    {
        InvalidateLayout();
        base.OnThemeChanged(e);
        NotifyAccessibilityClients(AccessibleEvents.LocationChange);
    }
}
