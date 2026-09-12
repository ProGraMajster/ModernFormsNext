using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

// GUIDs, by-value VARIANT and vtable order follow Microsoft's UIAutomationCore.h.
// BSTR/SAFEARRAY outputs transfer ownership to UIA; input pointers remain borrowed.
[StructLayout(LayoutKind.Sequential)]
internal readonly struct UiaPoint(double x, double y)
{
    internal readonly double X = x;
    internal readonly double Y = y;
}

[GeneratedComInterface, Guid("3589C92C-63F3-4367-99BB-ADA653B77CF2")]
internal partial interface ITextProviderAbi
{
    [PreserveSig] int AbiGetTextSelection(out IntPtr ranges);
    [PreserveSig] int AbiGetVisibleRanges(out IntPtr ranges);
    [PreserveSig] int AbiRangeFromChild(IntPtr child, out IntPtr range);
    [PreserveSig] int AbiRangeFromPoint(UiaPoint point, out IntPtr range);
    [PreserveSig] int AbiGetDocumentRange(out IntPtr range);
    [PreserveSig] int AbiGetSupportedTextSelection(out int selection);
}

[GeneratedComInterface, Guid("0DC5E6ED-3E16-4BF1-8F9A-A979878BC195")]
internal partial interface ITextProvider2Abi : ITextProviderAbi
{
    [PreserveSig] int AbiRangeFromAnnotation(IntPtr annotation, out IntPtr range);
    [PreserveSig] int AbiGetCaretRange(out int active, out IntPtr range);
}

[GeneratedComInterface, Guid("5347AD7B-C355-46F8-AFF5-909033582F63")]
internal partial interface ITextRangeProviderAbi
{
    [PreserveSig] int AbiClone(out IntPtr range);
    [PreserveSig] int AbiCompare(IntPtr other, out int equal);
    [PreserveSig] int AbiCompareEndpoints(int endpoint, IntPtr other, int otherEndpoint, out int result);
    [PreserveSig] int AbiExpandToEnclosingUnit(int unit);
    [PreserveSig] int AbiFindAttribute(int attribute, WindowsUiaVariant value, int backward, out IntPtr range);
    [PreserveSig] int AbiFindText(IntPtr text, int backward, int ignoreCase, out IntPtr range);
    [PreserveSig] int AbiGetAttributeValue(int attribute, out WindowsUiaVariant value);
    [PreserveSig] int AbiGetBoundingRectangles(out IntPtr rectangles);
    [PreserveSig] int AbiGetEnclosingElement(out IntPtr element);
    [PreserveSig] int AbiGetText(int maximumLength, out IntPtr text);
    [PreserveSig] int AbiMove(int unit, int count, out int moved);
    [PreserveSig] int AbiMoveEndpointByUnit(int endpoint, int unit, int count, out int moved);
    [PreserveSig] int AbiMoveEndpointByRange(int endpoint, IntPtr other, int otherEndpoint);
    [PreserveSig] int AbiSelect();
    [PreserveSig] int AbiAddToSelection();
    [PreserveSig] int AbiRemoveFromSelection();
    [PreserveSig] int AbiScrollIntoView(int alignToTop);
    [PreserveSig] int AbiGetChildren(out IntPtr children);
}

internal static class WindowsUiaTextNative
{
    private static readonly StrategyBasedComWrappers Wrappers = new();
    private static readonly Guid RangeId = typeof(ITextRangeProviderAbi).GUID;
    private static readonly Guid SimpleId = typeof(IRawElementProviderSimpleAbi).GUID;
    internal static IntPtr RangePointer(WindowsTextRangeProvider? range)
        => range is null ? IntPtr.Zero : InterfacePointer(range, RangeId);
    internal static IntPtr SimplePointer(WindowsUiaProvider provider) => InterfacePointer(provider, SimpleId);
    private static IntPtr InterfacePointer(object value, Guid iid)
    {
        IntPtr unknown = Wrappers.GetOrCreateComInterfaceForObject(value, CreateComInterfaceFlags.None);
        try { Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in iid, out IntPtr result)); return result; }
        finally { Marshal.Release(unknown); }
    }
    internal static T Borrow<T>(IntPtr pointer) where T : class
        => pointer != IntPtr.Zero && ComWrappers.TryGetObject(pointer, out var value) && value is T typed
            ? typed : throw new ArgumentException("The object is not a compatible provider from this document.");
    internal static IntPtr RangeArray(WindowsTextRangeProvider[] ranges)
    {
        IntPtr array = SafeArrayCreateVector(VarEnum.VT_UNKNOWN, 0, checked((uint)ranges.Length));
        if (array == IntPtr.Zero) throw new OutOfMemoryException();
        try {
            for (int i = 0; i < ranges.Length; i++) {
                IntPtr pointer = RangePointer(ranges[i]);
                try { Marshal.ThrowExceptionForHR(SafeArrayPutElement(array, ref i, pointer)); }
                finally { Marshal.Release(pointer); }
            }
            return array;
        }
        catch { SafeArrayDestroy(array); throw; }
    }
    internal static IntPtr Reserved(PlatformTextAttributeSentinel sentinel)
    {
        int result = sentinel == PlatformTextAttributeSentinel.Mixed
            ? UiaGetReservedMixedAttributeValue(out IntPtr pointer) : UiaGetReservedNotSupportedValue(out pointer);
        Marshal.ThrowExceptionForHR(result);
        return pointer;
    }
    [DllImport("UIAutomationCore.dll")] private static extern int UiaGetReservedMixedAttributeValue(out IntPtr value);
    [DllImport("UIAutomationCore.dll")] private static extern int UiaGetReservedNotSupportedValue(out IntPtr value);
    [DllImport("oleaut32.dll")] private static extern IntPtr SafeArrayCreateVector(VarEnum type, int lowerBound, uint length);
    [DllImport("oleaut32.dll")] private static extern int SafeArrayPutElement(IntPtr array, ref int index, IntPtr value);
    [DllImport("oleaut32.dll")] private static extern int SafeArrayDestroy(IntPtr array);
}
