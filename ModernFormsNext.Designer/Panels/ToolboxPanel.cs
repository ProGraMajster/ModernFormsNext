using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;
using SkiaSharp;

namespace ModernFormsNext.Designer.Panels;

internal sealed class ToolboxPanel : DesignerPanelBase
{
    private const int SearchTop = 36;
    private const int SearchHeight = 25;
    private const int CategoryTop = 72;
    private const int RowHeight = 26;

    private readonly DesignerCommandService commands;
    private readonly IReadOnlyList<DesignerToolboxItem> items;
    private readonly List<ToolboxRow> rows = [];
    private readonly TextBox searchBox;
    private int scrollOffset;

    public ToolboxPanel(DesignerCommandService commands, string title = "Toolbox", string searchText = "Search Toolbox")
        : base(title)
    {
        this.commands = commands;
        items = new DesignerToolboxService().GetItems();

        searchBox = Controls.Add(new TextBox
        {
            Left = 8,
            Top = SearchTop,
            Width = 240,
            Height = SearchHeight,
            Text = searchText
        });

        searchBox.TextChanged += (_, _) =>
        {
            scrollOffset = 0;
            Invalidate();
        };
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Y < CategoryTop)
            return;

        var index = rows.FindIndex(row => e.Y >= row.Top && e.Y < row.Top + RowHeight);

        if (index < 0)
            return;

        var item = rows[index].Item;

        if (item is null)
            return;

        if (item.IsComponent)
            commands.AddComponentType(item.TypeName);
        else
            commands.AddControlType(item.TypeName);

        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        var contentHeight = rows.Count * RowHeight;
        var viewportHeight = Math.Max(1, Height - CategoryTop);
        var maxOffset = Math.Max(0, contentHeight - viewportHeight);
        var delta = e.Delta.Y == 0 ? 0 : -Math.Sign(e.Delta.Y) * (RowHeight * 3);
        scrollOffset = Math.Clamp(scrollOffset + delta, 0, maxOffset);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        searchBox.Width = Math.Max(1, Width - 16);
        rows.Clear();

        var filter = GetFilterText();
        var y = CategoryTop - scrollOffset;
        var groupedItems = items
            .Where(item => MatchesFilter(item, filter))
            .GroupBy(item => item.Category)
            .OrderBy(group => GetCategoryRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in groupedItems)
        {
            rows.Add(new ToolboxRow(null, y));
            DrawRowText(e, group.Key, 18, y, DesignerColors.Text);
            y += RowHeight;

            foreach (var item in group)
            {
                rows.Add(new ToolboxRow(item, y));

                if (IsRowVisible(y))
                {
                    e.Canvas.DrawText(
                        GetGlyph(item),
                        Theme.UIFont,
                        e.LogicalToDeviceUnits(Theme.FontSize),
                        new System.Drawing.Rectangle(e.LogicalToDeviceUnits(24), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(22), e.LogicalToDeviceUnits(RowHeight)),
                        item.IsComponent ? DesignerColors.MutedText : DesignerColors.Text,
                        ContentAlignment.MiddleCenter);
                }

                DrawRowText(e, item.DisplayName, 50, y, item.IsComponent ? DesignerColors.MutedText : DesignerColors.Text);
                y += RowHeight;
            }
        }

        DrawScrollHint(e);
    }

    private string GetFilterText()
    {
        var text = searchBox.Text?.Trim() ?? string.Empty;
        return text.Equals("Search Toolbox", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Szukaj w przyborniku", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : text;
    }

    private static bool MatchesFilter(DesignerToolboxItem item, string filter)
        => string.IsNullOrWhiteSpace(filter)
        || item.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || item.TypeName.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || item.Category.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static int GetCategoryRank(string category)
        => category switch
        {
            "Common" => 0,
            "Containers" => 1,
            "Components" => 2,
            _ => 10
        };

    private void DrawRowText(PaintEventArgs e, string text, int x, int y, SKColor color)
    {
        if (!IsRowVisible(y))
            return;

        e.Canvas.DrawText(
            text,
            Theme.UIFont,
            e.LogicalToDeviceUnits(Theme.FontSize),
            new System.Drawing.Rectangle(e.LogicalToDeviceUnits(x), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(Math.Max(1, Width - x - 8)), e.LogicalToDeviceUnits(RowHeight)),
            color,
            ContentAlignment.MiddleLeft,
            maxLines: 1,
            ellipsis: true);
    }

    private bool IsRowVisible(int y)
        => y + RowHeight >= CategoryTop && y <= Height;

    private void DrawScrollHint(PaintEventArgs e)
    {
        var contentHeight = rows.Count * RowHeight;
        var viewportHeight = Math.Max(1, Height - CategoryTop);

        if (contentHeight <= viewportHeight)
            return;

        var trackTop = CategoryTop;
        var trackHeight = Math.Max(1, Height - CategoryTop - 4);
        var thumbHeight = Math.Max(24, trackHeight * viewportHeight / contentHeight);
        var maxOffset = Math.Max(1, contentHeight - viewportHeight);
        var thumbTop = trackTop + ((trackHeight - thumbHeight) * scrollOffset / maxOffset);
        e.Canvas.FillRectangle(Math.Max(0, Width - 5), thumbTop, 3, thumbHeight, DesignerColors.MutedText);
    }

    private static string GetGlyph(DesignerToolboxItem item)
        => item.TypeName switch
        {
            "Panel" => "[]",
            "Button" => "ab",
            "Label" => "A",
            "TextBox" => "[]",
            "CheckBox" => "x",
            "RadioButton" => "o",
            "ComboBox" => "v",
            "ListBox" => "#",
            "TreeView" => "+",
            "TabControl" => "T",
            _ => item.IsComponent ? "*" : "+"
        };

    private sealed record ToolboxRow(DesignerToolboxItem? Item, int Top);
}
