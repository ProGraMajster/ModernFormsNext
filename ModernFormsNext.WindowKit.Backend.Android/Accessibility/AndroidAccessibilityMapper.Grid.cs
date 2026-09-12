using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

internal static partial class AndroidAccessibilityMapper
{
    internal static AndroidGridCell? GridCell(IPlatformAccessibleObject node)
    {
        if (node.GetGridCell() is not { } cell || cell.Grid.GetGridInfo() is not { } grid
            || cell.Row < 0 || cell.Column < 0 || cell.RowSpan <= 0 || cell.ColumnSpan <= 0
            || (long)cell.Row + cell.RowSpan > grid.Rows || (long)cell.Column + cell.ColumnSpan > grid.Columns)
            return null;
        return new(cell.Row, cell.Column, cell.RowSpan, cell.ColumnSpan);
    }
}

internal readonly record struct AndroidGridCell(int Row, int Column, int RowSpan, int ColumnSpan);
