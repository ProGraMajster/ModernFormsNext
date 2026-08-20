using System.Drawing;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerHitTestService
{
    internal const int ResizeHandleSize = 7;

    private readonly DesignerCoordinateMapper coordinateMapper;
    private readonly DesignerLayoutEngine layoutEngine = new();

    public DesignerHitTestService(DesignerCoordinateMapper coordinateMapper)
    {
        this.coordinateMapper = coordinateMapper;
    }

    public DesignerHitTestResult HitTestControl(DesignerSession state, DesignPoint documentPoint)
    {
        var layout = layoutEngine.Layout(state.Document);
        var documentClip = new DesignBounds(0, 0, Math.Max(1, state.Document.Size.Width), Math.Max(1, state.Document.Size.Height));

        return HitTestControls(state, state.Document.Controls, parentNode: null, layout, documentClip, documentPoint)
            ?? DesignerHitTestResult.Empty;
    }

    public DesignControlNode? HitTestSplitter(DesignerSession state, DesignPoint documentPoint)
    {
        var layout = layoutEngine.Layout(state.Document);
        var documentClip = new DesignBounds(0, 0, Math.Max(1, state.Document.Size.Width), Math.Max(1, state.Document.Size.Height));

        return HitTestSplitters(state, state.Document.Controls, parentNode: null, layout, documentClip, documentPoint);
    }

    public DesignControlNode? HitTestTabHeader(DesignerSession state, DesignPoint documentPoint, out int tabIndex)
    {
        var layout = layoutEngine.Layout(state.Document);
        var documentClip = new DesignBounds(0, 0, Math.Max(1, state.Document.Size.Width), Math.Max(1, state.Document.Size.Height));

        return HitTestTabHeaders(state, state.Document.Controls, parentNode: null, layout, documentClip, documentPoint, out tabIndex);
    }

    public DesignerResizeHandle HitTestResizeHandle(
        DesignerSession state,
        int surfaceWidth,
        int surfaceHeight,
        float surfaceX,
        float surfaceY)
    {
        var selectedNode = state.SelectedNode;

        if (selectedNode is not null && !DesignerLayoutProperties.IsVisible(selectedNode))
            return DesignerResizeHandle.None;

        if (selectedNode is null)
        {
            if (state.Document.RootKind != DesignRootKind.UserControl)
                return DesignerResizeHandle.None;

            var rootView = coordinateMapper.GetView(state, surfaceWidth, surfaceHeight);
            var rootBounds = new Rectangle(rootView.FormX, rootView.FormY, rootView.FormWidth, rootView.FormHeight);

            foreach (var rootHandle in GetRootHandles())
            {
                if (Contains(GetHandleBounds(rootBounds, rootHandle), surfaceX, surfaceY))
                    return rootHandle;
            }

            return DesignerResizeHandle.None;
        }

        var view = coordinateMapper.GetView(state, surfaceWidth, surfaceHeight);
        var layout = layoutEngine.Layout(state.Document);
        var bounds = coordinateMapper.ToSurfaceBounds(layout.GetEffectiveBounds(selectedNode), view);

        foreach (var handle in GetHandlesInHitTestOrder())
        {
            if (!DesignerLayoutProperties.CanResize(selectedNode, handle))
                continue;

            if (!Contains(GetHandleBounds(bounds, handle), surfaceX, surfaceY))
                continue;

            var documentPoint = coordinateMapper.MapToDocument(view, surfaceX, surfaceY);

            return layout.IsPointInsideVisibleParentChain(state.Document, selectedNode, documentPoint)
                ? handle
                : DesignerResizeHandle.None;
        }

        return DesignerResizeHandle.None;
    }

    public static IEnumerable<DesignerResizeHandle> GetHandles()
    {
        yield return DesignerResizeHandle.TopLeft;
        yield return DesignerResizeHandle.Top;
        yield return DesignerResizeHandle.TopRight;
        yield return DesignerResizeHandle.Right;
        yield return DesignerResizeHandle.BottomRight;
        yield return DesignerResizeHandle.Bottom;
        yield return DesignerResizeHandle.BottomLeft;
        yield return DesignerResizeHandle.Left;
    }

    public static IEnumerable<DesignerResizeHandle> GetRootHandles()
    {
        yield return DesignerResizeHandle.Right;
        yield return DesignerResizeHandle.BottomRight;
        yield return DesignerResizeHandle.Bottom;
    }

    public static Rectangle GetHandleBounds(
        Rectangle bounds,
        DesignerResizeHandle handle,
        int handleSize = ResizeHandleSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(handleSize);

        var half = handleSize / 2;
        var x = handle switch
        {
            DesignerResizeHandle.TopLeft or DesignerResizeHandle.Left or DesignerResizeHandle.BottomLeft => bounds.Left,
            DesignerResizeHandle.Top or DesignerResizeHandle.Bottom => bounds.Left + bounds.Width / 2,
            DesignerResizeHandle.TopRight or DesignerResizeHandle.Right or DesignerResizeHandle.BottomRight => bounds.Right,
            _ => bounds.Right
        };
        var y = handle switch
        {
            DesignerResizeHandle.TopLeft or DesignerResizeHandle.Top or DesignerResizeHandle.TopRight => bounds.Top,
            DesignerResizeHandle.Left or DesignerResizeHandle.Right => bounds.Top + bounds.Height / 2,
            DesignerResizeHandle.BottomLeft or DesignerResizeHandle.Bottom or DesignerResizeHandle.BottomRight => bounds.Bottom,
            _ => bounds.Bottom
        };

        return new Rectangle(x - half, y - half, handleSize, handleSize);
    }

    private static IEnumerable<DesignerResizeHandle> GetHandlesInHitTestOrder()
    {
        yield return DesignerResizeHandle.TopLeft;
        yield return DesignerResizeHandle.TopRight;
        yield return DesignerResizeHandle.BottomRight;
        yield return DesignerResizeHandle.BottomLeft;
        yield return DesignerResizeHandle.Top;
        yield return DesignerResizeHandle.Right;
        yield return DesignerResizeHandle.Bottom;
        yield return DesignerResizeHandle.Left;
    }

    private static bool Contains(Rectangle bounds, float x, float y)
        => x >= bounds.Left
        && x < bounds.Right
        && y >= bounds.Top
        && y < bounds.Bottom;

    private static DesignerHitTestResult? HitTestControls(
        DesignerSession state,
        IEnumerable<DesignControlNode> controls,
        DesignControlNode? parentNode,
        DesignerLayoutResult layout,
        DesignBounds parentClip,
        DesignPoint point)
    {
        var orderedControls = new List<DesignControlNode>(controls);

        foreach (var index in GetFrontToBackIndices(orderedControls.Count, parentNode))
        {
            var control = orderedControls[index];
            if (!DesignerLayoutProperties.IsVisible(control))
                continue;

            var absoluteBounds = layout.GetEffectiveBounds(control);
            var visibleBounds = Intersect(absoluteBounds, parentClip);

            if (!visibleBounds.Contains(point.X, point.Y))
                continue;

            var childHit = state.IsProjectUserControlType(control.TypeName)
                ? null
                : HitTestControls(state, GetHitTestChildren(control), control, layout, visibleBounds, point);

            if (childHit is not null)
                return childHit;

            if (absoluteBounds.Contains(point.X, point.Y))
                return new DesignerHitTestResult(control, absoluteBounds);
        }

        return null;
    }

    private static IEnumerable<DesignControlNode> GetHitTestChildren(DesignControlNode node)
    {
        if (DesignerSpecialContainers.IsTabControl(node))
        {
            if (DesignerSpecialContainers.GetSelectedTabPage(node) is { } page)
                yield return page;

            yield break;
        }

        foreach (var child in node.Children)
            yield return child;
    }

    private static DesignControlNode? HitTestSplitters(
        DesignerSession state,
        IEnumerable<DesignControlNode> controls,
        DesignControlNode? parentNode,
        DesignerLayoutResult layout,
        DesignBounds parentClip,
        DesignPoint point)
    {
        var orderedControls = new List<DesignControlNode>(controls);

        foreach (var index in GetFrontToBackIndices(orderedControls.Count, parentNode))
        {
            var control = orderedControls[index];
            if (!DesignerLayoutProperties.IsVisible(control))
                continue;

            var absoluteBounds = layout.GetEffectiveBounds(control);
            var visibleBounds = Intersect(absoluteBounds, parentClip);

            if (!visibleBounds.Contains(point.X, point.Y))
                continue;

            if (DesignerSpecialContainers.IsSplitContainer(control)
                && Intersect(DesignerSpecialContainers.GetSplitterBounds(control, absoluteBounds), visibleBounds).Contains(point.X, point.Y))
            {
                return control;
            }

            var childHit = state.IsProjectUserControlType(control.TypeName)
                ? null
                : HitTestSplitters(state, GetHitTestChildren(control), control, layout, visibleBounds, point);

            if (childHit is not null)
                return childHit;
        }

        return null;
    }

    private static DesignControlNode? HitTestTabHeaders(
        DesignerSession state,
        IEnumerable<DesignControlNode> controls,
        DesignControlNode? parentNode,
        DesignerLayoutResult layout,
        DesignBounds parentClip,
        DesignPoint point,
        out int tabIndex)
    {
        var orderedControls = new List<DesignControlNode>(controls);

        foreach (var index in GetFrontToBackIndices(orderedControls.Count, parentNode))
        {
            var control = orderedControls[index];
            if (!DesignerLayoutProperties.IsVisible(control))
                continue;

            var absoluteBounds = layout.GetEffectiveBounds(control);
            var visibleBounds = Intersect(absoluteBounds, parentClip);

            if (!visibleBounds.Contains(point.X, point.Y))
                continue;

            if (DesignerSpecialContainers.IsTabControl(control)
                && TryHitTabHeader(control, absoluteBounds, point, out tabIndex))
            {
                return control;
            }

            if (!state.IsProjectUserControlType(control.TypeName))
            {
                var childHit = HitTestTabHeaders(state, GetHitTestChildren(control), control, layout, visibleBounds, point, out tabIndex);

                if (childHit is not null)
                    return childHit;
            }
        }

        tabIndex = -1;
        return null;
    }

    private static IEnumerable<int> GetFrontToBackIndices(int count, DesignControlNode? parentNode)
    {
        if (parentNode is not null && PreservesSequentialChildOrder(parentNode))
        {
            for (var index = count - 1; index >= 0; index--)
                yield return index;

            yield break;
        }

        for (var index = 0; index < count; index++)
            yield return index;
    }

    private static bool PreservesSequentialChildOrder(DesignControlNode node)
        => DesignerSpecialContainers.IsFlowLayoutPanel(node)
        || DesignerSpecialContainers.IsTableLayoutPanel(node)
        || DesignerSpecialContainers.IsTabControl(node);

    private static bool TryHitTabHeader(
        DesignControlNode tabControl,
        DesignBounds bounds,
        DesignPoint point,
        out int tabIndex)
    {
        var x = bounds.X + 4;
        var y = bounds.Y + 2;
        const int headerHeight = 24;

        for (var index = 0; index < tabControl.Children.Count; index++)
        {
            var page = tabControl.Children[index];

            if (!DesignerSpecialContainers.IsTabPage(page))
                continue;

            var text = DesignerSpecialContainers.TryGetString(page, "Text", out var pageText) ? pageText : page.Name;
            var width = Math.Clamp(48 + (text.Length * 6), 64, 140);
            var header = new DesignBounds(x, y, width, headerHeight);

            if (header.Contains(point.X, point.Y))
            {
                tabIndex = index;
                return true;
            }

            x += width;
        }

        tabIndex = -1;
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
