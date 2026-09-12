using Android.Views.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

internal sealed partial class AndroidAccessibilityNodeProvider
{
    private static void ApplyGridInfo(AccessibilityNodeInfo info, IPlatformAccessibleObject node, bool selected)
    {
        if (AndroidAccessibilityMapper.GridCell(node) is { } cell)
        {
#pragma warning disable CA1422
            using var item = AccessibilityNodeInfo.CollectionItemInfo.Obtain(
                cell.Row, cell.RowSpan, cell.Column, cell.ColumnSpan, false, selected);
#pragma warning restore CA1422
            info.SetCollectionItemInfo(item);
        }
        // Headers already have real labelled peers in the same tree. Marking them as headings does
        // not invent cell coordinates for the separate header row/column.
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            info.Heading = node.GetControlType() == 32;
    }
}
