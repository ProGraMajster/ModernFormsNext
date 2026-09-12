using Android.Views.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using static ModernFormsNext.WindowKit.Backend.Android.Accessibility.AndroidAccessibilityMapper;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

internal sealed partial class AndroidAccessibilityNodeProvider
{
    private const string ScrollAmountKey = "android.view.accessibility.action.ARGUMENT_SCROLL_AMOUNT_FLOAT";
    private static void ApplyViewportInfo(AccessibilityNodeInfo info, IPlatformAccessibleObject node, List<int> actions)
    {
        var scroll = node.GetScrollInfo();
        info.Scrollable = scroll is { } viewport ? viewport.Horizontal.IsScrollable || viewport.Vertical.IsScrollable
            : actions.Contains(ActionScrollForward) || actions.Contains(ActionScrollBackward);
        if (OperatingSystem.IsAndroidVersionAtLeast(35))
            info.GranularScrollingSupported = scroll.HasValue && (node.GetSupportedActions() & 256) != 0;
    }
    private void ApplyViewportEvent(AccessibilityEvent nativeEvent, IPlatformAccessibleObject node)
    {
        if (node.GetScrollInfo() is not { } info) return;
        int Pixels(double value) => (int)Math.Clamp(Math.Round(value * host.Density), 0, int.MaxValue);
        nativeEvent.Scrollable = info.Horizontal.IsScrollable || info.Vertical.IsScrollable;
        nativeEvent.ScrollX = Pixels(info.Horizontal.Offset - info.Horizontal.Minimum);
        nativeEvent.ScrollY = Pixels(info.Vertical.Offset - info.Vertical.Minimum);
        nativeEvent.MaxScrollX = Pixels(info.Horizontal.Maximum - info.Horizontal.Minimum);
        nativeEvent.MaxScrollY = Pixels(info.Vertical.Maximum - info.Vertical.Minimum);
    }
}
