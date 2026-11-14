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
            return RevealControlIntoView(owner);
        }

        // The same canonical nested reveal is used by a virtualized logical item after realization.
        // Its private hit/focus container may be Hidden to avoid duplicate semantic peers; the
        // logical peer already checked its own supported actions before invoking this operation.
        internal static bool RevealControlIntoView(Control owner)
            => ScrollableControl.RevealThroughAncestors(owner);
    }
}
