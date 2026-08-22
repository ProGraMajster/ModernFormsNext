using ModernFormsNext;
using ModernFormsNext.Designer.History;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

internal sealed class DesignerMouseController
{
    private const int MinimumControlSize = 8;

    private readonly DesignerSession state;
    private readonly DesignerCoordinateMapper coordinateMapper;
    private readonly DesignerHitTestService hitTestService;
    private readonly DesignerLayoutEngine layoutEngine = new();
    private DesignerMouseOperation operation;
    private DesignerResizeHandle resizeHandle;
    private DesignControlNode? activeNode;
    private DesignPoint startDocumentPoint;
    private DesignBounds startBounds;
    private int startSplitterDistance;
    private bool changedBounds;
    private bool resizingRoot;
    private DesignSize startRootSize;
    private (float X, float Y) startRootSurfacePoint;
    private float startRootScale = 1f;
    private DesignerTransaction? activeTransaction;
    private DesignerModelMutationSnapshot? operationSnapshot;

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

        if (handle != DesignerResizeHandle.None
            && state.SelectedNode is null
            && state.Document.RootKind == DesignRootKind.UserControl)
        {
            BeginRootResize(surface, handle, surfacePoint);
            return;
        }

        if (!TryGetDocumentPoint(surface, surfacePoint.X, surfacePoint.Y, out var point))
        {
            state.SelectForm();
            ClearOperation(surface);
            return;
        }

        if (hitTestService.HitTestTabHeader(state, point, out var tabIndex) is { } tabControl)
        {
            using var transaction = state.Transactions.Begin($"Change selected tab on {tabControl.Name}");
            var snapshot = DesignerModelMutationSnapshot.CaptureNode(tabControl);
            try
            {
                DesignerSpecialContainers.SetInt(tabControl, DesignerSpecialContainers.SelectedIndexPropertyName, tabIndex);
            }
            finally
            {
                snapshot.RecordChanges(state.Transactions);
            }
            state.SelectNode(tabControl);
            transaction.Commit();
            state.Log($"Selected tab {tabIndex + 1} on {tabControl.Name}.");
            ClearOperation(surface);
            return;
        }

        if (hitTestService.HitTestSplitter(state, point) is { } splitContainer)
        {
            state.SelectNode(splitContainer);
            BeginOperation(surface, splitContainer, DesignerMouseOperation.MovingSplitter, DesignerResizeHandle.None, point);
            startSplitterDistance = DesignerSpecialContainers.GetInt(
                splitContainer,
                DesignerSpecialContainers.SplitterDistancePropertyName,
                GetDefaultSplitterDistance(splitContainer));
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

        if (operation is not (DesignerMouseOperation.Dragging or DesignerMouseOperation.Resizing or DesignerMouseOperation.MovingSplitter)
            || activeNode is null && !resizingRoot)
            return;

        try
        {
            if (resizingRoot)
            {
                var rootDeltaX = (int)Math.Round((surfacePoint.X - startRootSurfacePoint.X) / startRootScale);
                var rootDeltaY = (int)Math.Round((surfacePoint.Y - startRootSurfacePoint.Y) / startRootScale);
                UpdateRootResize(rootDeltaX, rootDeltaY);
                return;
            }

            var currentPoint = GetDocumentPointUnbounded(surface, surfacePoint.X, surfacePoint.Y);
            var deltaX = currentPoint.X - startDocumentPoint.X;
            var deltaY = currentPoint.Y - startDocumentPoint.Y;

            if (operation == DesignerMouseOperation.Dragging)
                UpdateDrag(deltaX, deltaY);
            else if (operation == DesignerMouseOperation.Resizing)
                UpdateResize(deltaX, deltaY);
            else
                UpdateSplitterDistance(deltaX, deltaY);
        }
        catch
        {
            if (activeTransaction is not null && state.Transactions.HasActiveTransaction)
                CancelOperation(surface);
            else
                ClearOperation(surface);
            throw;
        }
    }

