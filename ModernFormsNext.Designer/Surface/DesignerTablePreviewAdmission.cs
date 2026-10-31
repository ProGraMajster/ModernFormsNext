using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

// Preflight borrowed, structurally validated data before ANY table property setter, child
// admission, layout, measurement or grid painting. No allocation/loop is sized by a strip count.
internal static class DesignerTablePreviewAdmission
{
    internal static bool TryValidate(IEnumerable<DesignControlNode> roots, DesignerPreviewResourcePolicy policy,
        out string? error)
    {
        error = null;
        long cells = 0, work = 0;
        var pending = new Stack<DesignControlNode>(roots);
        while (pending.TryPop(out var node))
        {
            if (DesignerSpecialContainers.IsTableLayoutPanel(node)
                || DesignerSurfaceRenderer.ResolveFrameworkControlType(node.TypeName) is { } type
                    && typeof(TableLayoutPanel).IsAssignableFrom(type))
            {
                try
                {
                    var cost = GetCost(node);
                    cells = checked(cells + checked(cost.Columns * cost.Rows));
                    work = checked(work + cost.Work);
                    if (cost.Columns > DesignerPreviewResourcePolicy.HardMaximumTableStrips
                        || cost.Rows > DesignerPreviewResourcePolicy.HardMaximumTableStrips
                        || cells > DesignerPreviewResourcePolicy.HardMaximumTableCells
                        || work > DesignerPreviewResourcePolicy.HardMaximumTableLayoutWork)
                        throw new InvalidOperationException("Table preview exceeds its hard resource safety budget.");
                    if (policy.MaximumTableStrips > 0 && Math.Max(cost.Columns, cost.Rows) > policy.MaximumTableStrips
                        || policy.MaximumTableCells > 0 && cells > policy.MaximumTableCells)
                        throw new InvalidOperationException("Table preview exceeds the configured resource policy. A trusted host can raise or disable its ordinary limits.");
                }
                catch (Exception exception) when (exception is FormatException or OverflowException or InvalidOperationException)
                {
                    string reason = exception is OverflowException ? "Table preview exceeds its arithmetic safety budget." : exception.Message;
                    error = $"{node.Name}: {reason} Layout is deferred; authored data is retained.";
                    return false;
                }
            }
            foreach (var child in node.Children) pending.Push(child);
        }
        return true;
    }

    private static (long Columns, long Rows, long Work) GetCost(DesignControlNode table)
    {
        // LayoutSettings paths would mutate the same runtime counts through a second spelling.
        // Preview only supports the canonical data members that this preflight understands.
        foreach (string path in table.Properties.Keys)
        {
            string[] parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.FirstOrDefault() == "LayoutSettings")
                throw new FormatException("Table preview requires canonical ColumnCount, RowCount and GrowStyle properties.");
        }
        long columns = Read(table, "ColumnCount", 0, 0);
        long rows = Read(table, "RowCount", 0, 0);
        var grow = TableLayoutPanelGrowStyle.AddRows;
        if (table.Properties.TryGetValue("GrowStyle", out var value))
        {
            string? type = value.EnumTypeName?.Trim();
            if (type?.StartsWith("global::", StringComparison.Ordinal) == true) type = type[8..];
            if (value.Kind != DesignPropertyValueKind.Enum || type != typeof(TableLayoutPanelGrowStyle).FullName
                || value.Value is not string name || !Enum.TryParse(name, out grow)
                || name != grow.ToString() || !Enum.IsDefined(grow))
                throw new FormatException("Table preview requires a declared GrowStyle selection.");
        }

        long columnSpans = 0, rowSpans = 0, columnPosition = 0, rowPosition = 0, spanCells = 0;
        foreach (var child in table.Children)
        {
            long column = Read(child, "TableColumn", -1, -1), row = Read(child, "TableRow", -1, -1);
            long columnSpan = Read(child, "TableColumnSpan", 1, 1), rowSpan = Read(child, "TableRowSpan", 1, 1);
            // Promote BEFORE addition/multiplication. Even a clipped span must not wrap an
            // intermediate Int32 in the production placement/reservation implementation.
            if (columnSpan + Math.Max(0, column) > int.MaxValue || rowSpan + Math.Max(0, row) > int.MaxValue)
                throw new FormatException("Table placement and span endpoints must be representable.");
            columnSpans = checked(columnSpans + columnSpan);
            rowSpans = checked(rowSpans + rowSpan);
            columnPosition = Math.Max(columnPosition, Math.Max(0, column));
            rowPosition = Math.Max(rowPosition, Math.Max(0, row));
            spanCells = checked(spanCells + checked(columnSpan * rowSpan));
            if (grow == TableLayoutPanelGrowStyle.FixedSize && column >= 0 && row >= 0
                && (column + columnSpan > columns || row + rowSpan > rows))
                throw new FormatException("A fixed table placement exceeds its declared dimensions.");
        }
        if (grow == TableLayoutPanelGrowStyle.FixedSize && spanCells > columns * rows)
            throw new FormatException("A fixed table cannot contain its declared child spans.");

        // Each child can advance a cursor past reservations by at most its span, and fixed
        // insertion points can start at the greatest authored position. Summing spans bounds
        // collisions, fragmentation, partial placements and automatic growth without copying
        // the runtime placement algorithm or creating a dense/sparse second layout engine.
        // Include both axes conservatively, regardless of visibility and current grow style.
        long columnBound = Math.Max(1, Math.Max(columns, checked(columnPosition + columnSpans)));
        long rowBound = Math.Max(1, Math.Max(rows, checked(rowPosition + rowSpans)));
        // AddColumns starts from an area/row-count estimate, which can exceed the sum
        // of column spans when tall spans are clipped to a small fixed row count.
        if (grow == TableLayoutPanelGrowStyle.AddColumns)
            columnBound = Math.Max(columnBound, checked(columnPosition + spanCells));
        long attempts = grow == TableLayoutPanelGrowStyle.AddColumns || columns == 0 && rows > 0 ? columnBound : 1;
        long work = checked(checked((columnBound * rowBound) + spanCells)
            * Math.Max(1, table.Children.Count) * attempts);
        return (columnBound, rowBound, work);
    }

    private static int Read(DesignControlNode node, string property, int fallback, int minimum)
    {
        if (!node.Properties.TryGetValue(property, out var value)) return fallback;
        if (value.Kind != DesignPropertyValueKind.Int32 || value.Value is not int number || number < minimum)
            throw new FormatException($"{property} requires an Int32 greater than or equal to {minimum}.");
        return number;
    }
}
