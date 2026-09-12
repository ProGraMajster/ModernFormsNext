using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

internal partial class WindowsUiaProvider : IScrollProvider, IScrollProviderAbi
{
    double IScrollProvider.HorizontalScrollPercent => ReadScroll(info => info.Horizontal.Percent);
    double IScrollProvider.VerticalScrollPercent => ReadScroll(info => info.Vertical.Percent);
    double IScrollProvider.HorizontalViewSize => ReadScroll(info => info.Horizontal.ViewPercent);
    double IScrollProvider.VerticalViewSize => ReadScroll(info => info.Vertical.ViewPercent);
    bool IScrollProvider.HorizontallyScrollable => ReadScroll(info => info.Horizontal.IsScrollable);
    bool IScrollProvider.VerticallyScrollable => ReadScroll(info => info.Vertical.IsScrollable);

    private T ReadScroll<T>(Func<PlatformAccessibleScrollInfo, T> read)
        => Read(node => read(node.GetScrollInfo() ?? throw new InvalidOperationException("The object has no viewport.")));

    void IScrollProvider.Scroll(ScrollAmount horizontal, ScrollAmount vertical)
    {
        static int Map(ScrollAmount amount) => amount switch
        { ScrollAmount.NoAmount => 0, ScrollAmount.SmallDecrement => 1, ScrollAmount.SmallIncrement => 2,
            ScrollAmount.LargeDecrement => 3, ScrollAmount.LargeIncrement => 4,
            _ => throw new ArgumentException("Unknown scroll amount.") };
        PerformScroll(new(0, null, null, Map(horizontal), Map(vertical)));
    }

    void IScrollProvider.SetScrollPercent(double horizontal, double vertical)
    {
        static double? Percent(double value) => value == -1 ? null
            : double.IsFinite(value) && value >= 0 && value <= 100 ? value
            : throw new ArgumentException("Scroll percentages must be zero through 100, or exactly -1.");
        PerformScroll(new(1, Percent(horizontal), Percent(vertical)));
    }

    private void PerformScroll(PlatformAccessibleScrollRequest request)
        => Mutate(node =>
        {
            int state = node.State;
            if ((state & StateUnavailable) != 0) throw new WindowsUiaElementNotEnabledException();
            var info = node.GetScrollInfo() ?? throw new InvalidOperationException("The object has no viewport.");
            bool horizontal = request.Kind == 0 ? request.HorizontalAmount != 0 : request.Horizontal.HasValue;
            bool vertical = request.Kind == 0 ? request.VerticalAmount != 0 : request.Vertical.HasValue;
            if (horizontal && !info.Horizontal.IsScrollable || vertical && !info.Vertical.IsScrollable)
                throw new InvalidOperationException("The requested axis is not scrollable.");
            bool supported = (node.GetSupportedActions() & 256) != 0;
            Context.Validate(target, IsRoot);
            if (!supported || PlatformAccessibilityPrivacy.HasSensitiveAncestor(node)
                || !node.PerformUiaAction(256, request)) throw new InvalidOperationException("The viewport rejected the request.");
        });

    private static object? ScrollProperty(IPlatformAccessibleObject node, int propertyId)
    {
        if (propertyId == WindowsUiaScrollIds.Orientation)
            return node.GetOrientation() is int orientation ? orientation + 1 : 0;
        var info = node.GetScrollInfo();
        if (propertyId == WindowsUiaScrollIds.Available) return info.HasValue;
        if (info is not { } scroll) return null;
        return propertyId switch {
            WindowsUiaScrollIds.HorizontalPercent => scroll.Horizontal.Percent,
            WindowsUiaScrollIds.VerticalPercent => scroll.Vertical.Percent,
            WindowsUiaScrollIds.HorizontalViewSize => scroll.Horizontal.ViewPercent,
            WindowsUiaScrollIds.VerticalViewSize => scroll.Vertical.ViewPercent,
            WindowsUiaScrollIds.HorizontallyScrollable => scroll.Horizontal.IsScrollable,
            WindowsUiaScrollIds.VerticallyScrollable => scroll.Vertical.IsScrollable,
            _ => null };
    }

    public int AbiScroll(ScrollAmount horizontal, ScrollAmount vertical) => Try(() => ((IScrollProvider)this).Scroll(horizontal, vertical));
    public int AbiSetScrollPercent(double horizontal, double vertical) => Try(() => ((IScrollProvider)this).SetScrollPercent(horizontal, vertical));
    public int AbiGetHorizontalScrollPercent(out double value) => TryGet(() => ((IScrollProvider)this).HorizontalScrollPercent, out value);
    public int AbiGetVerticalScrollPercent(out double value) => TryGet(() => ((IScrollProvider)this).VerticalScrollPercent, out value);
    public int AbiGetHorizontalViewSize(out double value) => TryGet(() => ((IScrollProvider)this).HorizontalViewSize, out value);
    public int AbiGetVerticalViewSize(out double value) => TryGet(() => ((IScrollProvider)this).VerticalViewSize, out value);
    public int AbiGetHorizontallyScrollable(out int value) => TryGet(() => ((IScrollProvider)this).HorizontallyScrollable ? 1 : 0, out value);
    public int AbiGetVerticallyScrollable(out int value) => TryGet(() => ((IScrollProvider)this).VerticallyScrollable ? 1 : 0, out value);
}
