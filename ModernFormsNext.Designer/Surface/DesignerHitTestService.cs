using System.Drawing;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerHitTestService
{
    private const int ResizeHandleSize = 7;

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

        return HitTestControls(state.Document.Controls, layout, documentClip, documentPoint)
            ?? DesignerHitTestResult.Empty;
    }

    public DesignerResizeHandle HitTestResizeHandle(
        DesignerSession state,
        int surfaceWidth,
        int surfaceHeight,
        float surfaceX,
        float surfaceY)
    {
        var selectedNode = state.SelectedNode;

        if (selectedNode is null)
            return DesignerResizeHandle.None;

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

    public static Rectangle GetHandleBounds(Rectangle bounds, DesignerResizeHandle handle)
    {
        var half = ResizeHandleSize / 2;
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

        return new Rectangle(x - half, y - half, ResizeHandleSize, ResizeHandleSize);
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
        && x <= bounds.Right
        && y >= bounds.Top
        && y <= bounds.Bottom;

    private static DesignerHitTestResult? HitTestControls(
        DesignControlCollection controls,
        DesignerLayoutResult layout,
        DesignBounds parentClip,
        DesignPoint point)
    {
        for (var index = controls.Count - 1; index >= 0; index--)
        {
            var control = controls[index];
            var absoluteBounds = layout.GetEffectiveBounds(control);
            var visibleBounds = Intersect(absoluteBounds, parentClip);

            if (!visibleBounds.Contains(point.X, point.Y))
                continue;

            var childHit = HitTestControls(control.Children, layout, visibleBounds, point);

            if (childHit is not null)
                return childHit;

            if (absoluteBounds.Contains(point.X, point.Y))
                return new DesignerHitTestResult(control, absoluteBounds);
        }

        return null;
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
