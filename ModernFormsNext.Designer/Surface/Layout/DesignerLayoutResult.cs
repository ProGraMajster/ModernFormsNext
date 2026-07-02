using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerLayoutResult
{
    private readonly IReadOnlyDictionary<DesignControlNode, DesignBounds> effectiveBounds;

    public DesignerLayoutResult(IReadOnlyDictionary<DesignControlNode, DesignBounds> effectiveBounds)
    {
        this.effectiveBounds = effectiveBounds;
    }

    public DesignBounds GetEffectiveBounds(DesignControlNode node)
        => effectiveBounds.TryGetValue(node, out var bounds) ? bounds : node.Bounds;

    public bool TryGetEffectiveBounds(DesignControlNode node, out DesignBounds bounds)
        => effectiveBounds.TryGetValue(node, out bounds);

    public DesignBounds GetVisibleBounds(DesignDocument document, DesignControlNode node)
    {
        var visible = GetEffectiveBounds(node);

        foreach (var clip in GetAncestorClipBounds(document, node))
            visible = Intersect(visible, clip);

        return visible;
    }

    public bool IsPointInsideVisibleParentChain(DesignDocument document, DesignControlNode node, DesignPoint documentPoint)
        => GetAncestorClipBounds(document, node).All(bounds => bounds.Contains(documentPoint.X, documentPoint.Y));

    private IEnumerable<DesignBounds> GetAncestorClipBounds(DesignDocument document, DesignControlNode node)
    {
        yield return new DesignBounds(0, 0, Math.Max(1, document.Size.Width), Math.Max(1, document.Size.Height));

        if (!TryFindPath(document.Controls, node, path: [], out var nodePath))
            yield break;

        for (var index = 0; index < nodePath.Count - 1; index++)
            yield return GetEffectiveBounds(nodePath[index]);
    }

    private static bool TryFindPath(
        DesignControlCollection nodes,
        DesignControlNode target,
        List<DesignControlNode> path,
        out List<DesignControlNode> result)
    {
        foreach (var node in nodes)
        {
            path.Add(node);

            if (ReferenceEquals(node, target))
            {
                result = [.. path];
                path.RemoveAt(path.Count - 1);
                return true;
            }

            if (TryFindPath(node.Children, target, path, out result))
            {
                path.RemoveAt(path.Count - 1);
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        result = [];
        return false;
    }

    private static DesignBounds Intersect(DesignBounds first, DesignBounds second)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(first.Right, second.Right);
        var bottom = Math.Min(first.Bottom, second.Bottom);

        if (right <= left || bottom <= top)
            return new DesignBounds(left, top, 0, 0);

        return new DesignBounds(left, top, right - left, bottom - top);
    }
}