    public void HandleMouseUp(Control surface, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        try
        {
            if (operation is DesignerMouseOperation.Dragging or DesignerMouseOperation.Resizing or DesignerMouseOperation.MovingSplitter
                && (activeNode is not null || resizingRoot)
                && changedBounds)
            {
                if (resizingRoot)
                {
                    RecordOperationChanges();
                    CommitOperationTransaction();
                    state.Log($"Resized {state.Document.FormName} to {state.Document.Size.Width} x {state.Document.Size.Height}.");
                    ClearOperation(surface);
                    return;
                }

                var node = activeNode!;

                var surfacePoint = ToSurfacePoint(surface, e);
                var currentPoint = GetDocumentPointUnbounded(surface, surfacePoint.X, surfacePoint.Y);

                if (operation == DesignerMouseOperation.Dragging)
                {
                    // Record live bounds first. Reparenting is a nested transaction whose own node
                    // snapshot starts from those final drag bounds.
                    RecordOperationChanges();
                    state.ReparentNodeAtDocumentPoint(node, currentPoint);
                }
                else
                {
                    RecordOperationChanges();
                }

                if (operation == DesignerMouseOperation.MovingSplitter)
                {
                    var distance = DesignerSpecialContainers.GetInt(node, DesignerSpecialContainers.SplitterDistancePropertyName, startSplitterDistance);
                    state.Log($"Moved {node.Name} splitter to {distance}.");
                }
                else
                {
                    var action = operation == DesignerMouseOperation.Dragging ? "Moved" : "Resized";
                    state.Log($"{action} {node.Name} to {node.Bounds.X}, {node.Bounds.Y}, {node.Bounds.Width} x {node.Bounds.Height}.");
                }

                CommitOperationTransaction();
            }
        }
        catch
        {
            CancelOperation(surface);
            throw;
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
        ClearOperation(surface);
        operation = nextOperation;
        resizeHandle = handle;
        activeNode = node;
        startDocumentPoint = documentPoint;
        startBounds = node.Bounds;
        startSplitterDistance = 0;
        changedBounds = false;
        resizingRoot = false;
        operationSnapshot = DesignerModelMutationSnapshot.CaptureNode(node);
        activeTransaction = state.Transactions.Begin(nextOperation switch
        {
            DesignerMouseOperation.Dragging => $"Move {node.Name}",
            DesignerMouseOperation.Resizing => $"Resize {node.Name}",
            DesignerMouseOperation.MovingSplitter => $"Move {node.Name} splitter",
            _ => $"Change {node.Name}"
        });
        surface.Capture = true;
    }

    private void BeginRootResize(
        Control surface,
        DesignerResizeHandle handle,
        (float X, float Y) surfacePoint)
    {
        ClearOperation(surface);
        operation = DesignerMouseOperation.Resizing;
        resizeHandle = handle;
        activeNode = null;
        startDocumentPoint = default;
        startRootSize = state.Document.Size;
        startRootSurfacePoint = surfacePoint;
        startRootScale = coordinateMapper.GetView(state, surface.Width, surface.Height).Scale;
        startSplitterDistance = 0;
        changedBounds = false;
        resizingRoot = true;
        operationSnapshot = DesignerModelMutationSnapshot.CaptureDocumentLayout(state.Document);
        activeTransaction = state.Transactions.Begin($"Resize {state.Document.FormName}");
        surface.Capture = true;
    }

    private void ClearOperation(Control surface)
    {
        activeTransaction?.Dispose();
        activeTransaction = null;
        operationSnapshot = null;
        operation = DesignerMouseOperation.None;
        resizeHandle = DesignerResizeHandle.None;
        activeNode = null;
        changedBounds = false;
        resizingRoot = false;
        startRootSurfacePoint = default;
        startRootScale = 1f;
        startSplitterDistance = 0;
        surface.Capture = false;
    }

    public bool CancelOperation(Control surface)
    {
        if (activeTransaction is null)
            return false;

        RecordOperationChanges();
        activeTransaction.Rollback();
        activeTransaction = null;
        operationSnapshot = null;
        state.Log("Cancelled the active Designer gesture.");
        ClearOperation(surface);
        return true;
    }

    private void RecordOperationChanges()
    {
        operationSnapshot?.RecordChanges(state.Transactions);
        operationSnapshot = null;
    }

    private void CommitOperationTransaction()
    {
        try
        {
            activeTransaction?.Commit();
        }
        finally
        {
            if (!state.Transactions.HasActiveTransaction)
            {
                activeTransaction = null;
                operationSnapshot = null;
            }
        }
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

    private void UpdateRootResize(int deltaX, int deltaY)
    {
        var width = startRootSize.Width;
        var height = startRootSize.Height;

        if (resizeHandle is DesignerResizeHandle.Right or DesignerResizeHandle.BottomRight)
            width = Math.Max(MinimumControlSize, startRootSize.Width + deltaX);

        if (resizeHandle is DesignerResizeHandle.Bottom or DesignerResizeHandle.BottomRight)
            height = Math.Max(MinimumControlSize, startRootSize.Height + deltaY);

        var nextSize = new DesignSize(width, height);

        if (state.Document.Size == nextSize)
            return;

        layoutEngine.ResizeRoot(state.Document, nextSize);
        changedBounds = true;
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
    }

    private void UpdateSplitterDistance(int deltaX, int deltaY)
    {
        if (activeNode is null || !DesignerSpecialContainers.IsSplitContainer(activeNode))
            return;

        var orientation = DesignerSpecialContainers.GetEnum(
            activeNode,
            DesignerSpecialContainers.OrientationPropertyName,
            Orientation.Horizontal);
        var splitterWidth = Math.Max(1, DesignerSpecialContainers.GetInt(activeNode, DesignerSpecialContainers.SplitterWidthPropertyName, 5));
        var panel1Minimum = Math.Max(0, DesignerSpecialContainers.GetInt(activeNode, "Panel1MinimumSize", 25));
        var panel2Minimum = Math.Max(0, DesignerSpecialContainers.GetInt(activeNode, "Panel2MinimumSize", 25));
        var layout = layoutEngine.Layout(state.Document);
        var bounds = layout.GetEffectiveBounds(activeNode);
        var available = orientation == Orientation.Horizontal ? bounds.Width : bounds.Height;
        var maximum = Math.Max(panel1Minimum, available - splitterWidth - panel2Minimum);
        var delta = orientation == Orientation.Horizontal ? deltaX : deltaY;
        var nextDistance = Math.Clamp(startSplitterDistance + delta, panel1Minimum, maximum);

        if (DesignerSpecialContainers.GetInt(activeNode, DesignerSpecialContainers.SplitterDistancePropertyName, startSplitterDistance) == nextDistance)
            return;

        DesignerSpecialContainers.SetInt(activeNode, DesignerSpecialContainers.SplitterDistancePropertyName, nextDistance);
        changedBounds = true;
    }

    private int GetDefaultSplitterDistance(DesignControlNode splitContainer)
    {
        var layout = layoutEngine.Layout(state.Document);
        var bounds = layout.GetEffectiveBounds(splitContainer);
        var orientation = DesignerSpecialContainers.GetEnum(
            splitContainer,
            DesignerSpecialContainers.OrientationPropertyName,
            Orientation.Horizontal);

        return Math.Max(25, (orientation == Orientation.Horizontal ? bounds.Width : bounds.Height) / 2);
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
        // WindowBase converts backend DIPs to device pixels before routing mouse events through
        // scaled control bounds. Convert back exactly once here; all remaining designer interaction
        // code works in logical surface/document coordinates and must not apply DPI again.
        var logicalPoint = DesignerDpiCoordinateConverter.DeviceToLogical(e.X, e.Y, surface.Scaling);
        return (logicalPoint.X, logicalPoint.Y);
    }
}
