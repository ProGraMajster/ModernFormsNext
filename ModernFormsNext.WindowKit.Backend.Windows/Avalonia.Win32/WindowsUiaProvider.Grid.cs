using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

internal partial class WindowsUiaProvider : IGridProvider, IGridItemProvider, ITableProvider, ITableItemProvider,
    IGridProviderAbi, IGridItemProviderAbi, ITableProviderAbi, ITableItemProviderAbi
{
    private static bool IsGridPatternAvailable(IPlatformAccessibleObject node, int pattern)
        => pattern switch
        {
            WindowsUiaGridIds.Grid => node.GetGridInfo() is not null,
            WindowsUiaGridIds.Table => node.GetGridInfo() is { IsTable: true },
            WindowsUiaGridIds.GridItem => node.GetGridCell() is not null,
            WindowsUiaGridIds.TableItem => node.GetGridCell()?.Grid.GetGridInfo() is { IsTable: true },
            _ => false
        };
    private static bool IsGridProperty(int property)
        => property is >= WindowsUiaGridIds.RowCount and <= WindowsUiaGridIds.ContainingGrid
            or >= WindowsUiaGridIds.RowHeaders and <= WindowsUiaGridIds.ColumnHeaderItems
            or WindowsUiaGridIds.GridAvailable or WindowsUiaGridIds.GridItemAvailable
            or WindowsUiaGridIds.TableAvailable or WindowsUiaGridIds.TableItemAvailable;

    private object? GridProperty(IPlatformAccessibleObject node, int property)
    {
        var grid = node.GetGridInfo();
        var cell = node.GetGridCell();
        return property switch
        {
            WindowsUiaGridIds.GridAvailable => grid is not null,
            WindowsUiaGridIds.GridItemAvailable => cell is not null,
            WindowsUiaGridIds.TableAvailable => grid is { IsTable: true },
            WindowsUiaGridIds.TableItemAvailable => cell?.Grid.GetGridInfo() is { IsTable: true },
            WindowsUiaGridIds.RowCount => grid?.Rows,
            WindowsUiaGridIds.ColumnCount => grid?.Columns,
            WindowsUiaGridIds.Row => cell?.Row,
            WindowsUiaGridIds.Column => cell?.Column,
            WindowsUiaGridIds.RowSpan => cell?.RowSpan,
            WindowsUiaGridIds.ColumnSpan => cell?.ColumnSpan,
            WindowsUiaGridIds.ContainingGrid => cell is null ? null : CheckedGridProvider(cell.Grid),
            WindowsUiaGridIds.Traversal => grid is { IsTable: true } table ? table.Traversal : null,
            WindowsUiaGridIds.RowHeaders => grid is { IsTable: true } ? GridHeaders(node, true) : null,
            WindowsUiaGridIds.ColumnHeaders => grid is { IsTable: true } ? GridHeaders(node, false) : null,
            WindowsUiaGridIds.RowHeaderItems => cell is null ? null : HeaderProviders(cell.RowHeaders),
            WindowsUiaGridIds.ColumnHeaderItems => cell is null ? null : HeaderProviders(cell.ColumnHeaders),
            _ => null
        };
    }

    private WindowsUiaProvider CheckedGridProvider(IPlatformAccessibleObject node)
    {
        var provider = Context.GetOrCreate(node);
        _ = provider.PlatformObject; // Validate returned cells/headers against the same captured HWND.
        return provider;
    }

    private WindowsUiaProvider[] HeaderProviders(IReadOnlyList<IPlatformAccessibleObject> headers)
    {
        // Native SAFEARRAY requests are bounded independently of snapshot traversal limits.
        if (headers.Count > 65536) throw new InvalidOperationException("The table header result exceeds the native operation limit.");
        var result = new WindowsUiaProvider[headers.Count];
        for (int i = 0; i < result.Length; i++) result[i] = CheckedGridProvider(headers[i]);
        return result;
    }
    private WindowsUiaProvider[] GridHeaders(IPlatformAccessibleObject node, bool rows)
    {
        if (node.GetGridInfo() is not { IsTable: true } info || node is not IPlatformAccessibilityGrid grid)
            throw new InvalidOperationException("The object does not expose table headers.");
        // Reject an oversized axis before asking the canonical provider to materialize headers.
        if ((rows ? info.Rows : info.Columns) > 65536)
            throw new InvalidOperationException("The table header result exceeds the native operation limit.");
        return HeaderProviders(grid.GetGridHeaders(rows));
    }
    private T ReadGrid<T>(Func<PlatformAccessibleGridInfo, T> read)
        => Read(node => read(node.GetGridInfo() ?? throw new InvalidOperationException("The object has no grid capability.")));
    private T ReadGridCell<T>(Func<PlatformAccessibleGridCell, T> read)
        => Read(node => read(node.GetGridCell() ?? throw new InvalidOperationException("The object has no grid cell capability.")));

    int IGridProvider.RowCount => ReadGrid(info => info.Rows);
    int IGridProvider.ColumnCount => ReadGrid(info => info.Columns);
    IRawElementProviderSimple? IGridProvider.GetItem(int row, int column)
        => Read(node =>
        {
            var info = node.GetGridInfo() ?? throw new InvalidOperationException("The object has no grid capability.");
            if (row < 0 || column < 0 || row >= info.Rows || column >= info.Columns)
                throw new ArgumentOutOfRangeException(nameof(row), "The coordinate is outside the grid.");
            var item = ((IPlatformAccessibilityGrid)node).GetGridItem(row, column);
            return item is null ? null : CheckedGridProvider(item);
        });
    int IGridItemProvider.Row => ReadGridCell(cell => cell.Row);
    int IGridItemProvider.Column => ReadGridCell(cell => cell.Column);
    int IGridItemProvider.RowSpan => ReadGridCell(cell => cell.RowSpan);
    int IGridItemProvider.ColumnSpan => ReadGridCell(cell => cell.ColumnSpan);
    IRawElementProviderSimple IGridItemProvider.ContainingGrid => ReadGridCell(cell => CheckedGridProvider(cell.Grid));
    IRawElementProviderSimple[] ITableProvider.GetRowHeaders() => Read(node => GridHeaders(node, true));
    IRawElementProviderSimple[] ITableProvider.GetColumnHeaders() => Read(node => GridHeaders(node, false));
    int ITableProvider.RowOrColumnMajor => ReadGrid(info => info.IsTable ? info.Traversal : throw new InvalidOperationException("The object has no table capability."));
    IRawElementProviderSimple[] ITableItemProvider.GetRowHeaderItems() => ReadGridCell(cell => HeaderProviders(cell.RowHeaders));
    IRawElementProviderSimple[] ITableItemProvider.GetColumnHeaderItems() => ReadGridCell(cell => HeaderProviders(cell.ColumnHeaders));

    public int AbiGetGridItem(int row, int column, out IntPtr provider)
        => TryGet(() => ((IGridProvider)this).GetItem(row, column) is WindowsUiaProvider item
            ? WindowsUiaNativeMethods.GetSimpleProviderPointer(item) : IntPtr.Zero, out provider);
    public int AbiGetRowCount(out int value) => TryGet(() => ((IGridProvider)this).RowCount, out value);
    public int AbiGetColumnCount(out int value) => TryGet(() => ((IGridProvider)this).ColumnCount, out value);
    public int AbiGetRow(out int value) => TryGet(() => ((IGridItemProvider)this).Row, out value);
    public int AbiGetColumn(out int value) => TryGet(() => ((IGridItemProvider)this).Column, out value);
    public int AbiGetRowSpan(out int value) => TryGet(() => ((IGridItemProvider)this).RowSpan, out value);
    public int AbiGetColumnSpan(out int value) => TryGet(() => ((IGridItemProvider)this).ColumnSpan, out value);
    public int AbiGetContainingGrid(out IntPtr value) => TryGet(() => WindowsUiaNativeMethods.GetSimpleProviderPointer(
        (WindowsUiaProvider)((IGridItemProvider)this).ContainingGrid), out value);
    public int AbiGetRowHeaders(out IntPtr value) => TryGet(() => WindowsUiaNativeMethods.CreateSafeArray(((ITableProvider)this).GetRowHeaders().Cast<WindowsUiaProvider>().ToArray()), out value);
    public int AbiGetColumnHeaders(out IntPtr value) => TryGet(() => WindowsUiaNativeMethods.CreateSafeArray(((ITableProvider)this).GetColumnHeaders().Cast<WindowsUiaProvider>().ToArray()), out value);
    public int AbiGetRowOrColumnMajor(out int value) => TryGet(() => ((ITableProvider)this).RowOrColumnMajor, out value);
    public int AbiGetRowHeaderItems(out IntPtr value) => TryGet(() => WindowsUiaNativeMethods.CreateSafeArray(((ITableItemProvider)this).GetRowHeaderItems().Cast<WindowsUiaProvider>().ToArray()), out value);
    public int AbiGetColumnHeaderItems(out IntPtr value) => TryGet(() => WindowsUiaNativeMethods.CreateSafeArray(((ITableItemProvider)this).GetColumnHeaderItems().Cast<WindowsUiaProvider>().ToArray()), out value);
}
