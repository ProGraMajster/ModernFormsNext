using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

internal enum ScrollAmount { LargeDecrement, SmallDecrement, NoAmount, LargeIncrement, SmallIncrement }
internal interface IScrollProvider
{
    void Scroll(ScrollAmount horizontal, ScrollAmount vertical);
    void SetScrollPercent(double horizontal, double vertical);
    double HorizontalScrollPercent { get; }
    double VerticalScrollPercent { get; }
    double HorizontalViewSize { get; }
    double VerticalViewSize { get; }
    bool HorizontallyScrollable { get; }
    bool VerticallyScrollable { get; }
}

// The method ordering is the native IScrollProvider vtable, not alphabetical property order.
[GeneratedComInterface]
[Guid("B38B8077-1FC3-42A5-8CAE-D40C2215055A")]
internal partial interface IScrollProviderAbi
{
    [PreserveSig] int AbiScroll(ScrollAmount horizontal, ScrollAmount vertical);
    [PreserveSig] int AbiSetScrollPercent(double horizontal, double vertical);
    [PreserveSig] int AbiGetHorizontalScrollPercent(out double value);
    [PreserveSig] int AbiGetVerticalScrollPercent(out double value);
    [PreserveSig] int AbiGetHorizontalViewSize(out double value);
    [PreserveSig] int AbiGetVerticalViewSize(out double value);
    [PreserveSig] int AbiGetHorizontallyScrollable(out int value);
    [PreserveSig] int AbiGetVerticallyScrollable(out int value);
}

internal static class WindowsUiaScrollIds
{
    internal const int Pattern = 10004, Available = 30034, HorizontalPercent = 30053, HorizontalViewSize = 30054,
        VerticalPercent = 30055, VerticalViewSize = 30056, HorizontallyScrollable = 30057, VerticallyScrollable = 30058;
    internal const int Orientation = 30023;
}
