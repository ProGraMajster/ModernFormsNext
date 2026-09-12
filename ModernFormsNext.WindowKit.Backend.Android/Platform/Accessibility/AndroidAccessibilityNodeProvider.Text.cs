using Android.OS;
using Android.Views.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using static ModernFormsNext.WindowKit.Backend.Android.Accessibility.AndroidAccessibilityMapper;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

internal sealed partial class AndroidAccessibilityNodeProvider
{
    private static void ApplyTextInfo(AccessibilityNodeInfo info, IPlatformAccessibleObject node)
    {
        if (node.GetTextProvider() is not { } provider) return;
        if (provider.GetSelection().FirstOrDefault() is { } selection) info.SetTextSelection(selection.Start, selection.End);
        info.MovementGranularities = (MovementGranularity)(1 | 2 | 4 | 8 | 16);
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) info.TextSelectable = provider.SupportsSelection;
    }

    private static object ReadTextActionArguments(int action, Bundle? arguments)
    {
        if (action == ActionSetTextSelection) {
            const string start = "ACTION_ARGUMENT_SELECTION_START_INT", end = "ACTION_ARGUMENT_SELECTION_END_INT";
            return new TextSelectionRequest(arguments?.ContainsKey(start) == true ? arguments.GetInt(start) : null,
                arguments?.ContainsKey(end) == true ? arguments.GetInt(end) : null);
        }
        return new TextMovementRequest(arguments?.GetInt("ACTION_ARGUMENT_MOVEMENT_GRANULARITY_INT", 0) ?? 0,
            arguments?.GetBoolean("ACTION_ARGUMENT_EXTEND_SELECTION_BOOLEAN", false) ?? false);
    }

    private static void ApplyTextEvent(AccessibilityEvent nativeEvent, IPlatformAccessibleObject node)
    {
        if (node.GetIsSensitive() || node.GetTextProvider() is not { } provider) return;
        if (provider.GetSelection().FirstOrDefault() is not { } selection) return;
        int start = selection.Start, end = selection.End, count = provider.DocumentRange.End;
        if (!ReferenceEquals(provider, node.GetTextProvider())) return;
        nativeEvent.FromIndex = start;
        nativeEvent.ToIndex = end;
        nativeEvent.ItemCount = count;
    }
}
