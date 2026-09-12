using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

internal static partial class AndroidAccessibilityMapper
{
    internal const int ActionScrollUp = 16908344, ActionScrollLeft = 16908345,
        ActionScrollDown = 16908346, ActionScrollRight = 16908347;
    internal static bool IsViewportAction(int action) => action is ActionScrollForward or ActionScrollBackward
        or ActionScrollUp or ActionScrollDown or ActionScrollLeft or ActionScrollRight;
    private static void AddViewportActions(List<int> result, PlatformAccessibleScrollInfo info)
    {
        static bool Back(PlatformAccessibleScrollAxis a) => a.IsScrollable && a.Offset > a.Minimum;
        static bool Forward(PlatformAccessibleScrollAxis a) => a.IsScrollable && a.Offset < a.Maximum;
        if (Back(info.Horizontal)) result.Add(ActionScrollLeft);
        if (Forward(info.Horizontal)) result.Add(ActionScrollRight);
        if (Back(info.Vertical)) result.Add(ActionScrollUp);
        if (Forward(info.Vertical)) result.Add(ActionScrollDown);
        var primary = info.Vertical.IsScrollable ? info.Vertical : info.Horizontal;
        if (Back(primary)) result.Add(ActionScrollBackward);
        if (Forward(primary)) result.Add(ActionScrollForward);
    }
    private static bool PerformViewportAction(IPlatformAccessibleObject node, int action, object? parameter,
        PlatformAccessibleScrollInfo scroll, Func<bool>? isCurrent)
    {
        double pages = parameter is null ? 1 : parameter is double amount ? amount : double.NaN;
        if (double.IsNaN(pages) || pages < 0) return false;
        bool vertical = action is ActionScrollUp or ActionScrollDown
            || action is ActionScrollForward or ActionScrollBackward && scroll.Vertical.IsScrollable;
        bool backward = action is ActionScrollUp or ActionScrollLeft or ActionScrollBackward;
        var axis = vertical ? scroll.Vertical : scroll.Horizontal;
        if (!axis.IsScrollable) return false;
        double signed = backward ? -pages : pages;
        PlatformAccessibleScrollRequest request = double.IsPositiveInfinity(pages)
            ? new(1, vertical ? null : backward ? 0 : 100, vertical ? backward ? 0 : 100 : null)
            : new(2, vertical ? 0 : signed, vertical ? signed : 0);
        return !PlatformAccessibilityPrivacy.HasSensitiveAncestor(node)
            && (isCurrent?.Invoke() ?? true) && node.PerformUiaAction(256, request);
    }
}
