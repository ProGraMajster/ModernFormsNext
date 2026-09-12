using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed partial class WindowsUiaProviderTests
{
    [Fact]
    public void OversizedTableAxisIsRejectedBeforeExtractingCanonicalHeaders()
    {
        using var control = new OversizedTableControl();
        using var surface = new SkiaControlSurface(control);
        using var root = WindowsUiaRootProvider.Create(new IntPtr(42), ((IPlatformAccessibilityHost)surface).AccessibilityRoot!, new InlineDispatcher());
        var error = Assert.Throws<InvalidOperationException>(() => ((ITableProvider)root).GetRowHeaders());
        Assert.Contains("native operation limit", error.Message);
        Assert.Equal(0, control.Headers.Reads);
    }

    private sealed class OversizedTableControl : Control
    {
        internal OversizedHeaders Headers { get; } = new();
        protected override ModernFormsNext.Accessibility.AccessibleObject CreateAccessibilityInstance() => new TablePeer(this);
        private sealed class TablePeer(OversizedTableControl owner) : ControlAccessibleObject(owner)
        {
            public override ModernFormsNext.Accessibility.AccessibleGridProvider GridProvider => owner.Headers;
        }
    }

    private sealed class OversizedHeaders : ModernFormsNext.Accessibility.AccessibleGridProvider
    {
        internal int Reads;
        public override int RowCount => 65537;
        public override int ColumnCount => 1;
        public override bool IsTable => true;
        public override ModernFormsNext.Accessibility.AccessibleObject? GetItem(int row, int column) => null;
        public override IReadOnlyList<ModernFormsNext.Accessibility.AccessibleObject> GetRowHeaders()
        { Reads++; throw new InvalidOperationException("Header extraction must not run."); }
    }

    [Fact]
    public void GridPatternsUseActualDataGridCellsHeadersSelectionAndReadOnlyValues()
    {
        using var grid = CreateSemanticGrid();
        using var surface = new SkiaControlSurface(grid);
        using var root = WindowsUiaRootProvider.Create(new IntPtr(42), ((IPlatformAccessibilityHost)surface).AccessibilityRoot!, new InlineDispatcher());
        Assert.Same(root, root.GetPatternProvider(WindowsUiaGridIds.Grid));
        Assert.Same(root, root.GetPatternProvider(WindowsUiaGridIds.Table));
        var pattern = (IGridProvider)root;
        Assert.Equal(2, pattern.RowCount);
        Assert.Equal(2, pattern.ColumnCount);
        var cell = Assert.IsType<WindowsUiaProvider>(pattern.GetItem(1, 1));
        var item = (IGridItemProvider)cell;
        Assert.Same(root, item.ContainingGrid);
        Assert.Equal(1, item.Row);
        Assert.Equal(1, item.Column);
        Assert.Equal(1, item.RowSpan);
        Assert.Equal(1, item.ColumnSpan);
        Assert.Equal(2, ((ITableProvider)root).GetRowHeaders().Length);
        var headers = ((ITableProvider)root).GetColumnHeaders();
        Assert.Equal(2, headers.Length);
        Assert.Same(headers[1], Assert.Single(((ITableItemProvider)cell).GetColumnHeaderItems()));
        Assert.Equal(0, ((ITableProvider)root).RowOrColumnMajor);
        ((ISelectionItemProvider)cell).Select();
        Assert.Same(cell, Assert.Single(((ISelectionProvider)root).GetSelection()));
        Assert.Same(root, ((ISelectionItemProvider)cell).SelectionContainer);
        grid.ReadOnly = true;
        Assert.Same(cell, cell.GetPatternProvider(WindowsUiaIds.ValuePattern));
        Assert.Equal("last", ((IValueProvider)cell).Value);
        Assert.True(((IValueProvider)cell).IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => ((IValueProvider)cell).SetValue("replacement"));
        Assert.Equal("last", grid.Rows[1].Cells[1].Value);
    }

    [Fact]
    public void NativeGridVtableReturnsExactSimpleProviderAndLiveItemCoordinates()
    {
        using var grid = CreateSemanticGrid();
        using var surface = new SkiaControlSurface(grid);
        using var root = WindowsUiaRootProvider.Create(new IntPtr(42), ((IPlatformAccessibilityHost)surface).AccessibilityRoot!, new InlineDispatcher());
        IntPtr unknown = WindowsUiaNativeMethods.GetUnknownPointer(root);
        IntPtr pattern = IntPtr.Zero, cell = IntPtr.Zero, item = IntPtr.Zero;
        try
        {
            Guid gridId = typeof(IGridProviderAbi).GUID;
            Assert.Equal(0, Marshal.QueryInterface(unknown, in gridId, out pattern));
            var getItem = Marshal.GetDelegateForFunctionPointer<GridGetItemAbi>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pattern), 3 * IntPtr.Size));
            Assert.Equal(0, getItem(pattern, 1, 1, out cell));
            Assert.NotEqual(IntPtr.Zero, cell);
            // Slot 3 must be IRawElementProviderSimple::get_ProviderOptions, not an arbitrary
            // IUnknown default-interface vtable which only appears to work after another QI.
            var options = Marshal.GetDelegateForFunctionPointer<GridGetIntAbi>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(cell), 3 * IntPtr.Size));
            Assert.Equal(0, options(cell, out int flags));
            Assert.Equal((int)ProviderOptions.ServerSideProvider, flags);
            Guid itemId = typeof(IGridItemProviderAbi).GUID;
            Assert.Equal(0, Marshal.QueryInterface(cell, in itemId, out item));
            var row = Marshal.GetDelegateForFunctionPointer<GridGetIntAbi>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(item), 3 * IntPtr.Size));
            var column = Marshal.GetDelegateForFunctionPointer<GridGetIntAbi>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(item), 4 * IntPtr.Size));
            Assert.Equal(0, row(item, out int rowIndex));
            Assert.Equal(1, rowIndex);
            Assert.Equal(0, column(item, out int columnIndex));
            Assert.Equal(1, columnIndex);
            grid.SortByColumn(0, SortOrder.Descending);
            Assert.Equal(0, row(item, out rowIndex));
            Assert.Equal(0, rowIndex);
            grid.Rows.RemoveAt(0);
            Assert.Equal(unchecked((int)0x80040201), row(item, out _));
            Assert.NotEqual(0, root.AbiGetGridItem(-1, 0, out IntPtr invalid));
            Assert.Equal(IntPtr.Zero, invalid);
        }
        finally
        {
            if (item != IntPtr.Zero) Marshal.Release(item);
            if (cell != IntPtr.Zero) Marshal.Release(cell);
            if (pattern != IntPtr.Zero) Marshal.Release(pattern);
            Marshal.Release(unknown);
        }
    }

    [Theory]
    [InlineData(27, 50005)] [InlineData(28, 50016)] [InlineData(29, 50028)]
    [InlineData(30, 50029)] [InlineData(31, 50034)] [InlineData(32, 50035)] [InlineData(33, 50001)]
    public void AddedControlTypesUseVerifiedNativeIds(int canonical, int native)
        => Assert.Equal(native, WindowsUiaControlTypeMapper.Map(canonical));

    private static DataGridView CreateSemanticGrid()
    {
        var grid = new DataGridView { Size = new System.Drawing.Size(400, 200), RowHeadersVisible = true, SelectionMode = DataGridViewSelectionMode.CellSelect };
        grid.Columns.Add("Name", 140); grid.Columns.Add("Value", 140);
        grid.Rows.Add("a", "first"); grid.Rows.Add("z", "last");
        return grid;
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GridGetItemAbi(IntPtr self, int row, int column, out IntPtr provider);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GridGetIntAbi(IntPtr self, out int value);
}
