using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Designer.Layout;
using SkiaSharp;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerPropertyGridRenderer
{
    private static readonly SKColor Row = new(39, 44, 50);
    private static readonly SKColor AlternatingRow = new(34, 39, 45);
    private static readonly SKColor CategoryRow = new(45, 50, 57);
    private static readonly SKColor GridLine = new(58, 65, 73);
    private static readonly SKColor SelectedRow = new(50, 88, 122);
    private static readonly SKColor SelectedValueCell = new(66, 103, 135);
    private static readonly SKColor EditingValueCell = new(248, 250, 252);
    private static readonly SKColor EditingText = new(12, 18, 24);
    private static readonly SKColor ButtonBackground = new(48, 54, 62);
    private static readonly SKColor ButtonSelected = new(0, 122, 204);

    public void Render(PaintEventArgs e, DesignerPropertyGridState state, int width, int height, int scrollOffset)
    {
        DrawObjectHeader(e, state, width);
        DrawToolbar(e, state, width);
        DrawRows(e, state, width, height, scrollOffset);
        DrawDescription(e, state, width, height);
    }

    public int GetContentHeight(DesignerPropertyGridState state)
        => state.Rows.Sum(row => row.Kind == DesignerPropertyGridRowKind.Category
            ? DesignerPropertyGridMetrics.CategoryRowHeight
            : DesignerPropertyGridMetrics.RowHeight);

    public int GetGridViewportHeight(int height)
        => Math.Max(1, height - DesignerPropertyGridMetrics.GridTop - DesignerPropertyGridMetrics.DescriptionHeight);

    private static void DrawObjectHeader(PaintEventArgs e, DesignerPropertyGridState state, int width)
    {
        var top = DesignerPropertyGridMetrics.HeaderTop;
        var height = DesignerPropertyGridMetrics.ObjectHeaderHeight;

        e.Canvas.FillRectangle(1, top, Math.Max(1, width - 2), height, new SKColor(32, 37, 43));
        e.Canvas.DrawLine(0, top + height, width, top + height, DesignerColors.PanelBorder);

        var nameWidth = Math.Min(Math.Max(80, width / 3), 130);
        DrawText(e, state.HeaderName, 10, top, nameWidth, height, DesignerColors.Text);
        DrawText(e, state.HeaderType, 14 + nameWidth, top, Math.Max(1, width - nameWidth - 24), height, DesignerColors.MutedText);
    }

    private static void DrawToolbar(PaintEventArgs e, DesignerPropertyGridState state, int width)
    {
        var top = DesignerPropertyGridMetrics.HeaderTop + DesignerPropertyGridMetrics.ObjectHeaderHeight;
        e.Canvas.FillRectangle(1, top, Math.Max(1, width - 2), DesignerPropertyGridMetrics.ToolbarHeight, new SKColor(36, 41, 47));
        e.Canvas.DrawLine(0, top + DesignerPropertyGridMetrics.ToolbarHeight, width, top + DesignerPropertyGridMetrics.ToolbarHeight, DesignerColors.PanelBorder);

        DrawToolbarButton(e, "Categorized", 8, top + 5, 88, state.SortMode == DesignerPropertySortMode.Categorized);
        DrawToolbarButton(e, "A-Z", 100, top + 5, 44, state.SortMode == DesignerPropertySortMode.Alphabetical);
        if (state.SupportsEvents)
        {
            DrawToolbarButton(e, "Properties", 154, top + 5, 82, state.Mode == DesignerPropertyGridMode.Properties);
            DrawToolbarButton(e, "Events", 240, top + 5, Math.Max(58, width - 248), state.Mode == DesignerPropertyGridMode.Events);
        }
    }

    private static void DrawToolbarButton(PaintEventArgs e, string text, int x, int y, int width, bool selected)
    {
        e.Canvas.FillRectangle(x, y, width, 22, selected ? ButtonSelected : ButtonBackground);
        e.Canvas.DrawRectangle(x, y, width, 22, selected ? new SKColor(92, 180, 230) : DesignerColors.PanelBorder);
        DrawText(e, text, x + 6, y, Math.Max(1, width - 12), 22, DesignerColors.Text);
    }

    private void DrawRows(PaintEventArgs e, DesignerPropertyGridState state, int width, int height, int scrollOffset)
    {
        var gridTop = DesignerPropertyGridMetrics.GridTop;
        var gridBottom = Math.Max(gridTop, height - DesignerPropertyGridMetrics.DescriptionHeight);
        var contentLeft = DesignerPropertyGridMetrics.HorizontalPadding;
        var contentWidth = Math.Max(1, width - (DesignerPropertyGridMetrics.HorizontalPadding * 2));
        var valueLeft = contentLeft + Math.Max(110, (int)(contentWidth * 0.46));
        var valueWidth = Math.Max(1, contentLeft + contentWidth - valueLeft);
        var y = gridTop - scrollOffset;
        var rowIndex = 0;

        e.Canvas.Save();
        e.Canvas.Clip(new Rectangle(0, gridTop, width, Math.Max(1, gridBottom - gridTop)));

        foreach (var row in state.Rows)
        {
            var rowHeight = row.Kind == DesignerPropertyGridRowKind.Category
                ? DesignerPropertyGridMetrics.CategoryRowHeight
                : DesignerPropertyGridMetrics.RowHeight;

            if (y + rowHeight >= gridTop && y <= gridBottom)
                DrawRow(e, state, row, rowIndex, contentLeft, contentWidth, valueLeft, valueWidth, y, rowHeight);

            y += rowHeight;
            rowIndex++;
        }

        if (state.Rows.Count == 0)
            DrawText(e, "No properties available.", 10, gridTop + 10, Math.Max(1, width - 20), 24, DesignerColors.MutedText);

        e.Canvas.Restore();
        DrawScrollIndicator(e, state, width, height, scrollOffset);
    }

    private static void DrawRow(
        PaintEventArgs e,
        DesignerPropertyGridState state,
        DesignerPropertyGridRow row,
        int rowIndex,
        int contentLeft,
        int contentWidth,
        int valueLeft,
        int valueWidth,
        int y,
        int rowHeight)
    {
        if (row.Kind == DesignerPropertyGridRowKind.Category)
        {
            e.Canvas.FillRectangle(contentLeft, y, contentWidth, rowHeight, CategoryRow);
            DrawText(e, row.CategoryName ?? "Misc", contentLeft + 8, y, contentWidth - 16, rowHeight, DesignerColors.Text);
            e.Canvas.DrawLine(contentLeft, y + rowHeight, contentLeft + contentWidth, y + rowHeight, GridLine);
            return;
        }

        var selected = row.Property is not null && ReferenceEquals(row.Property, state.SelectedProperty)
            || row.Event is not null && ReferenceEquals(row.Event, state.SelectedEvent);
        var editing = row.Property is not null && ReferenceEquals(row.Property, state.EditingProperty)
            || row.Event is not null && ReferenceEquals(row.Event, state.EditingEvent);
        var background = selected ? SelectedRow : rowIndex % 2 == 0 ? Row : AlternatingRow;

        e.Canvas.FillRectangle(contentLeft, y, contentWidth, rowHeight, background);
        if (selected)
            e.Canvas.FillRectangle(valueLeft, y, valueWidth, rowHeight, editing ? EditingValueCell : SelectedValueCell);
        e.Canvas.DrawLine(contentLeft, y + rowHeight, contentLeft + contentWidth, y + rowHeight, GridLine);
        e.Canvas.DrawLine(valueLeft - 1, y, valueLeft - 1, y + rowHeight, GridLine);

        var displayName = row.Property is not null ? row.Property.DisplayName : row.Event?.DisplayName ?? string.Empty;
        var valueText = editing ? state.EditingText : row.Property is not null ? row.Property.GetValueText() : row.Event?.GetValueText() ?? string.Empty;
        var nameColor = row.Property?.IsReadOnly == true ? DesignerColors.MutedText : DesignerColors.Text;
        var valueColor = editing ? EditingText : row.Property?.IsReadOnly == true ? DesignerColors.MutedText : DesignerColors.Text;
        var depth = row.Property?.Depth ?? 0;
        var indent = depth * DesignerPropertyGridMetrics.PropertyIndent;
        var glyphLeft = contentLeft + 6 + indent;
        var textLeft = contentLeft + 8 + indent + DesignerPropertyGridMetrics.ExpansionGlyphWidth;

        if (row.Property?.HasChildren == true)
            DrawExpansionGlyph(e, row.Property.IsExpanded, glyphLeft, y, rowHeight);

        DrawText(e, displayName, textLeft, y, Math.Max(1, valueLeft - textLeft - 6), rowHeight, nameColor);

        if (editing)
            DrawEditingValue(e, state, valueText, valueLeft, y, valueWidth, rowHeight);
        else
        {
            var dialogWidth = row.Property?.HasDialogEditor == true ? 26 : 0;
            DrawText(e, valueText, valueLeft + DesignerPropertyGridMetrics.ValuePadding, y, Math.Max(1, valueWidth - DesignerPropertyGridMetrics.ValuePadding - 2 - dialogWidth), rowHeight, valueColor);

            if (row.Property?.HasDialogEditor == true)
                DrawDialogButton(e, valueLeft + valueWidth - dialogWidth, y + 2, dialogWidth - 3, rowHeight - 4);
        }
    }

    private static void DrawExpansionGlyph(PaintEventArgs e, bool expanded, int x, int y, int rowHeight)
    {
        var centerY = y + (rowHeight / 2);
        using var paint = new SKPaint
        {
            Color = DesignerColors.MutedText,
            IsAntialias = false,
            StrokeWidth = 1
        };

        e.Canvas.DrawLine(x + 3, centerY, x + 11, centerY, paint);

        if (!expanded)
            e.Canvas.DrawLine(x + 7, centerY - 4, x + 7, centerY + 4, paint);
    }

    private static void DrawDialogButton(PaintEventArgs e, int x, int y, int width, int height)
    {
        e.Canvas.FillRectangle(x, y, Math.Max(1, width), Math.Max(1, height), new SKColor(54, 61, 69));
        e.Canvas.DrawRectangle(x, y, Math.Max(1, width), Math.Max(1, height), GridLine);
        DrawText(e, "...", x + 4, y, Math.Max(1, width - 8), Math.Max(1, height), DesignerColors.Text);
    }

    private static void DrawEditingValue(
        PaintEventArgs e,
        DesignerPropertyGridState state,
        string valueText,
        int valueLeft,
        int y,
        int valueWidth,
        int rowHeight)
    {
        var textLeft = valueLeft + DesignerPropertyGridMetrics.ValuePadding;
        var textWidth = Math.Max(1, valueWidth - DesignerPropertyGridMetrics.ValuePadding - 2);
        var selectionStart = Math.Clamp(Math.Min(state.EditingSelectionStart, state.EditingSelectionEnd), 0, valueText.Length);
        var selectionEnd = Math.Clamp(Math.Max(state.EditingSelectionStart, state.EditingSelectionEnd), 0, valueText.Length);

        if (selectionStart != selectionEnd)
        {
            var selectionX = textLeft + MeasureText(valueText[..selectionStart]);
            var selectionRight = textLeft + MeasureText(valueText[..selectionEnd]);
            var selectionWidth = Math.Max(2, (int)Math.Ceiling(selectionRight - selectionX));

            e.Canvas.FillRectangle(
                (int)Math.Floor(selectionX),
                y + 3,
                Math.Min(selectionWidth, Math.Max(1, valueLeft + valueWidth - (int)Math.Floor(selectionX) - 2)),
                Math.Max(1, rowHeight - 6),
                new SKColor(0, 120, 215));
        }

        DrawText(e, valueText, textLeft, y, textWidth, rowHeight, EditingText);

        if (selectionStart == selectionEnd)
        {
            var caretIndex = Math.Clamp(state.EditingCaretIndex, 0, valueText.Length);
            var caretX = textLeft + MeasureText(valueText[..caretIndex]);

            using var caretPaint = new SKPaint
            {
                Color = EditingText,
                IsAntialias = false,
                StrokeWidth = Math.Max(1, e.LogicalToDeviceUnits(1))
            };

            e.Canvas.DrawLine(caretX, y + 4, caretX, y + rowHeight - 4, caretPaint);
        }
    }

    private static float MeasureText(string text)
    {
        using var font = new SKFont(Theme.UIFont, Theme.FontSize);
        return font.MeasureText(text);
    }

    private void DrawScrollIndicator(PaintEventArgs e, DesignerPropertyGridState state, int width, int height, int scrollOffset)
    {
        var contentHeight = GetContentHeight(state);
        var viewportHeight = GetGridViewportHeight(height);

        if (contentHeight <= viewportHeight)
            return;

        var trackTop = DesignerPropertyGridMetrics.GridTop + 2;
        var trackHeight = Math.Max(1, viewportHeight - 4);
        var thumbHeight = Math.Max(24, (int)(trackHeight * (viewportHeight / (double)contentHeight)));
        var maxOffset = Math.Max(1, contentHeight - viewportHeight);
        var thumbTop = trackTop + (int)((trackHeight - thumbHeight) * (scrollOffset / (double)maxOffset));

        e.Canvas.FillRectangle(width - 6, trackTop, 4, trackHeight, new SKColor(45, 50, 57));
        e.Canvas.FillRectangle(width - 6, thumbTop, 4, thumbHeight, DesignerColors.MutedText);
    }

    private static void DrawDescription(PaintEventArgs e, DesignerPropertyGridState state, int width, int height)
    {
        var top = Math.Max(DesignerPropertyGridMetrics.GridTop, height - DesignerPropertyGridMetrics.DescriptionHeight);
        e.Canvas.FillRectangle(1, top, Math.Max(1, width - 2), DesignerPropertyGridMetrics.DescriptionHeight - 1, new SKColor(32, 37, 43));
        e.Canvas.DrawLine(0, top, width, top, DesignerColors.PanelBorder);

        DrawText(e, state.DescriptionTitle, 10, top + 8, Math.Max(1, width - 20), 22, DesignerColors.Text);
        DrawText(e, state.DescriptionText, 10, top + 32, Math.Max(1, width - 20), 38, DesignerColors.MutedText, maxLines: 2);
    }

    private static void DrawText(
        PaintEventArgs e,
        string text,
        int x,
        int y,
        int width,
        int height,
        SKColor color,
        int maxLines = 1)
    {
        e.Canvas.DrawText(
            text,
            Theme.UIFont,
            e.LogicalToDeviceUnits(Theme.FontSize),
            new Rectangle(e.LogicalToDeviceUnits(x), e.LogicalToDeviceUnits(y), e.LogicalToDeviceUnits(width), e.LogicalToDeviceUnits(height)),
            color,
            ContentAlignment.MiddleLeft,
            maxLines: maxLines,
            ellipsis: true);
    }
}
