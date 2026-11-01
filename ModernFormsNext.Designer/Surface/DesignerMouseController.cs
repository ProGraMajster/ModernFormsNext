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
    private DesignerSelectionSnapshot operationSelection;
    private IReadOnlyList<(DesignControlNode Node, DesignBounds Bounds)>? groupStart;
    private IReadOnlyList<DesignControlNode> marqueeBefore = [];
    private DesignControlNode? marqueePrimary;
    private bool additiveMarquee;
    private DesignDocument? marqueeDocument;
    internal DesignBounds? MarqueeBounds { get; private set; }

    internal bool HasActiveOperation => activeTransaction is not null || operation == DesignerMouseOperation.Selecting;

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

        // A second down means the previous platform gesture ended without a matching up. Resolve
        // only that in-flight Designer transaction before hit testing the new action.
        CancelOrClearExistingOperation(surface, "NewMouseDown");

        var surfacePoint = ToSurfacePoint(surface, e);
        bool selectionModifier = e.Control && !e.AltGraph || e.Shift;
        var handle = hitTestService.HitTestResizeHandle(state, surface.Width, surface.Height, surfacePoint.X, surfacePoint.Y);

        if (!selectionModifier && handle != DesignerResizeHandle.None && state.SelectedNode is { } selectedNode)
        {
            BeginOperation(surface, selectedNode, DesignerMouseOperation.Resizing, handle, GetDocumentPointUnbounded(surface, surfacePoint.X, surfacePoint.Y));
            return;
        }

        if (!selectionModifier && handle != DesignerResizeHandle.None
            && state.SelectedNode is null
            && state.Document.RootKind == DesignRootKind.UserControl)
        {
            BeginRootResize(surface, handle, surfacePoint);
            return;
        }

        if (!TryGetDocumentPoint(surface, surfacePoint.X, surfacePoint.Y, out var point))
        {
            if (!selectionModifier) state.SelectForm();
            ClearOperation(surface);
            return;
        }

        if (!selectionModifier && hitTestService.HitTestTabHeader(state, point, out var tabIndex) is { } tabControl)
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

        if (!selectionModifier && hitTestService.HitTestSplitter(state, point) is { } splitContainer)
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
            operation = DesignerMouseOperation.Selecting;
            startDocumentPoint = point;
            additiveMarquee = e.Control && !e.AltGraph || e.Shift;
            marqueeBefore = state.SelectedNodes;
            marqueePrimary = state.SelectedNode;
            marqueeDocument = state.Document;
            MarqueeBounds = new DesignBounds(point.X, point.Y, 0, 0);
            surface.Capture = true;
            return;
        }

        state.SelectNodeFromPointer(hit.Node, e.Control && !e.AltGraph, e.Shift);
        if (e.Control && !e.AltGraph || e.Shift) return;

        if (state.SelectedNodes.Count > 1)
        {
            if (!state.TryGetManualSelectedRoots(out var roots, out var error)) { state.Log(error!); return; }
            BeginOperation(surface, hit.Node, DesignerMouseOperation.Dragging, DesignerResizeHandle.None, point, roots);
            return;
        }

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

        if (operation == DesignerMouseOperation.Selecting)
        {
            UpdateMarquee(GetDocumentPointUnbounded(surface, surfacePoint.X, surfacePoint.Y));
            return;
        }

        if (CancelIfSelectionReplaced(surface)) return;

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
                UpdateDrag((long)currentPoint.X - startDocumentPoint.X, (long)currentPoint.Y - startDocumentPoint.Y);
            else if (operation == DesignerMouseOperation.Resizing)
                UpdateResize(deltaX, deltaY);
            else
                UpdateSplitterDistance(deltaX, deltaY);
        }
        catch
        {
            if (activeTransaction is not null && state.Transactions.HasActiveTransaction)
                CancelOperation(surface, "MouseMoveException");
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
            if (operation == DesignerMouseOperation.Selecting)
            {
                if (!ReferenceEquals(marqueeDocument, state.Document)) { ClearOperation(surface); return; }
                var surfacePoint = ToSurfacePoint(surface, e);
                UpdateMarquee(GetDocumentPointUnbounded(surface, surfacePoint.X, surfacePoint.Y));
                var nodes = MarqueeBounds is { Width: > 0, Height: > 0 } rectangle
                    ? hitTestService.HitTestMarquee(state, rectangle) : [];
                var attached = new HashSet<DesignControlNode>(state.EnumerateNodes().Select(item => item.Node), ReferenceEqualityComparer.Instance);
                var before = additiveMarquee ? marqueeBefore.Where(attached.Contains).ToArray() : [];
                var primary = nodes.LastOrDefault() ?? (additiveMarquee && marqueePrimary is not null && attached.Contains(marqueePrimary) ? marqueePrimary : null);
                // Release capture before publication; a reentrant selection remains newer.
                ClearOperation(surface);
                // Existing and enclosed members overlap. Admit each identity once so a valid
                // document-sized union does not consume the bounded input budget twice.
                state.Host.Selection.SelectNodes(before.Concat(nodes).Distinct<DesignControlNode>(ReferenceEqualityComparer.Instance), primary);
                return;
            }
            if (CancelIfSelectionReplaced(surface)) return;
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
                    if (groupStart is not null) state.ReparentSelectedNodesAtDocumentPoint(currentPoint);
                    else state.ReparentNodeAtDocumentPoint(node, currentPoint);
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
            if (!CancelOperation(surface, "MouseUpException"))
                ClearOperation(surface);
            throw;
        }

        CancelOrClearExistingOperation(surface, "NewPointerOperation");
    }

    private void BeginOperation(
        Control surface,
        DesignControlNode node,
        DesignerMouseOperation nextOperation,
        DesignerResizeHandle handle,
        DesignPoint documentPoint,
        IReadOnlyList<DesignControlNode>? groupNodes = null)
    {
        CancelOrClearExistingOperation(surface, "NewPointerOperation");
        operation = nextOperation;
        resizeHandle = handle;
        activeNode = node;
        startDocumentPoint = documentPoint;
        startBounds = node.Bounds;
        startSplitterDistance = 0;
        changedBounds = false;
        resizingRoot = false;
        // Freeze gesture targets before Begin/Log observers can replace selection or mutate data.
        groupStart = groupNodes?.Select(member => (member, member.Bounds)).ToArray();
        operationSnapshot = groupNodes is null ? DesignerModelMutationSnapshot.CaptureNode(node)
            : DesignerModelMutationSnapshot.CaptureNodes(groupNodes);
        operationSelection = DesignerSelectionSnapshot.Capture(state.Host.Selection);
        activeTransaction = state.Transactions.Begin(nextOperation switch
        {
            DesignerMouseOperation.Dragging => $"Move {node.Name}",
            DesignerMouseOperation.Resizing => $"Resize {node.Name}",
            DesignerMouseOperation.MovingSplitter => $"Move {node.Name} splitter",
            _ => $"Change {node.Name}"
        });
        surface.Capture = true;
        state.Log($"GESTURE_BEGIN id={state.Transactions.CurrentTransactionId} Operation={nextOperation} " +
            $"Node={node.Name} Bounds={DescribeBounds(node.Bounds)} {DescribeTransactionState()}");
    }

    private void BeginRootResize(
        Control surface,
        DesignerResizeHandle handle,
        (float X, float Y) surfacePoint)
    {
        CancelOrClearExistingOperation(surface, "NewRootResizeOperation");
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
        operationSelection = DesignerSelectionSnapshot.Capture(state.Host.Selection);
        activeTransaction = state.Transactions.Begin($"Resize {state.Document.FormName}");
        surface.Capture = true;
        state.Log($"GESTURE_BEGIN id={state.Transactions.CurrentTransactionId} Operation=RootResize " +
            $"Bounds=0,0,{state.Document.Size.Width},{state.Document.Size.Height} {DescribeTransactionState()}");
    }

    private void ClearOperation(Control surface)
    {
        try
        {
            activeTransaction?.Dispose();
        }
        finally
        {
            // A post-rollback observer may throw after the manager has already completed the
            // transaction. Clear capture and gesture state in that case; retain them only when an
            // actual model revert failed and the transaction remains available for retry.
            if (activeTransaction is null || !state.Transactions.HasActiveTransaction)
            {
                activeTransaction = null;
                operationSnapshot = null;
                operation = DesignerMouseOperation.None;
                resizeHandle = DesignerResizeHandle.None;
                activeNode = null;
                groupStart = null;
                operationSelection = default;
                MarqueeBounds = null;
                marqueeBefore = [];
                marqueePrimary = null;
                marqueeDocument = null;
                changedBounds = false;
                resizingRoot = false;
                startRootSurfacePoint = default;
                startRootScale = 1f;
                startSplitterDistance = 0;
                surface.Capture = false;
            }
        }
    }

    public bool CancelOperation(Control surface, string reason = "Explicit")
    {
        if (operation == DesignerMouseOperation.Selecting)
        {
            ClearOperation(surface);
            return true; // Selection was not published while the marquee was in flight.
        }
        if (activeTransaction is null)
            return false;

        var transactionId = state.Transactions.CurrentTransactionId;
        var expectedSelection = operationSelection;
        bool retainNewerSelection = !expectedSelection.Matches(state.Host.Selection);
        var newerSelection = DesignerSelectionSnapshot.Capture(state.Host.Selection);
        var before = DescribeCurrentBounds();
        state.Log($"GESTURE_CANCEL id={transactionId} Reason={reason} Before={before} {DescribeTransactionState()}");
        try
        {
            RecordOperationChanges();
            activeTransaction.Rollback();
            state.Log($"GESTURE_ROLLBACK_CURRENT id={transactionId} Reason={reason} " +
                $"After={DescribeCurrentBounds()} {DescribeTransactionState()}");
            return true;
        }
        finally
        {
            if (!state.Transactions.HasActiveTransaction)
            {
                try
                {
                    // Rollback restores the transaction's original selection. Restore an external
                    // replacement only if no rollback observer has already published something newer.
                    if (retainNewerSelection && expectedSelection.Matches(state.Host.Selection))
                        newerSelection.Restore(state.Host.Selection);
                }
                finally
                {
                    activeTransaction = null;
                    operationSnapshot = null;
                    ClearOperation(surface);
                }
            }
        }
    }

    private bool CancelIfSelectionReplaced(Control surface)
    {
        if (activeTransaction is null || operationSelection.Matches(state.Host.Selection)) return false;
        CancelOperation(surface, "SelectionReplaced");
        return true;
    }

    private void RecordOperationChanges()
    {
        operationSnapshot?.RecordChanges(state.Transactions);
        operationSnapshot = null;
    }

    private void CommitOperationTransaction()
    {
        var transactionId = state.Transactions.CurrentTransactionId;
        try
        {
            activeTransaction?.Commit();
            state.Log($"GESTURE_COMMIT id={transactionId} Bounds={DescribeCurrentBounds()} {DescribeTransactionState()}");
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

    private void CancelOrClearExistingOperation(Control surface, string reason)
    {
        if (!CancelOperation(surface, reason))
            ClearOperation(surface);
    }

    private string DescribeCurrentBounds()
        => resizingRoot
            ? $"0,0,{state.Document.Size.Width},{state.Document.Size.Height}"
            : activeNode is null ? "<none>" : DescribeBounds(activeNode.Bounds);

    private static string DescribeBounds(DesignBounds bounds)
        => $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";

    private string DescribeTransactionState()
        => $"Revision={state.CurrentHistory.CurrentRevision} UndoCount={state.CurrentHistory.UndoCount} " +
            $"RedoCount={state.CurrentHistory.RedoCount} ActiveTransaction=" +
            $"{state.Transactions.CurrentTransactionId?.ToString() ?? "<none>"} " +
            $"ReplayMode={state.Transactions.ReplayMode}";

    private void UpdateDrag(long deltaX, long deltaY)
    {
        if (activeNode is null)
            return;

        var starts = groupStart ?? new[] { (activeNode, startBounds) };
        var next = new List<(DesignControlNode Node, DesignBounds Bounds)>(starts.Count);
        foreach (var (node, bounds) in starts)
        {
            long x = bounds.X + deltaX, y = bounds.Y + deltaY;
            if (x < int.MinValue || y < int.MinValue || x + bounds.Width > int.MaxValue || y + bounds.Height > int.MaxValue) return;
            next.Add((node, new DesignBounds((int)x, (int)y, bounds.Width, bounds.Height)));
        }
        foreach (var (node, bounds) in next)
        {
            if (node.Bounds == bounds) continue;
            node.Bounds = bounds;
            changedBounds = true;
        }
    }

    private void UpdateMarquee(DesignPoint point)
    {
        int x = Math.Clamp(point.X, 0, state.Document.Size.Width), y = Math.Clamp(point.Y, 0, state.Document.Size.Height);
        int left = Math.Min(startDocumentPoint.X, x), top = Math.Min(startDocumentPoint.Y, y);
        MarqueeBounds = new DesignBounds(left, top, Math.Max(startDocumentPoint.X, x) - left, Math.Max(startDocumentPoint.Y, y) - top);
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

        layoutEngine.ResizeRoot(state.Document, nextSize, state.PreviewResourcePolicy);
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
        var layout = layoutEngine.Layout(state.Document, state.PreviewResourcePolicy);
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
        var layout = layoutEngine.Layout(state.Document, state.PreviewResourcePolicy);
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
