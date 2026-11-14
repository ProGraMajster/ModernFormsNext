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
    private Point publishedViewportPosition;
    private Size publishedViewportSize;
    // Notification history only. Movement remains owned by scroll_position/ScrollWindow.
    private Point publishedScrollPosition;
    private long scrollPositionVersion;
    private ScrollEventType pendingHorizontalType = ScrollEventType.ThumbPosition;
    private ScrollEventType pendingVerticalType = ScrollEventType.ThumbPosition;

    /// <summary>Refreshes derived content after the canonical logical scroll position or viewport size changes.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>
    /// Runs on the UI thread after a complete scrollbar update, including programmatic values and range clamps.
    /// Position is already committed and child displacement complete. Overrides may request existing layout;
    /// reentrant changes are coalesced by the normal bounded scroll publication loop. Call base.
    /// </remarks>
    protected virtual void OnScrollViewportChanged(EventArgs e) { }

    private void HandleScrollMetadata(object? sender, EventArgs e)
    {
        scroll_update_depth++;
        Exception? failure = null;
        try {
            if (sender == hscrollbar && hscrollbar.DesiredVisibility)
                ScrollWindow(hscrollbar.Value - hscrollbar.Minimum - scroll_position.X, 0);
            else if (sender == vscrollbar && vscrollbar.DesiredVisibility)
                ScrollWindow(0, vscrollbar.Value - vscrollbar.Minimum - scroll_position.Y);
        }
        catch (Exception exception) { failure = exception; throw; }
        finally { EndScrollUpdate(failure); }
    }

    internal Rectangle LogicalScrollViewport
    {
        get
        {
            var rect = base.DisplayRectangle;
            if (vscrollbar.DesiredVisibility) rect.Width -= vscrollbar.Width;
            if (hscrollbar.DesiredVisibility) rect.Height -= hscrollbar.Height;
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

    internal virtual bool PerformAccessibleScroll(AccessibleScrollRequest request)
    {
        if (IsDisposed || !Enabled || !Visible || !AutoScroll) return false;
        var lifetime = new AccessibilityControlLifetime(this);
        var info = GetAccessibleScrollInfo();
        if (!request.TryGetOffset(info.Horizontal, true, out var horizontal)
            || !request.TryGetOffset(info.Vertical, false, out var vertical)
            || horizontal.HasValue && !hscrollbar.Enabled || vertical.HasValue && !vscrollbar.Enabled) return false;
        // Validate both axes before movement, then finish each canonical commit. Public and
        // accessibility observers of H can retire the target before V: retain that established
        // action/lifetime boundary instead of evaluating success before a deferred finally flush.
        if (horizontal.HasValue && !SetAxis(hscrollbar, horizontal.Value)) return false;
        if (!lifetime.IsCurrent || !Enabled || !Visible || !AutoScroll) return false;
        if (vertical.HasValue && !SetAxis(vscrollbar, vertical.Value)) return false;
        return lifetime.IsCurrent && Enabled && Visible && AutoScroll;

        bool SetAxis(ScrollBar bar, double value)
        {
            if (!lifetime.IsCurrent || bar.IsDisposed || !bar.Visible || !bar.Enabled) return false;
            int target = (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), bar.Minimum, bar.Maximum);
            if (bar.Value == target) return true;
            bool isHorizontal = ReferenceEquals(bar, hscrollbar);
            var amount = isHorizontal ? request.HorizontalAmount : request.VerticalAmount;
            var type = request.Kind switch {
                AccessibleScrollRequestKind.Amount => amount switch {
                    AccessibleScrollAmount.SmallDecrement => ScrollEventType.SmallDecrement,
                    AccessibleScrollAmount.SmallIncrement => ScrollEventType.SmallIncrement,
                    AccessibleScrollAmount.LargeDecrement => ScrollEventType.LargeDecrement,
                    AccessibleScrollAmount.LargeIncrement => ScrollEventType.LargeIncrement,
                    _ => ScrollEventType.ThumbPosition
                },
                AccessibleScrollRequestKind.Viewport => target < bar.Value
                    ? ScrollEventType.LargeDecrement : ScrollEventType.LargeIncrement,
                _ => ScrollEventType.ThumbPosition
            };
            bar.SetValue(target, type);
            if (!lifetime.IsCurrent || !Enabled || !Visible) return false;
            return lifetime.IsCurrent;
        }
    }

    // A publication failure must not replace the exception from movement/layout which caused
    // the finally block to run. Successful publication lets that original exception propagate.
    private void EndScrollUpdate(Exception? precedingFailure)
    {
        try { EndScrollUpdate(); }
        catch (Exception exception) when (precedingFailure is not null) {
            throw new AggregateException(precedingFailure, exception);
        }
    }

    private void EndScrollUpdate()
    {
        if (--scroll_update_depth != 0 || publishing_scroll || IsDisposed || Disposing) return;
        publishing_scroll = true;
        List<Exception>? failures = null;
        try {
            var lifetime = new AccessibilityControlLifetime(this);
            int pass = 0;
            for (; pass < 64; pass++) {
                if (!lifetime.IsCurrent) break;
                var viewport = LogicalScrollViewport;
                if (publishedViewportPosition != scroll_position || publishedViewportSize != viewport.Size)
                {
                    publishedViewportPosition = scroll_position;
                    publishedViewportSize = viewport.Size;
                    try { OnScrollViewportChanged(EventArgs.Empty); }
                    catch (Exception exception) { (failures ??= []).Add(exception); }
                    if (!lifetime.IsCurrent) break;
                    if (publishedViewportPosition != scroll_position || publishedViewportSize != LogicalScrollViewport.Size)
                        continue;
                }
                var current = GetAccessibleScrollInfo();
                if (published_scroll != current) {
                    published_scroll = current;
                    try { NotifyAccessibilityClients(AccessibleEvents.ScrollChanged); }
                    catch (Exception exception) { (failures ??= []).Add(exception); }
                    continue;
                }
                // Publish one current axis per pass. An H callback can replace V (or H itself);
                // refresh derived content before dispatching another event, never reuse stale args.
                ScrollEventArgs? change = null;
                if (publishedScrollPosition.X != scroll_position.X) {
                    change = new(pendingHorizontalType, publishedScrollPosition.X, scroll_position.X, ScrollOrientation.HorizontalScroll);
                    publishedScrollPosition.X = scroll_position.X;
                } else if (publishedScrollPosition.Y != scroll_position.Y) {
                    change = new(pendingVerticalType, publishedScrollPosition.Y, scroll_position.Y, ScrollOrientation.VerticalScroll);
                    publishedScrollPosition.Y = scroll_position.Y;
                }
                if (change is null) break;
                try { OnScroll(change); }
                catch (Exception exception) { (failures ??= []).Add(exception); }
            }
            if (pass == 64)
                (failures ??= []).Add(new InvalidOperationException("Viewport notifications did not stabilize after 64 changes."));
        } finally { publishing_scroll = false; }
        if (failures is not null) throw new AggregateException(failures);
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
