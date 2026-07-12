using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using SkiaSharp;

namespace ModernFormsNext.Designer.Panels;

internal sealed class DocumentOutlinePanel : DesignerPanelBase
{
    private const int RowHeight = 23;
    private const int ToolbarTop = HeaderHeight + 4;
    private const int ToolbarHeight = 24;
    private const int SearchHeight = 24;
    private const int DragThreshold = 4;

    private readonly DesignerSession state;
    private readonly ModernFormsDesignerOptions options;
    private readonly List<OutlineRow> rows = [];
    private readonly HashSet<DesignControlNode> collapsedNodes = [];
    private readonly ContextMenu contextMenu;
    private readonly MenuItem deleteMenuItem;
    private readonly ComboBox displayModeBox;
    private readonly Button collapseAllButton;
    private readonly Button expandAllButton;
    private readonly Button moveDownButton;
    private readonly Button moveUpButton;
    private readonly Button moveOutButton;
    private readonly Button moveNextContainerButton;
    private readonly TextBox searchBox;
    private readonly string searchPlaceholder;
    private DesignControlNode? contextNode;
    private DesignControlNode? dragNode;
    private int dragStartY;
    private int dropIndex = -1;
    private int scrollOffset;
    private bool isDragging;
    private bool isRootCollapsed;

    public DocumentOutlinePanel(
        DesignerSession state,
        ModernFormsDesignerOptions options,
        string title = "Document Outline",
        string deleteText = "Delete",
        string searchText = "Search document outline")
        : base(title)
    {
        this.state = state;
        this.options = options;
        searchPlaceholder = searchText;
        contextMenu = new ContextMenu();
        deleteMenuItem = contextMenu.Items.Add(deleteText, onClick: (_, _) =>
        {
            if (contextNode is not null)
                state.DeleteNode(contextNode);
        });
        ContextMenu = contextMenu;

        displayModeBox = Controls.Add(new ComboBox
        {
            Left = 8,
            Top = ToolbarTop,
            Width = 74,
            Height = ToolbarHeight
        });
        displayModeBox.Items.AddRange(["Name", "Short", "Full"]);
        displayModeBox.SelectedIndex = 1;
        displayModeBox.SelectedIndexChanged += (_, _) => Invalidate();
        ApplyPanelInputStyle(displayModeBox);

        collapseAllButton = AddToolbarButton("-", CollapseAll);
        expandAllButton = AddToolbarButton("+", ExpandAll);
        moveDownButton = AddToolbarButton("↓", () => ExecuteOutlineCommand(state.MoveSelectedNodeDown));
        moveUpButton = AddToolbarButton("↑", () => ExecuteOutlineCommand(state.MoveSelectedNodeUp));
        moveOutButton = AddToolbarButton("←", () => ExecuteOutlineCommand(state.MoveSelectedNodeOutOfContainer));
        moveNextContainerButton = AddToolbarButton("→", () => ExecuteOutlineCommand(state.MoveSelectedNodeToNextContainer));

        searchBox = Controls.Add(new TextBox
        {
            Left = 8,
            Top = SearchTop,
            Width = 240,
            Height = SearchHeight,
            Placeholder = searchPlaceholder
        });
        ApplyPanelInputStyle(searchBox);
        searchBox.TextChanged += (_, _) =>
        {
            scrollOffset = 0;
            Invalidate();
        };

        SetToolbarVisibility();

        state.DocumentChanged += (_, _) =>
        {
            PruneCollapsedNodes();
            ClampScrollOffset();
            Invalidate();
        };
        state.SelectionChanged += (_, _) => Invalidate();
        SizeChanged += (_, _) =>
        {
            LayoutToolbar();
            ClampScrollOffset();
            Invalidate();
        };

        LayoutToolbar();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Y < TreeTop)
            return;

        var row = GetRowAt(e.Y);

        if (row is null)
            return;

        if (IsExpanderHit(row, e.X))
        {
            ToggleCollapsed(row);
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            contextNode = row.Node;
            deleteMenuItem.Enabled = contextNode is not null && !DesignerSpecialContainers.IsSpecialGeneratedPart(contextNode);

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
        dragNode = DesignerSpecialContainers.IsSpecialGeneratedPart(row.Node) ? null : row.Node;
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
        LayoutToolbar();
        BuildRows();
        ClampScrollOffset();

        e.Canvas.Save();
        e.Canvas.Clip(new Rectangle(0, TreeTop, Width, Math.Max(1, Height - TreeTop)));

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var y = TreeTop + (i * RowHeight) - scrollOffset;

            if (y + RowHeight < TreeTop || y > Height)
                continue;

            DrawRow(e, row, i, y);
        }

