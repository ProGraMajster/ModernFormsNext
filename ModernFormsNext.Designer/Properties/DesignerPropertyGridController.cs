using System.Drawing;
using ModernFormsNext;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerPropertyGridController
{
    private readonly DesignerPropertyGridState state;
    private readonly DesignerPropertyGridRenderer renderer;

    public DesignerPropertyGridController(DesignerPropertyGridState state, DesignerPropertyGridRenderer renderer)
    {
        this.state = state;
        this.renderer = renderer;
    }

    public bool HandleMouseDown(DesignerPropertyGrid grid, int x, int y, int scrollOffset)
    {
        if (TryHandleToolbar(x, y, grid.Width))
            return true;

        if (!TryGetRowAt(y, scrollOffset, out var row, out var rowBounds))
            return false;

        if (row.Kind == DesignerPropertyGridRowKind.Category)
            return true;

        if (row.Property is { HasChildren: true } property && IsExpansionGlyphHit(property, x))
        {
            state.SelectRow(row);
            state.ToggleExpansion(property);
            return true;
        }

        state.SelectRow(row);

        if (row.Property?.HasDialogEditor == true && IsDialogButtonHit(grid.Width, rowBounds, x))
        {
            grid.OpenDialogEditor(row);
            return true;
        }

        return true;
    }

    public bool TryBeginEdit(DesignerPropertyGrid grid, int x, int y, int scrollOffset)
    {
        if (!TryGetRowAt(y, scrollOffset, out var row, out var rowBounds))
            return false;

        if (row.Kind == DesignerPropertyGridRowKind.Category || x < GetValueLeft(grid.Width) || !IsEditable(row))
            return false;

        state.SelectRow(row);

        grid.BeginEdit(row, GetValueCellBounds(grid.Width, rowBounds));
        return true;
    }

    public bool TryCreateDefaultEventHandler(
        int x,
        int y,
        int width,
        int scrollOffset,
        out DesignerEventDescriptor? eventDescriptor,
        out string handlerName)
    {
        eventDescriptor = null;
        handlerName = string.Empty;

        if (!TryGetRowAt(y, scrollOffset, out var row, out _)
            || row.Event is null
            || x < GetValueLeft(width))
        {
            return false;
        }

        state.SelectRow(row);
        return state.TryCreateDefaultEventHandler(out eventDescriptor, out handlerName);
    }

    public bool TryBeginEditSelected(DesignerPropertyGrid grid, int scrollOffset)
    {
        var currentY = DesignerPropertyGridMetrics.GridTop - scrollOffset;

        foreach (var row in state.Rows)
        {
            var rowHeight = row.Kind == DesignerPropertyGridRowKind.Category
                ? DesignerPropertyGridMetrics.CategoryRowHeight
                : DesignerPropertyGridMetrics.RowHeight;

            var selected = row.Property is not null && ReferenceEquals(row.Property, state.SelectedProperty)
                || row.Event is not null && ReferenceEquals(row.Event, state.SelectedEvent);

            if (selected && IsEditable(row))
            {
                grid.BeginEdit(row, GetValueCellBounds(grid.Width, new Rectangle(DesignerPropertyGridMetrics.HorizontalPadding, currentY, 1, rowHeight)));
                return true;
            }

            currentY += rowHeight;
        }

        return false;
    }

    public int HandleMouseWheel(MouseEventArgs e, int height, int currentOffset)
    {
        var contentHeight = renderer.GetContentHeight(state);
        var viewportHeight = renderer.GetGridViewportHeight(height);
        var maxOffset = Math.Max(0, contentHeight - viewportHeight);

        if (maxOffset == 0)
            return 0;

        var delta = e.Delta.Y == 0 ? 0 : -Math.Sign(e.Delta.Y) * (DesignerPropertyGridMetrics.RowHeight * 3);
        return Math.Clamp(currentOffset + delta, 0, maxOffset);
    }

    private bool TryHandleToolbar(int x, int y, int width)
    {
        var top = DesignerPropertyGridMetrics.HeaderTop + DesignerPropertyGridMetrics.ObjectHeaderHeight + 5;

        if (y < top || y > top + 22)
            return false;

        if (Hit(x, 8, 88))
        {
            state.SetSortMode(DesignerPropertySortMode.Categorized);
            return true;
        }

        if (Hit(x, 100, 44))
        {
            state.SetSortMode(DesignerPropertySortMode.Alphabetical);
            return true;
        }

        if (Hit(x, 154, 82))
        {
            state.SetMode(DesignerPropertyGridMode.Properties);
            return true;
        }

        if (Hit(x, 240, Math.Max(58, width - 248)))
        {
            state.SetMode(DesignerPropertyGridMode.Events);
            return true;
        }

        return false;
    }

    private bool TryGetRowAt(int y, int scrollOffset, out DesignerPropertyGridRow row, out Rectangle rowBounds)
    {
        var currentY = DesignerPropertyGridMetrics.GridTop - scrollOffset;

        foreach (var candidate in state.Rows)
        {
            var rowHeight = candidate.Kind == DesignerPropertyGridRowKind.Category
                ? DesignerPropertyGridMetrics.CategoryRowHeight
                : DesignerPropertyGridMetrics.RowHeight;

            if (y >= currentY && y < currentY + rowHeight)
            {
                row = candidate;
                rowBounds = new Rectangle(DesignerPropertyGridMetrics.HorizontalPadding, currentY, 1, rowHeight);
                return true;
            }

            currentY += rowHeight;
        }

        row = null!;
        rowBounds = Rectangle.Empty;
        return false;
    }

    private static Rectangle GetValueCellBounds(int width, Rectangle rowBounds)
    {
        var contentLeft = DesignerPropertyGridMetrics.HorizontalPadding;
        var contentWidth = Math.Max(1, width - (DesignerPropertyGridMetrics.HorizontalPadding * 2));
        var valueLeft = GetValueLeft(width);
        var valueWidth = Math.Max(1, contentLeft + contentWidth - valueLeft);

        return new Rectangle(
            valueLeft + 1,
            rowBounds.Y + 1,
            Math.Max(1, valueWidth - 2),
            Math.Max(1, rowBounds.Height - 2));
    }

    private static bool IsDialogButtonHit(int width, Rectangle rowBounds, int x)
    {
        var valueBounds = GetValueCellBounds(width, rowBounds);
        var dialogLeft = valueBounds.Right - 27;
        return x >= dialogLeft && x <= valueBounds.Right;
    }

    private static int GetValueLeft(int width)
    {
        var contentLeft = DesignerPropertyGridMetrics.HorizontalPadding;
        var contentWidth = Math.Max(1, width - (DesignerPropertyGridMetrics.HorizontalPadding * 2));
        return contentLeft + Math.Max(110, (int)(contentWidth * 0.46));
    }

    private static bool IsEditable(DesignerPropertyGridRow row)
        => row.Property is { IsReadOnly: false } || row.Event is not null;

    private static bool IsExpansionGlyphHit(DesignerPropertyDescriptor property, int x)
    {
        var left = DesignerPropertyGridMetrics.HorizontalPadding
            + 6
            + (property.Depth * DesignerPropertyGridMetrics.PropertyIndent);

        return x >= left && x <= left + DesignerPropertyGridMetrics.ExpansionGlyphWidth;
    }

    private static bool Hit(int x, int left, int width)
        => x >= left && x <= left + width;
}
