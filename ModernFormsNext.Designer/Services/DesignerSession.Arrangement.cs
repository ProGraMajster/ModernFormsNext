using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

public sealed partial class DesignerSession
{
    /// <summary>Aligns, sizes or distributes selected sibling controls as one undoable edit.</summary>
    /// <param name="arrangement">The requested arrangement.</param>
    /// <returns>Whether bounds changed; invalid or incompatible groups are left unchanged.</returns>
    /// <remarks>
    /// Call on the session's UI thread. At least two independently positioned siblings are required;
    /// distribution requires three. The primary selection supplies alignment and size. Managed
    /// layouts, selected ancestor/descendant pairs and incompatible size constraints reject the
    /// complete edit. Distribution sorts by position then outline order, preserves both outside
    /// edges and allocates integer remainders from the leading edge (including negative gaps).
    /// A committed model notification updates preview layout and rendering; selection is retained.
    /// </remarks>
    /// <example><code>
    /// session.Host.Selection.SelectNodes(new[] { firstButton, secondButton }, firstButton);
    /// session.ArrangeSelectedNodes(DesignerGroupArrangement.AlignLeft);
    /// </code></example>
    public bool ArrangeSelectedNodes(DesignerGroupArrangement arrangement)
    {
        ThrowIfDisposed();
        if (!Enum.IsDefined(arrangement)) throw new ArgumentOutOfRangeException(nameof(arrangement));
        if (!TryGetManualSelectedRoots(out var roots, out var error)) { Log(error!); return false; }
        bool distribute = arrangement is DesignerGroupArrangement.DistributeHorizontally or DesignerGroupArrangement.DistributeVertically;
        if (roots.Count < (distribute ? 3 : 2) || roots.Count != SelectedNodes.Count
            || roots.Select(FindParent).Distinct<DesignControlNode?>(ReferenceEqualityComparer.Instance).Take(2).Count() != 1)
        { Log("Arrange requires independently selected siblings (three or more for distribution)."); return false; }
        var reference = SelectedNode!.Bounds;
        bool resize = arrangement is DesignerGroupArrangement.SameWidth or DesignerGroupArrangement.SameHeight or DesignerGroupArrangement.SameSize;
        if (resize && roots.Any(node => node.Properties.TryGetValue("AutoSize", out var value) && value.Value is true))
        { Log("Turn off AutoSize before setting a shared explicit size."); return false; }
        var plan = new List<(DesignControlNode Node, DesignBounds Bounds)>(roots.Count);
        if (distribute)
        {
            bool horizontal = arrangement == DesignerGroupArrangement.DistributeHorizontally;
            // Stable ordering keeps ties in outline order, independently of click order.
            var ordered = roots.OrderBy(node => horizontal ? node.Bounds.X : node.Bounds.Y).ToArray();
            long start = horizontal ? ordered[0].Bounds.X : ordered[0].Bounds.Y;
            long end = horizontal ? (long)ordered[^1].Bounds.X + ordered[^1].Bounds.Width
                : (long)ordered[^1].Bounds.Y + ordered[^1].Bounds.Height;
            long free = end - start - ordered.Sum(node => (long)(horizontal ? node.Bounds.Width : node.Bounds.Height));
            long preceding = 0;
            for (int index = 0; index < ordered.Length; index++)
            {
                var node = ordered[index];
                long numerator = free * index;
                long gap = numerator / (ordered.Length - 1);
                if (numerator < 0 && numerator % (ordered.Length - 1) != 0) gap--;
                long location = start + preceding + gap;
                if (!TryTranslateBounds(node.Bounds, horizontal ? location - node.Bounds.X : 0,
                    horizontal ? 0 : location - node.Bounds.Y, out var bounds))
                { Log("Distribution exceeds the supported coordinate range."); return false; }
                plan.Add((node, bounds));
                preceding += horizontal ? node.Bounds.Width : node.Bounds.Height;
            }
        }
        else
        {
            foreach (var node in roots)
            {
                var old = node.Bounds;
                long x = old.X, y = old.Y;
                int width = old.Width, height = old.Height;
                switch (arrangement)
                {
                    case DesignerGroupArrangement.AlignLeft: x = reference.X; break;
                    case DesignerGroupArrangement.AlignRight: x = (long)reference.X + reference.Width - width; break;
                    case DesignerGroupArrangement.AlignTop: y = reference.Y; break;
                    case DesignerGroupArrangement.AlignBottom: y = (long)reference.Y + reference.Height - height; break;
                    case DesignerGroupArrangement.SameWidth: width = reference.Width; break;
                    case DesignerGroupArrangement.SameHeight: height = reference.Height; break;
                    case DesignerGroupArrangement.SameSize: width = reference.Width; height = reference.Height; break;
                }
                if (!TryTranslateBounds(new DesignBounds(0, 0, width, height), x, y, out var bounds)
                    || resize && DesignerLayoutProperties.ApplySizeConstraints(node, bounds) != bounds)
                { Log("The arrangement exceeds a member's size or coordinate constraints."); return false; }
                plan.Add((node, bounds));
            }
        }
        return ApplyGroupBounds(plan, $"Arrange selected controls: {arrangement}");
    }
}