        e.Canvas.Restore();
        DrawScrollHint(e);
    }

    private Button AddToolbarButton(string text, Action action)
    {
        var button = Controls.Add(new Button
        {
            Top = ToolbarTop,
            Width = 34,
            Height = ToolbarHeight,
            Text = text,
            TextAlign = ContentAlignment.MiddleCenter
        });
        button.Style.BackgroundColor = new SKColor(52, 58, 66);
        button.Style.ForegroundColor = DesignerColors.Text;
        button.Style.Border.Color = new SKColor(82, 91, 102);
        button.Style.Border.Width = 1;
        button.Click += (_, _) => action();
        return button;
    }

    private void LayoutToolbar()
    {
        SetToolbarVisibility();

        var x = 8;
        if (options.ShowDocumentOutlineToolbar)
        {
            displayModeBox.SetBounds(x, ToolbarTop, Math.Min(74, Math.Max(1, Width - 16)), ToolbarHeight);
            x += 80;
            collapseAllButton.SetBounds(x, ToolbarTop, 26, ToolbarHeight);
            x += 30;
            expandAllButton.SetBounds(x, ToolbarTop, 26, ToolbarHeight);
            x += 30;
            moveDownButton.SetBounds(x, ToolbarTop, 26, ToolbarHeight);
            x += 30;
            moveUpButton.SetBounds(x, ToolbarTop, 26, ToolbarHeight);
            x += 30;
            moveOutButton.SetBounds(x, ToolbarTop, 26, ToolbarHeight);
            x += 30;
            moveNextContainerButton.SetBounds(x, ToolbarTop, 26, ToolbarHeight);
        }

        if (options.ShowDocumentOutlineSearch)
            searchBox.SetBounds(8, SearchTop, Math.Max(1, Width - 16), SearchHeight);
    }

    private void SetToolbarVisibility()
    {
        displayModeBox.Visible = options.ShowDocumentOutlineToolbar;
        collapseAllButton.Visible = options.ShowDocumentOutlineToolbar;
        expandAllButton.Visible = options.ShowDocumentOutlineToolbar;
        moveDownButton.Visible = options.ShowDocumentOutlineToolbar;
        moveUpButton.Visible = options.ShowDocumentOutlineToolbar;
        moveOutButton.Visible = options.ShowDocumentOutlineToolbar;
        moveNextContainerButton.Visible = options.ShowDocumentOutlineToolbar;
        searchBox.Visible = options.ShowDocumentOutlineSearch;
    }

    private void ExecuteOutlineCommand(Func<bool> command)
    {
        if (command())
        {
            ClampScrollOffset();
            Invalidate();
        }
    }

    private void CollapseAll()
    {
        collapsedNodes.Clear();
        foreach (var item in state.EnumerateNodes())
        {
            if (item.Node.Children.Count > 0)
                collapsedNodes.Add(item.Node);
        }

        isRootCollapsed = state.Document.Controls.Count > 0;
        scrollOffset = 0;
        Invalidate();
    }

    private void ExpandAll()
    {
        collapsedNodes.Clear();
        isRootCollapsed = false;
        scrollOffset = 0;
        Invalidate();
    }

    private void DrawRow(PaintEventArgs e, OutlineRow row, int index, int y)
    {
        var selected = row.Node is null
            ? state.SelectedNode is null
            : ReferenceEquals(row.Node, state.SelectedNode);
        var indent = GetIndent(row);

        if (selected)
            e.Canvas.FillRectangle(6, y, Math.Max(1, Width - 12), RowHeight, DesignerColors.Selection);

        if (isDragging && index == dropIndex)
            e.Canvas.DrawRectangle(6, y, Math.Max(1, Width - 12), RowHeight, new SKColor(0, 122, 204));

        if (row.HasChildren)
        {
            e.Canvas.DrawText(
                row.IsCollapsed ? "+" : "-",
                Theme.UIFont,
                e.LogicalToDeviceUnits(Theme.FontSize),
                new Rectangle(e.LogicalToDeviceUnits(indent), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(14), e.LogicalToDeviceUnits(RowHeight)),
                DesignerColors.Text,
                ContentAlignment.MiddleCenter);
        }

        e.Canvas.DrawText(
            row.Node is null ? "[]" : GetGlyph(row.TypeName),
            Theme.UIFont,
            e.LogicalToDeviceUnits(Theme.FontSize),
            new Rectangle(e.LogicalToDeviceUnits(indent + 16), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(24), e.LogicalToDeviceUnits(RowHeight)),
            DesignerColors.MutedText,
            ContentAlignment.MiddleCenter);

        e.Canvas.DrawText(
            GetDisplayText(row),
            Theme.UIFont,
            e.LogicalToDeviceUnits(Theme.FontSize),
            new Rectangle(e.LogicalToDeviceUnits(indent + 44), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(Math.Max(1, Width - indent - 52)), e.LogicalToDeviceUnits(RowHeight)),
            DesignerColors.Text,
            ContentAlignment.MiddleLeft,
            maxLines: 1,
            ellipsis: true);
    }

    private void BuildRows()
    {
        rows.Clear();

        var filter = GetFilterText();
        var formHasChildren = state.Document.Controls.Count > 0;
        var formCollapsed = string.IsNullOrEmpty(filter) && isRootCollapsed;
        rows.Add(new OutlineRow(null, 0, state.Document.FormName, "Form", "ModernFormsNext.Form", formHasChildren, formCollapsed));

        if (formCollapsed)
            return;

        foreach (var node in state.Document.Controls)
            AddNode(node, 1, filter);
    }

    private bool AddNode(DesignControlNode node, int depth, string filter)
    {
        var insertIndex = rows.Count;
        var hasChildren = node.Children.Count > 0;
        var collapsed = string.IsNullOrEmpty(filter) && collapsedNodes.Contains(node);
        var typeName = DesignerSpecialContainers.GetOutlineType(node);
        rows.Add(new OutlineRow(
            node,
            depth,
            DesignerSpecialContainers.GetOutlineName(node),
            typeName,
            state.ResolveControlType(node)?.FullName ?? $"ModernFormsNext.{typeName}",
            hasChildren,
            collapsed));

        var childStart = rows.Count;

        if (!collapsed)
        {
            foreach (var child in node.Children)
                AddNode(child, depth + 1, filter);
        }

        var matches = MatchesFilter(node, filter);
        var childMatches = rows.Count > childStart;

        if (!string.IsNullOrEmpty(filter) && !matches && !childMatches)
        {
            rows.RemoveAt(insertIndex);
            return false;
        }

        return true;
    }

    private string GetFilterText()
    {
        var text = searchBox.Text?.Trim() ?? string.Empty;
        return !options.ShowDocumentOutlineSearch || text.Equals(searchPlaceholder, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : text;
    }

    private static bool MatchesFilter(DesignControlNode node, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        return node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || node.TypeName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || DesignerSpecialContainers.GetOutlineName(node).Contains(filter, StringComparison.OrdinalIgnoreCase)
            || DesignerSpecialContainers.GetOutlineType(node).Contains(filter, StringComparison.OrdinalIgnoreCase);
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

    private bool IsExpanderHit(OutlineRow row, int x)
        => row.HasChildren && x >= GetIndent(row) && x < GetIndent(row) + 16;

    private void ToggleCollapsed(OutlineRow row)
    {
        if (row.Node is null)
        {
            isRootCollapsed = !isRootCollapsed;
            Invalidate();
            return;
        }

        if (!collapsedNodes.Remove(row.Node))
            collapsedNodes.Add(row.Node);

        Invalidate();
    }

    private void PruneCollapsedNodes()
    {
        var liveNodes = state.EnumerateNodes()
            .Select(item => item.Node)
            .ToHashSet();
        collapsedNodes.RemoveWhere(node => !liveNodes.Contains(node));
        isRootCollapsed &= state.Document.Controls.Count > 0;
    }

    private string GetDisplayText(OutlineRow row)
        => DisplayMode switch
        {
            OutlineDisplayMode.NameOnly => row.Name,
            OutlineDisplayMode.Full => $"{row.Name}  {row.FullTypeName}",
            _ => $"{row.Name}  {row.TypeName}"
        };

    private OutlineDisplayMode DisplayMode
        => displayModeBox.SelectedIndex switch
        {
            0 => OutlineDisplayMode.NameOnly,
            2 => OutlineDisplayMode.Full,
            _ => OutlineDisplayMode.Short
        };

    private int SearchTop => options.ShowDocumentOutlineToolbar
        ? ToolbarTop + ToolbarHeight + 4
        : HeaderHeight + 4;

    private int TreeTop
    {
        get
        {
            var top = HeaderHeight + 4;

            if (options.ShowDocumentOutlineToolbar)
                top += ToolbarHeight + 4;

            if (options.ShowDocumentOutlineSearch)
                top += SearchHeight + 4;

            return top;
        }
    }

    private static int GetIndent(OutlineRow row)
        => 8 + (row.Depth * 20);

    private static string GetGlyph(string typeName)
        => typeName switch
        {
            "Panel" => "[]",
            "SplitterPanel" => "[]",
            "SplitContainer" => "[]",
            "TabControl" => "T",
            "TabPage" => "[]",
            "FlowLayoutPanel" => ">>",
            "TableLayoutPanel" => "#",
            "Button" => "ab",
            "Label" => "A",
            "TextBox" => "[]",
            _ => "+"
        };

    private sealed record OutlineRow(
        DesignControlNode? Node,
        int Depth,
        string Name,
        string TypeName,
        string FullTypeName,
        bool HasChildren,
        bool IsCollapsed);

    private enum OutlineDisplayMode
    {
        NameOnly,
        Short,
        Full
    }
}
