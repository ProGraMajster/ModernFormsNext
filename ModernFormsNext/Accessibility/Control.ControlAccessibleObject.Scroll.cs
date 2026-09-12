using System.Drawing;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class Control
{
    public partial class ControlAccessibleObject
    {
        /// <inheritdoc/>
        public override AccessibleScrollInfo? ScrollInfo
            => Owner is { IsDisposed: false, Disposing: false } owner && !IsSensitive ? owner switch
            {
                ScrollableControl { AutoScroll: true } viewport => viewport.GetAccessibleScrollInfo(),
                ListBox list => list.GetAccessibleScrollInfo(),
                TreeView tree => tree.GetAccessibleScrollInfo(),
                TextBox text => text.GetAccessibleScrollInfo(),
                DocumentViewer viewer => viewer.GetAccessibleScrollInfo(),
                _ => null
            } : null;

        private static AccessibleActions GetScrollActions(Control owner)
        {
            var result = owner is ScrollableControl { AutoScroll: true } or ListBox or TreeView or TextBox or DocumentViewer ? AccessibleActions.Scroll : AccessibleActions.None;
            for (var current = owner.Parent; current is not null; current = current.Parent)
                if (current is ScrollableControl { AutoScroll: true }) return result | AccessibleActions.ScrollIntoView;
            return result;
        }

        private static AccessibleStates GetScrollState(Control owner)
        {
            Control? firstViewport = owner.Parent;
            while (firstViewport is not null && firstViewport is not ScrollableControl { AutoScroll: true })
                firstViewport = firstViewport.Parent;
            // Most semantic objects have no scrolling ancestor. Avoid mapping all four
            // presentation corners when no viewport can contribute an Offscreen state.
            if (firstViewport is null) return AccessibleStates.None;
            var visible = AccessibilityScrollGeometry.ToCanonicalBounds(owner, new(0, 0, owner.Width, owner.Height));
            for (var current = firstViewport; current is not null; current = current.Parent)
            {
                if (current is not ScrollableControl { AutoScroll: true } viewport) continue;
                visible = Rectangle.Intersect(visible, viewport.GetAccessibleScrollInfo().ViewportBounds);
                if (visible.Width <= 0 || visible.Height <= 0) return AccessibleStates.Offscreen;
            }
            return AccessibleStates.None;
        }

        private bool PerformScrollAction(AccessibleActions action, object? parameter)
        {
            if (Owner is not { IsDisposed: false, Enabled: true, Visible: true } owner) return false;
            if (action == AccessibleActions.Scroll)
                return parameter is AccessibleScrollRequest request && owner switch
                {
                    ScrollableControl viewport => viewport.PerformAccessibleScroll(request),
                    ListBox list => list.PerformAccessibleScroll(request),
                    TreeView tree => tree.PerformAccessibleScroll(request),
                    TextBox text => text.PerformAccessibleScroll(request),
                    DocumentViewer viewer => viewer.PerformAccessibleScroll(request),
                    _ => false
                };
            if (action != AccessibleActions.ScrollIntoView || parameter is not null) return false;
            var lifetime = new AccessibilityControlLifetime(owner);
            bool found = false;
            // The item remains the same throughout nested reveal; moving does not select/focus it.
            for (var current = owner.Parent; current is not null; current = current.Parent)
            {
                if (!lifetime.IsCurrent || !owner.Visible || !owner.Enabled) return false;
                if (current is not ScrollableControl { AutoScroll: true } viewport) continue;
                found = true;
                var bounds = BoundsInAncestor(owner, viewport);
                var rect = viewport.LogicalScrollViewport;
                var dx = RevealDelta(bounds.Left, bounds.Right, rect.Left, rect.Right);
                var dy = RevealDelta(bounds.Top, bounds.Bottom, rect.Top, rect.Bottom);
                if (!viewport.PerformAccessibleScroll(AccessibleScrollRequest.ByViewport(
                    rect.Width > 0 ? dx / rect.Width : 0, rect.Height > 0 ? dy / rect.Height : 0))) return false;
            }
            return found && lifetime.IsCurrent;
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
            var points = new[] { new PointF(0, 0), new PointF(owner.ScaledWidth, 0),
                new PointF(0, owner.ScaledHeight), new PointF(owner.ScaledWidth, owner.ScaledHeight) };
            for (Control? current = owner; !ReferenceEquals(current, ancestor); current = current.Parent)
            {
                if (current is null) return RectangleF.Empty;
                for (int i = 0; i < points.Length; i++) points[i] = current.ClientPointToParentPresentation(points[i]);
            }
            return RectangleF.FromLTRB(points.Min(p => p.X) / ancestor.ScaleFactor.Width,
                points.Min(p => p.Y) / ancestor.ScaleFactor.Height, points.Max(p => p.X) / ancestor.ScaleFactor.Width,
                points.Max(p => p.Y) / ancestor.ScaleFactor.Height);
        }
    }
}
