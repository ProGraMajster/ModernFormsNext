namespace ModernFormsNext.WindowKit.Platform.Accessibility;

/// <summary>Optional internal grid projection over the canonical accessible hierarchy.</summary>
internal interface IPlatformAccessibilityGrid
{
    PlatformAccessibleGridInfo? GridInfo { get; }
    PlatformAccessibleGridCell? GridCell { get; }
    IPlatformAccessibleObject? GetGridItem(int row, int column);
    IReadOnlyList<IPlatformAccessibleObject> GetGridHeaders(bool rows);
}

internal readonly record struct PlatformAccessibleGridInfo(int Rows, int Columns, bool IsTable, int Traversal);
internal sealed record PlatformAccessibleGridCell(IPlatformAccessibleObject Grid, int Row, int Column,
    int RowSpan, int ColumnSpan, IReadOnlyList<IPlatformAccessibleObject> RowHeaders,
    IReadOnlyList<IPlatformAccessibleObject> ColumnHeaders);

internal static class PlatformAccessibilityGridExtensions
{
    internal static PlatformAccessibleGridInfo? GetGridInfo(this IPlatformAccessibleObject node)
    {
        if (node is not IPlatformAccessibilityGrid grid || PlatformAccessibilityPrivacy.HasSensitiveAncestor(node)) return null;
        var result = grid.GridInfo;
        return !PlatformAccessibilityPrivacy.HasSensitiveAncestor(node) ? result : null;
    }
    internal static PlatformAccessibleGridCell? GetGridCell(this IPlatformAccessibleObject node)
    {
        if (node is not IPlatformAccessibilityGrid grid || PlatformAccessibilityPrivacy.HasSensitiveAncestor(node)) return null;
        var result = grid.GridCell;
        return !PlatformAccessibilityPrivacy.HasSensitiveAncestor(node) ? result : null;
    }
}
