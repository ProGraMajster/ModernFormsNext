using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

internal static partial class WindowsUiaEventMapper
{
    private static void RaiseScrollProperties(IWindowsUiaEventSink sink, WindowsUiaProvider provider, IPlatformAccessibleObject node)
    {
        var scroll = node.GetScrollInfo();
        RaisePropertyChanged(sink, provider, WindowsUiaScrollIds.Available, scroll.HasValue);
        if (scroll is not { } info) return;
        RaisePropertyChanged(sink, provider, WindowsUiaScrollIds.HorizontalPercent, info.Horizontal.Percent);
        RaisePropertyChanged(sink, provider, WindowsUiaScrollIds.VerticalPercent, info.Vertical.Percent);
        RaisePropertyChanged(sink, provider, WindowsUiaScrollIds.HorizontalViewSize, info.Horizontal.ViewPercent);
        RaisePropertyChanged(sink, provider, WindowsUiaScrollIds.VerticalViewSize, info.Vertical.ViewPercent);
        RaisePropertyChanged(sink, provider, WindowsUiaScrollIds.HorizontallyScrollable, info.Horizontal.IsScrollable);
        RaisePropertyChanged(sink, provider, WindowsUiaScrollIds.VerticallyScrollable, info.Vertical.IsScrollable);
    }
    private static void RaiseRangeProperties(IWindowsUiaEventSink sink, WindowsUiaProvider provider, IPlatformAccessibleObject node)
    {
        if (PlatformAccessibilityPrivacy.HasSensitiveAncestor(node) || node.GetRangeValue() is not { } range
            || PlatformAccessibilityPrivacy.HasSensitiveAncestor(node)) return;
        RaisePropertyChanged(sink, provider, 30047, range.Value);
        RaisePropertyChanged(sink, provider, 30048, range.IsReadOnly);
        RaisePropertyChanged(sink, provider, 30049, range.Minimum);
        RaisePropertyChanged(sink, provider, 30050, range.Maximum);
        RaisePropertyChanged(sink, provider, 30051, range.LargeChange);
        RaisePropertyChanged(sink, provider, 30052, range.SmallChange);
    }
}
