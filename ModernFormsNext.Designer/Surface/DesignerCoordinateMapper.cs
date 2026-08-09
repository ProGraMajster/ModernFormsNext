using System.Drawing;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

/// <summary>
/// Maps logical coordinates between the design document and the logical designer surface.
/// </summary>
/// <remarks>
/// This mapper never applies monitor DPI. <see cref="DesignerSurfaceView.Scale"/> is only the
/// preview zoom needed to fit the logical document in the logical surface viewport. Device-pixel
/// conversion is performed separately by <see cref="DesignerDpiCoordinateConverter"/>.
/// </remarks>
internal sealed class DesignerCoordinateMapper
{
    private const int WorkspacePadding = 42;
    private const int FormTitleHeight = 28;
    private const int FormBorder = 3;

    public DesignerSurfaceView GetView(DesignerSession state, int width, int height)
    {
        var documentWidth = Math.Max(1, state.Document.Size.Width);
        var documentHeight = Math.Max(1, state.Document.Size.Height);
        var titleHeight = state.Document.RootKind == DesignRootKind.UserControl ? 0 : FormTitleHeight;
        var availableWidth = Math.Max(1, width - (WorkspacePadding * 2));
        var availableHeight = Math.Max(1, height - (WorkspacePadding * 2) - titleHeight);
        var scale = Math.Min(1f, Math.Min(availableWidth / (float)documentWidth, availableHeight / (float)documentHeight));
        var clientWidth = Math.Max(1, (int)Math.Round(documentWidth * scale));
        var clientHeight = Math.Max(1, (int)Math.Round(documentHeight * scale));
        var formWidth = clientWidth + (FormBorder * 2);
        var formHeight = titleHeight + clientHeight + (FormBorder * 2);
        var formX = Math.Max(WorkspacePadding, (width - formWidth) / 2);
        var formY = Math.Max(WorkspacePadding, (height - formHeight) / 2);

        return new DesignerSurfaceView(scale, formX, formY, titleHeight, FormBorder, clientWidth, clientHeight);
    }

    public bool TryMapToDocument(
        DesignerSession state,
        int width,
        int height,
        float x,
        float y,
        out DesignPoint point)
    {
        var view = GetView(state, width, height);

        if (x < view.ClientX || y < view.ClientY || x >= view.ClientX + view.ClientWidth || y >= view.ClientY + view.ClientHeight)
        {
            point = default;
            return false;
        }

        point = MapToDocument(view, x, y);
        return true;
    }

    public DesignPoint MapToDocument(DesignerSurfaceView view, float x, float y)
        => new(
            (int)Math.Floor((x - view.ClientX) / view.Scale),
            (int)Math.Floor((y - view.ClientY) / view.Scale));

    public Rectangle ToSurfaceBounds(DesignBounds bounds, DesignerSurfaceView view)
    {
        var left = view.ClientX + (int)Math.Round(bounds.X * view.Scale);
        var top = view.ClientY + (int)Math.Round(bounds.Y * view.Scale);
        var right = view.ClientX + (int)Math.Round(bounds.Right * view.Scale);
        var bottom = view.ClientY + (int)Math.Round(bounds.Bottom * view.Scale);

        // Scale edges rather than location and size independently. This gives adjacent controls,
        // selection borders, and resize handles one shared rounded boundary at fractional zoom.
        return Rectangle.FromLTRB(left, top, Math.Max(left + 1, right), Math.Max(top + 1, bottom));
    }

    public DesignBounds GetAbsoluteBounds(DesignDocument document, DesignControlNode node)
        => TryGetAbsoluteBounds(document.Controls, node, offsetX: 0, offsetY: 0, out var bounds)
            ? bounds
            : node.Bounds;

    public DesignBounds GetParentClientBounds(DesignDocument document, DesignControlNode node)
    {
        if (TryGetParentAbsoluteBounds(document.Controls, node, offsetX: 0, offsetY: 0, out var parentBounds))
            return parentBounds;

        return new DesignBounds(0, 0, Math.Max(1, document.Size.Width), Math.Max(1, document.Size.Height));
    }

    public DesignBounds GetVisibleClipBounds(DesignDocument document, DesignControlNode node)
    {
        var visible = GetAbsoluteBounds(document, node);

        foreach (var clip in GetAncestorClipBounds(document, node))
            visible = Intersect(visible, clip);

        return visible;
    }

    public bool IsPointInsideVisibleParentChain(DesignDocument document, DesignControlNode node, DesignPoint documentPoint)
        => GetAncestorClipBounds(document, node).All(bounds => bounds.Contains(documentPoint.X, documentPoint.Y));

    public DesignPoint PointToParent(DesignDocument document, DesignControlNode node, DesignPoint documentPoint)
    {
        if (TryGetParentAbsoluteBounds(document.Controls, node, offsetX: 0, offsetY: 0, out var parentBounds))
            return new DesignPoint(documentPoint.X - parentBounds.X, documentPoint.Y - parentBounds.Y);

        return documentPoint;
    }

    private static IEnumerable<DesignBounds> GetAncestorClipBounds(DesignDocument document, DesignControlNode node)
    {
        yield return new DesignBounds(0, 0, Math.Max(1, document.Size.Width), Math.Max(1, document.Size.Height));

        if (!TryFindPath(document.Controls, node, path: [], out var nodePath))
            yield break;

        var offsetX = 0;
        var offsetY = 0;

        for (var index = 0; index < nodePath.Count - 1; index++)
        {
            var ancestor = nodePath[index];
            var absolute = new DesignBounds(
                offsetX + ancestor.Bounds.X,
                offsetY + ancestor.Bounds.Y,
                ancestor.Bounds.Width,
                ancestor.Bounds.Height);

            yield return absolute;
            offsetX = absolute.X;
            offsetY = absolute.Y;
        }
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

    private static bool TryGetAbsoluteBounds(
        DesignControlCollection nodes,
        DesignControlNode target,
        int offsetX,
        int offsetY,
        out DesignBounds bounds)
    {
        foreach (var node in nodes)
        {
            var absolute = new DesignBounds(
                offsetX + node.Bounds.X,
                offsetY + node.Bounds.Y,
                node.Bounds.Width,
                node.Bounds.Height);

            if (ReferenceEquals(node, target))
            {
                bounds = absolute;
                return true;
            }

            if (TryGetAbsoluteBounds(node.Children, target, absolute.X, absolute.Y, out bounds))
                return true;
        }

        bounds = default;
        return false;
    }

    private static bool TryGetParentAbsoluteBounds(
        DesignControlCollection nodes,
        DesignControlNode target,
        int offsetX,
        int offsetY,
        out DesignBounds bounds)
    {
        foreach (var node in nodes)
        {
            var absolute = new DesignBounds(
                offsetX + node.Bounds.X,
                offsetY + node.Bounds.Y,
                node.Bounds.Width,
                node.Bounds.Height);

            if (node.Children.Any(child => ReferenceEquals(child, target)))
            {
                bounds = absolute;
                return true;
            }

            if (TryGetParentAbsoluteBounds(node.Children, target, absolute.X, absolute.Y, out bounds))
                return true;
        }

        bounds = default;
        return false;
    }
}
