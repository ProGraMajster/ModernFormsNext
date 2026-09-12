using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

internal interface IGridProvider
{
    IRawElementProviderSimple? GetItem(int row, int column);
    int RowCount { get; }
    int ColumnCount { get; }
}
internal interface IGridItemProvider
{
    int Row { get; }
    int Column { get; }
    int RowSpan { get; }
    int ColumnSpan { get; }
    IRawElementProviderSimple ContainingGrid { get; }
}
internal interface ITableProvider
{
    IRawElementProviderSimple[] GetRowHeaders();
    IRawElementProviderSimple[] GetColumnHeaders();
    int RowOrColumnMajor { get; }
}
internal interface ITableItemProvider
{
    IRawElementProviderSimple[] GetRowHeaderItems();
    IRawElementProviderSimple[] GetColumnHeaderItems();
}

// GUIDs and method order follow the native Windows SDK UIAutomationCore.h contracts.
// The ABI returns owned interface pointers/SAFEARRAYs; managed test interfaces stay separate.
[GeneratedComInterface, Guid("B17D6187-0907-464B-A168-0EF17A1572B1")]
internal partial interface IGridProviderAbi
{
    [PreserveSig] int AbiGetGridItem(int row, int column, out IntPtr provider);
    [PreserveSig] int AbiGetRowCount(out int value);
    [PreserveSig] int AbiGetColumnCount(out int value);
}
[GeneratedComInterface, Guid("D02541F1-FB81-4D64-AE32-F520F8A6DBD1")]
internal partial interface IGridItemProviderAbi
{
    [PreserveSig] int AbiGetRow(out int value);
    [PreserveSig] int AbiGetColumn(out int value);
    [PreserveSig] int AbiGetRowSpan(out int value);
    [PreserveSig] int AbiGetColumnSpan(out int value);
    [PreserveSig] int AbiGetContainingGrid(out IntPtr value);
}
[GeneratedComInterface, Guid("9C860395-97B3-490A-B52A-858CC22AF166")]
internal partial interface ITableProviderAbi
{
    [PreserveSig] int AbiGetRowHeaders(out IntPtr value);
    [PreserveSig] int AbiGetColumnHeaders(out IntPtr value);
    [PreserveSig] int AbiGetRowOrColumnMajor(out int value);
}
[GeneratedComInterface, Guid("B9734FA6-771F-4D78-9C90-2517999349CD")]
internal partial interface ITableItemProviderAbi
{
    [PreserveSig] int AbiGetRowHeaderItems(out IntPtr value);
    [PreserveSig] int AbiGetColumnHeaderItems(out IntPtr value);
}

internal static class WindowsUiaGridIds
{
    internal const int Grid = 10006, GridItem = 10007, Table = 10012, TableItem = 10013;
    internal const int GridItemAvailable = 30029, GridAvailable = 30030, TableAvailable = 30038, TableItemAvailable = 30039;
    internal const int RowCount = 30062, ColumnCount = 30063, Row = 30064, Column = 30065,
        RowSpan = 30066, ColumnSpan = 30067, ContainingGrid = 30068;
    internal const int RowHeaders = 30081, ColumnHeaders = 30082, Traversal = 30083,
        RowHeaderItems = 30084, ColumnHeaderItems = 30085;
}
