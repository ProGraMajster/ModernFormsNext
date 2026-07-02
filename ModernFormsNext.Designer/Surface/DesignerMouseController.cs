using ModernFormsNext;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerMouseController
{
    private const int MinimumControlSize = 8;

    private readonly DesignerSession state;
    private readonly DesignerCoordinateMapper coordinateMapper;
    private readonly DesignerHitTestService hitTestService;
    private DesignerMouseOperation operation;
    private DesignerResizeHandle resizeHandle;
    private DesignControlNode? activeNode;
    private DesignPoint startDocumentPoint;
    private DesignBounds startBounds;
    private bool changedBounds;

    public DesignerMouseController(DesignerSession state)
    {
        this.state = state;
        coordinateMapper = new DesignerCoordinateMapper();
        hitTestService = new DesignerHitTestService(coordinateMapper);
    }

    public void HandleMouseDown(Control surface, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        var surfacePoint = ToSurfacePoint(surface, e);
        var handle = hitTestService.HitTestResizeHandle(state, surface.Width, surface.Height, surfacePoint.X, surfacePoint.Y);

        if (handle != DesignerResizeHandle.None && state.SelectedNode is { } selectedNode)
        {
            BeginOperation(surface, selectedNode, DesignerMouseOperation.Resizing, handle, GetDocumentPointUnbounded(surface, surfacePoint.X, surfacePoint.Y));
            return;
        }

        if (!TryGetDocumentPoint(surface, surfacePoint.X, surfacePoint.Y, out var point))
        {
            state.SelectForm();
            ClearOperation(surface);
            return;
        }

        var hit = hitTestService.HitTestControl(state, point);

        if (hit.Node is null)
        {
            state.SelectForm();
            ClearOperation(surface);
            return;
        }

        state.SelectNode(hit.Node);

        if (DesignerLayoutProperties.IsDocked(hit.Node))
        {
            state.Log($"{hit.Node.Name} is docked; set Dock to None before manual move.");
            ClearOperation(surface);
            return;
        }

        BeginOperation(surface, hit.Node, DesignerMouseOperation.Dragging, DesignerResizeHandle.None, point);
    }

    public void HandleMouseMove(Control surface, MouseEventArgs e)
    {
        var surfacePoint = ToSurfacePoint(surface, e);

        if (TryGetDocumentPoint(surface, surfacePoint.X, surfacePoint.Y, out var point))
            state.SetPointerPosition(point);
        else
            state.SetPointerPosition(null);

        if (operation is not (DesignerMouseOperation.Dragging or DesignerMouseOperation.Resizing) || activeNode is null)
            return;

        var currentPoint = GetDocumentPointUnbounded(surface, surfacePoint.X, surfacePoint.Y);
        var deltaX = currentPoint.X - startDocumentPoint.X;
        var deltaY = currentPoint.Y - startDocumentPoint.Y;

        if (operation == DesignerMouseOperation.Dragging)
            UpdateDrag(deltaX, deltaY);
        else
            UpdateResize(deltaX, deltaY);
    }

    public void HandleMouseUp(Control surface, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        if (operation is DesignerMouseOperation.Dragging or DesignerMouseOperation.Resizing && activeNode is not null && changedBounds)
        {
            var surfacePoint = ToSurfacePoint(surface, e);
            var currentPoint = GetDocumentPointUnbounded(surface, surfacePoint.X, surfacePoint.Y);

            if (operation == DesignerMouseOperation.Dragging)
                state.ReparentNodeAtDocumentPoint(activeNode, currentPoint);

            var action = operation == DesignerMouseOperation.Dragging ? "Moved" : "Resized";
            state.Log($"{action} {activeNode.Name} to {activeNode.Bounds.X}, {activeNode.Bounds.Y}, {activeNode.Bounds.Width} x {activeNode.Bounds.Height}.");
        }

        ClearOperation(surface);
    }

    private void BeginOperation(
        Control surface,
        DesignControlNode node,
        DesignerMouseOperation nextOperation,
        DesignerResizeHandle handle,
        DesignPoint documentPoint)
    {
        operation = nextOperation;
        resizeHandle = handle;
        activeNode = node;
        startDocumentPoint = documentPoint;
        startBounds = node.Bounds;
        changedBounds = false;
        surface.Capture = true;
    }

    private void ClearOperation(Control surface)
    {
        operation = DesignerMouseOperation.None;
        resizeHandle = DesignerResizeHandle.None;
        activeNode = null;
        changedBounds = false;
        surface.Capture = false;
    }

    private void UpdateDrag(int deltaX, int deltaY)
    {
        if (activeNode is null)
            return;

        var nextBounds = new DesignBounds(
            startBounds.X + deltaX,
            startBounds.Y + deltaY,
            startBounds.Width,
            startBounds.Height);

        CommitBounds(nextBounds);
    }

    private void UpdateResize(int deltaX, int deltaY)
    {
        if (activeNode is null || resizeHandle == DesignerResizeHandle.None)
            return;

        if (DesignerLayoutProperties.IsDocked(activeNode))
        {
            UpdateDockedResize(deltaX, deltaY);
            return;
        }

        var left = startBounds.X;
        var top = startBounds.Y;
        var right = startBounds.X + startBounds.Width;
        var bottom = startBounds.Y + startBounds.Height;
        var movesLeft = resizeHandle is DesignerResizeHandle.TopLeft or DesignerResizeHandle.Left or DesignerResizeHandle.BottomLeft;
        var movesRight = resizeHandle is DesignerResizeHandle.TopRight or DesignerResizeHandle.Right or DesignerResizeHandle.BottomRight;
        var movesTop = resizeHandle is DesignerResizeHandle.TopLeft or DesignerResizeHandle.Top or DesignerResizeHandle.TopRight;
        var movesBottom = resizeHandle is DesignerResizeHandle.BottomLeft or DesignerResizeHandle.Bottom or DesignerResizeHandle.BottomRight;

        if (movesLeft)
            left += deltaX;
        else if (movesRight)
            right += deltaX;

        if (movesTop)
            top += deltaY;
        else if (movesBottom)
            bottom += deltaY;

        if (right - left < MinimumControlSize)
        {
            if (movesLeft)
                left = right - MinimumControlSize;
            else
                right = left + MinimumControlSize;
        }

        if (bottom - top < MinimumControlSize)
        {
            if (movesTop)
                top = bottom - MinimumControlSize;
            else
                bottom = top + MinimumControlSize;
        }

        var nextBounds = new DesignBounds(left, top, right - left, bottom - top);

        CommitBounds(nextBounds);
    }

    private void UpdateDockedResize(int deltaX, int deltaY)
    {
        if (activeNode is null || !DesignerLayoutProperties.CanResize(activeNode, resizeHandle))
            return;

        var width = startBounds.Width;
        var height = startBounds.Height;

        switch (DesignerLayoutProperties.GetDock(activeNode))
        {
            case DockStyle.Top:
                height = startBounds.Height + deltaY;
                break;
            case DockStyle.Bottom:
                height = startBounds.Height - deltaY;
                break;
            case DockStyle.Left:
                width = startBounds.Width + deltaX;
                break;
            case DockStyle.Right:
                width = startBounds.Width - deltaX;
                break;
            default:
                return;
        }

        var nextBounds = new DesignBounds(
            startBounds.X,
            startBounds.Y,
            Math.Max(MinimumControlSize, width),
            Math.Max(MinimumControlSize, height));

        CommitBounds(nextBounds);
    }

    private void CommitBounds(DesignBounds bounds)
    {
        if (activeNode is null || activeNode.Bounds == bounds)
            return;

        activeNode.Bounds = bounds;
        changedBounds = true;
        state.NotifyDocumentChanged();
    }

    private bool TryGetDocumentPoint(Control surface, float logicalX, float logicalY, out DesignPoint point)
        => coordinateMapper.TryMapToDocument(state, surface.Width, surface.Height, logicalX, logicalY, out point);

    private DesignPoint GetDocumentPointUnbounded(Control surface, float logicalX, float logicalY)
    {
        var view = coordinateMapper.GetView(state, surface.Width, surface.Height);
        return coordinateMapper.MapToDocument(view, logicalX, logicalY);
    }

    private static (float X, float Y) ToSurfacePoint(Control surface, MouseEventArgs e)
    {
        var logicalX = (float)(e.X / surface.ScaleFactor.Width);
        var logicalY = (float)(e.Y / surface.ScaleFactor.Height);
        return (logicalX, logicalY);
    }
}
