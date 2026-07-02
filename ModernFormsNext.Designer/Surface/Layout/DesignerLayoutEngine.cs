using ModernFormsNext;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerLayoutEngine
{
    public DesignerLayoutResult Layout(DesignDocument document)
    {
        var bounds = new Dictionary<DesignControlNode, DesignBounds>();
        var documentClientBounds = new DesignBounds(0, 0, Math.Max(1, document.Size.Width), Math.Max(1, document.Size.Height));

        LayoutChildren(document.Controls, documentClientBounds, bounds);

        return new DesignerLayoutResult(bounds);
    }

    private static void LayoutChildren(
        DesignControlCollection children,
        DesignBounds parentClientBounds,
        IDictionary<DesignControlNode, DesignBounds> bounds)
    {
        var remaining = new DesignBounds(0, 0, Math.Max(0, parentClientBounds.Width), Math.Max(0, parentClientBounds.Height));

        foreach (var child in children)
        {
            var localBounds = GetLocalBounds(child, remaining);
            var absoluteBounds = new DesignBounds(
                parentClientBounds.X + localBounds.X,
                parentClientBounds.Y + localBounds.Y,
                Math.Max(0, localBounds.Width),
                Math.Max(0, localBounds.Height));

            bounds[child] = absoluteBounds;
            remaining = ConsumeDockSpace(child, remaining, localBounds);

            if (child.Children.Count > 0)
            {
                var childClientBounds = new DesignBounds(
                    absoluteBounds.X,
                    absoluteBounds.Y,
                    Math.Max(0, absoluteBounds.Width),
                    Math.Max(0, absoluteBounds.Height));

                LayoutChildren(child.Children, childClientBounds, bounds);
            }
        }
    }

    private static DesignBounds GetLocalBounds(DesignControlNode node, DesignBounds remaining)
    {
        var dock = DesignerLayoutProperties.GetDock(node);
        var width = Math.Max(0, node.Bounds.Width);
        var height = Math.Max(0, node.Bounds.Height);

        return dock switch
        {
            DockStyle.Top => new DesignBounds(remaining.X, remaining.Y, remaining.Width, Math.Min(height, remaining.Height)),
            DockStyle.Bottom => new DesignBounds(remaining.X, remaining.Bottom - Math.Min(height, remaining.Height), remaining.Width, Math.Min(height, remaining.Height)),
            DockStyle.Left => new DesignBounds(remaining.X, remaining.Y, Math.Min(width, remaining.Width), remaining.Height),
            DockStyle.Right => new DesignBounds(remaining.Right - Math.Min(width, remaining.Width), remaining.Y, Math.Min(width, remaining.Width), remaining.Height),
            DockStyle.Fill => remaining,
            _ => node.Bounds
        };
    }

    private static DesignBounds ConsumeDockSpace(
        DesignControlNode node,
        DesignBounds remaining,
        DesignBounds usedBounds)
    {
        return DesignerLayoutProperties.GetDock(node) switch
        {
            DockStyle.Top => new DesignBounds(remaining.X, remaining.Y + usedBounds.Height, remaining.Width, Math.Max(0, remaining.Height - usedBounds.Height)),
            DockStyle.Bottom => new DesignBounds(remaining.X, remaining.Y, remaining.Width, Math.Max(0, remaining.Height - usedBounds.Height)),
            DockStyle.Left => new DesignBounds(remaining.X + usedBounds.Width, remaining.Y, Math.Max(0, remaining.Width - usedBounds.Width), remaining.Height),
            DockStyle.Right => new DesignBounds(remaining.X, remaining.Y, Math.Max(0, remaining.Width - usedBounds.Width), remaining.Height),
            DockStyle.Fill => new DesignBounds(remaining.X, remaining.Y, 0, 0),
            _ => remaining
        };
    }
}
