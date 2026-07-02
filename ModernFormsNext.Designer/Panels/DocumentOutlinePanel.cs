using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using SkiaSharp;

namespace ModernFormsNext.Designer.Panels;

internal sealed class DocumentOutlinePanel : DesignerPanelBase
{
    private const int RowHeight = 23;
    private const int TreeTop = 36;
    private const int DragThreshold = 4;

    private readonly DesignerSession state;
    private readonly List<OutlineRow> rows = [];
    private readonly ContextMenu contextMenu;
    private readonly MenuItem deleteMenuItem;
    private DesignControlNode? contextNode;
    private DesignControlNode? dragNode;
    private int dragStartY;
    private int dropIndex = -1;
    private int scrollOffset;
    private bool isDragging;

    public DocumentOutlinePanel(DesignerSession state, string title = "Document Outline", string deleteText = "Delete")
        : base(title)
    {
        this.state = state;
        contextMenu = new ContextMenu();
        deleteMenuItem = contextMenu.Items.Add(deleteText, onClick: (_, _) =>
        {
            if (contextNode is not null)
                state.DeleteNode(contextNode);
        });
        ContextMenu = contextMenu;

        state.DocumentChanged += (_, _) =>
        {
            ClampScrollOffset();
            Invalidate();
        };
        state.SelectionChanged += (_, _) => Invalidate();
        SizeChanged += (_, _) =>
        {
            ClampScrollOffset();
            Invalidate();
        };
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        var row = GetRowAt(e.Y);

        if (row is null)
            return;

        if (e.Button == MouseButtons.Right)
        {
            contextNode = row.Node;
            deleteMenuItem.Enabled = contextNode is not null;

            if (row.Node is null)
                state.SelectForm();
            else
                state.SelectNode(row.Node);

            dragNode = null;
            return;
        }

        if (row.Node is null)
        {
            state.SelectForm();
            dragNode = null;
            return;
        }

        state.SelectNode(row.Node);
        dragNode = row.Node;
        dragStartY = e.Y;
        dropIndex = -1;
        isDragging = false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (e.Button != MouseButtons.Left)
            return;

        if (dragNode is null)
            return;

        if (!isDragging && Math.Abs(e.Y - dragStartY) >= DragThreshold)
            isDragging = true;

        if (!isDragging)
            return;

        dropIndex = GetRowIndexAt(e.Y);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
            return;

        if (dragNode is null)
            return;

        var node = dragNode;
        dragNode = null;

        if (!isDragging)
            return;

        isDragging = false;
        var targetRow = dropIndex >= 0 && dropIndex < rows.Count ? rows[dropIndex] : rows.FirstOrDefault();
        dropIndex = -1;

        if (targetRow is null)
            return;

        state.MoveNodeToOutlineTarget(node, targetRow.Node);
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        BuildRows();

        var maxOffset = GetMaxScrollOffset();

        if (maxOffset == 0)
            return;

        var delta = e.Delta.Y == 0 ? 0 : -Math.Sign(e.Delta.Y) * (RowHeight * 3);
        var nextOffset = Math.Clamp(scrollOffset + delta, 0, maxOffset);

        if (nextOffset == scrollOffset)
            return;

        scrollOffset = nextOffset;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        BuildRows();
        ClampScrollOffset();

        e.Canvas.Save();
        e.Canvas.Clip(new System.Drawing.Rectangle(0, TreeTop, Width, Math.Max(1, Height - TreeTop)));

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var y = TreeTop + (i * RowHeight) - scrollOffset;

            if (y + RowHeight < TreeTop || y > Height)
                continue;

            var selected = row.Node is null
                ? state.SelectedNode is null
                : ReferenceEquals(row.Node, state.SelectedNode);
            var indent = 16 + (row.Depth * 20);

            if (selected)
                e.Canvas.FillRectangle(6, y, Math.Max(1, Width - 12), RowHeight, DesignerColors.Selection);

            if (isDragging && i == dropIndex)
                e.Canvas.DrawRectangle(6, y, Math.Max(1, Width - 12), RowHeight, new SKColor(0, 122, 204));

            e.Canvas.DrawText(
                row.Node is null ? "[]" : GetGlyph(row.TypeName),
                Theme.UIFont,
                e.LogicalToDeviceUnits(Theme.FontSize),
                new System.Drawing.Rectangle(e.LogicalToDeviceUnits(indent), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(24), e.LogicalToDeviceUnits(RowHeight)),
                DesignerColors.MutedText,
                ContentAlignment.MiddleCenter);

            e.Canvas.DrawText(
                $"{row.Name}  {row.TypeName}",
                Theme.UIFont,
                e.LogicalToDeviceUnits(Theme.FontSize),
                new System.Drawing.Rectangle(e.LogicalToDeviceUnits(indent + 28), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(Math.Max(1, Width - indent - 36)), e.LogicalToDeviceUnits(RowHeight)),
                DesignerColors.Text,
                ContentAlignment.MiddleLeft,
                maxLines: 1,
                ellipsis: true);
        }

        e.Canvas.Restore();
        DrawScrollHint(e);
    }

    private void BuildRows()
    {
        rows.Clear();
        rows.Add(new OutlineRow(null, 0, state.Document.FormName, "Form"));

        foreach (var item in state.EnumerateNodes())
            rows.Add(new OutlineRow(item.Node, item.Depth, item.Node.Name, item.Node.TypeName));
    }

    private OutlineRow? GetRowAt(int y)
    {
        if (rows.Count == 0)
            BuildRows();

        var index = GetRowIndexAt(y);
        return index >= 0 && index < rows.Count ? rows[index] : null;
    }

    private int GetRowIndexAt(int y)
    {
        var index = (y - TreeTop + scrollOffset) / RowHeight;
        return index < 0 || index >= rows.Count ? -1 : index;
    }

    private void ClampScrollOffset()
    {
        BuildRows();
        scrollOffset = Math.Clamp(scrollOffset, 0, GetMaxScrollOffset());
    }

    private int GetMaxScrollOffset()
    {
        var contentHeight = rows.Count * RowHeight;
        var viewportHeight = Math.Max(1, Height - TreeTop);
        return Math.Max(0, contentHeight - viewportHeight);
    }

    private void DrawScrollHint(PaintEventArgs e)
    {
        var contentHeight = rows.Count * RowHeight;
        var viewportHeight = Math.Max(1, Height - TreeTop);

        if (contentHeight <= viewportHeight)
            return;

        var trackTop = TreeTop;
        var trackHeight = Math.Max(1, Height - TreeTop - 4);
        var thumbHeight = Math.Max(24, trackHeight * viewportHeight / contentHeight);
        var maxOffset = Math.Max(1, contentHeight - viewportHeight);
        var thumbTop = trackTop + ((trackHeight - thumbHeight) * scrollOffset / maxOffset);
        e.Canvas.FillRectangle(Math.Max(0, Width - 5), thumbTop, 3, thumbHeight, DesignerColors.MutedText);
    }

    private static string GetGlyph(string typeName)
        => typeName switch
        {
            "Panel" => "[]",
            "Button" => "ab",
            "Label" => "A",
            "TextBox" => "[]",
            _ => "+"
        };

    private sealed record OutlineRow(DesignControlNode? Node, int Depth, string Name, string TypeName);
}
