using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class ScrollableControl
{
    /// <summary>Scrolls an explicit descendant into view through this container and intervening scrollable containers.</summary>
    /// <param name="target">A visible control with nonempty bounds in this container's explicit descendant tree.</param>
    /// <returns>
    /// <see langword="true"/> when the request completed, including an already visible target or a
    /// position constrained by the available scroll ranges. <see langword="false"/> when the target
    /// or receiver is unavailable, or when a callback retires the relationship during the operation.
    /// </returns>
    /// <remarks>
    /// Call on the owning UI thread. Movement is immediate, from the nearest enclosing viewport
    /// outward through this container. Each changed axis raises <see cref="Scroll"/> with
    /// <see cref="ScrollEventType.ThumbPosition"/>. The method preserves focus and selection.
    /// Disabled controls can be revealed programmatically. An implicit, hidden, disposed or unrelated
    /// target is unavailable, as is a receiver with AutoScroll disabled or an empty padded viewport.
    /// Bounds are compared in logical pixels against the viewport inset by borders, scrollbars and
    /// padding. An oversized target that already covers the viewport stays in place. For transformed
    /// or clipped controls, the rectangular bounds are an estimate of the visible content.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <example><code>
    /// // Reveal the first invalid field while focus stays on the validation summary.
    /// if (detailsPanel.ScrollControlIntoView(invalidField))
    ///     statusLabel.Text = "Check the highlighted field.";
    /// </code></example>
    public bool ScrollControlIntoView(Control target)
    {
        ArgumentNullException.ThrowIfNull(target);
        GetFocusScope().VerifyAccess();
        if (!CanReveal(target) || !HasExplicitPath(target)) return false;
        return RevealThroughAncestors(target, this);
    }

    private bool CanReveal(Control target)
    {
        if (IsDisposed || Disposing || !Visible || !AutoScroll ||
            target.IsDisposed || target.Disposing || !target.Visible || target.Width <= 0 || target.Height <= 0)
            return false;
        var viewport = PaddedScrollViewport;
        return viewport.Width > 0 && viewport.Height > 0;
    }

    private bool HasExplicitPath(Control target)
    {
        for (Control? child = target; child?.Parent is { } parent; child = parent)
        {
            if (child.ImplicitControl) return false;
            if (ReferenceEquals(parent, this)) return true;
        }
        return false;
    }

    private Rectangle PaddedScrollViewport => LayoutUtils.DeflateRect(LogicalScrollViewport, PresentationPadding);

    // Semantic callers also use this traversal for realized virtual rows and internal
    // navigation headers. Keep their virtual accessibility dispatch and full ancestor scope.
    internal static bool RevealThroughAncestors(Control target, ScrollableControl? boundary = null)
    {
        if (target.IsDisposed || target.Disposing || !target.Visible || boundary is null && !target.Enabled)
            return false;
        var lifetime = new AccessibilityControlLifetime(target);
        bool found = false;
        for (Control? current = target.Parent; current is not null; current = current.Parent)
        {
            if (!IsCurrent()) return false;
            if (current is ScrollableControl { AutoScroll: true } viewport)
            {
                found = true;
                if (boundary is null)
                {
                    var rect = viewport.LogicalScrollViewport;
                    var bounds = BoundsInAncestor(target, viewport);
                    var dx = RevealDelta(bounds.Left, bounds.Right, rect.Left, rect.Right);
                    var dy = RevealDelta(bounds.Top, bounds.Bottom, rect.Top, rect.Bottom);
                    if (!viewport.PerformAccessibleScroll(AccessibleScrollRequest.ByViewport(
                        rect.Width > 0 ? dx / rect.Width : 0, rect.Height > 0 ? dy / rect.Height : 0))) return false;
                }
                else
                {
                    viewport.RevealAxis(target, horizontal: true);
                    if (!IsCurrent() || !viewport.AutoScroll) return false;
                    // An observer can change layout during the first axis. Map the current
                    // bounds again for the second axis and for each outer viewport.
                    viewport.RevealAxis(target, horizontal: false);
                    if (!viewport.AutoScroll) return false;
                }
            }
            if (ReferenceEquals(current, boundary)) return IsCurrent();
        }
        return boundary is null && found && IsCurrent();

        bool IsCurrent() => lifetime.IsCurrent && target.Visible &&
            (boundary is null ? target.Enabled : boundary.CanReveal(target));
    }

    private void RevealAxis(Control target, bool horizontal)
    {
        ScrollBar bar = horizontal ? hscrollbar : vscrollbar;
        if (!bar.Visible || bar.Maximum <= bar.Minimum) return;
        var rect = PaddedScrollViewport;
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var bounds = BoundsInAncestor(target, this);
        var delta = horizontal
            ? RevealDelta(bounds.Left, bounds.Right, rect.Left, rect.Right)
            : RevealDelta(bounds.Top, bounds.Bottom, rect.Top, rect.Bottom);
        if (!double.IsFinite(delta)) return;
        // Round only the delta. A half logical pixel rounds to zero at either edge,
        // avoiding oscillation at fractional monitor scale and signed range minima.
        var value = (int)Math.Clamp(bar.Value + Math.Round(delta), bar.Minimum, bar.Maximum);
        if (value != bar.Value) bar.Value = value;
    }

    private static double RevealDelta(double start, double end, double viewStart, double viewEnd)
    {
        // A target larger than the viewport aligns its leading edge once; already covering
        // the complete view does not oscillate between its two edges on repeated requests.
        if (start <= viewStart && end >= viewEnd) return 0;
        if (start < viewStart) return start - viewStart;
        if (end > viewEnd) return Math.Min(start - viewStart, end - viewEnd);
        return 0;
    }

    private static RectangleF BoundsInAncestor(Control owner, Control ancestor)
    {
        Span<PointF> points = stackalloc PointF[] { PointF.Empty, new(owner.ScaledWidth, 0),
            new(0, owner.ScaledHeight), new(owner.ScaledWidth, owner.ScaledHeight) };
        for (Control? current = owner; !ReferenceEquals(current, ancestor); current = current.Parent)
        {
            if (current is null) return RectangleF.Empty;
            for (int i = 0; i < points.Length; i++) points[i] = current.ClientPointToParentPresentation(points[i]);
        }
        float left = points[0].X, right = left, top = points[0].Y, bottom = top;
        foreach (var point in points)
        {
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }
        var scale = ancestor.ScaleFactor;
        return RectangleF.FromLTRB(left / scale.Width, top / scale.Height, right / scale.Width, bottom / scale.Height);
    }
}
