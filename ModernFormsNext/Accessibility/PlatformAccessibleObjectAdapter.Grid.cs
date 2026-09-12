using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.Accessibility;

internal sealed partial class PlatformAccessibleObjectAdapter : IPlatformAccessibilityGrid
{
    public PlatformAccessibleGridInfo? GridInfo
    {
        get
        {
            if (PlatformAccessibilityPrivacy.HasSensitiveAncestor(this) || accessible_object.GridProvider is not { } grid) return null;
            var info = new PlatformAccessibleGridInfo(grid.RowCount, grid.ColumnCount, grid.IsTable, (int)grid.Traversal);
            return PlatformAccessibilityPrivacy.HasSensitiveAncestor(this) || info.Rows < 0 || info.Columns < 0 || info.Traversal is < 0 or > 2 ? null : info;
        }
    }
    public PlatformAccessibleGridCell? GridCell
    {
        get
        {
            if (PlatformAccessibilityPrivacy.HasSensitiveAncestor(this) || accessible_object.GridCell is not { } cell) return null;
            var result = new PlatformAccessibleGridCell(From(cell.Grid)!, cell.Row, cell.Column, cell.RowSpan, cell.ColumnSpan,
                cell.RowHeaders.Select(header => From(header)!).ToArray(), cell.ColumnHeaders.Select(header => From(header)!).ToArray());
            return PlatformAccessibilityPrivacy.HasSensitiveAncestor(this) ? null : result;
        }
    }
    public IPlatformAccessibleObject? GetGridItem(int row, int column)
    {
        if (PlatformAccessibilityPrivacy.HasSensitiveAncestor(this) || accessible_object.GridProvider is not { } grid) return null;
        var cell = grid.GetItem(row, column);
        return PlatformAccessibilityPrivacy.HasSensitiveAncestor(this) ? null : From(cell);
    }
    public IReadOnlyList<IPlatformAccessibleObject> GetGridHeaders(bool rows)
    {
        if (PlatformAccessibilityPrivacy.HasSensitiveAncestor(this) || accessible_object.GridProvider is not { IsTable: true } grid) return [];
        var headers = rows ? grid.GetRowHeaders() : grid.GetColumnHeaders();
        if (headers.Count > 65536) throw new InvalidOperationException("The table header result exceeds the native operation limit.");
        var result = new IPlatformAccessibleObject[headers.Count];
        for (int i = 0; i < result.Length; ++i) result[i] = From(headers[i])!;
        return PlatformAccessibilityPrivacy.HasSensitiveAncestor(this) ? [] : result;
    }
}
