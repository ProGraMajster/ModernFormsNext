using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

internal static partial class AndroidAccessibilityMapper
{
    internal const int ActionSetTextSelection = 131072, ActionNextText = 256, ActionPreviousText = 512;
    internal readonly record struct TextSelectionRequest(int? Start, int? End);
    internal readonly record struct TextMovementRequest(int Granularity, bool Extend);

    private static void AddTextActions(List<int> actions, IPlatformAccessibleObject node)
    {
        if (node.GetTextProvider() is not { SupportsSelection: true }) return;
        actions.Add(ActionSetTextSelection);
        actions.Add(ActionNextText);
        actions.Add(ActionPreviousText);
    }

    private static bool PerformTextAction(IPlatformAccessibleObject node, int action, object? parameter, Func<bool>? isCurrent)
    {
        if (node.GetTextProvider() is not { SupportsSelection: true } provider) return false;
        bool Current() => isCurrent?.Invoke() != false && !node.GetIsSensitive()
            && ReferenceEquals(node.GetTextProvider(), provider);
        var caret = provider.GetCaretRange(out _);
        int length = provider.DocumentRange.End;
        if (action == ActionSetTextSelection) {
            if (parameter is not TextSelectionRequest request) return false;
            // Android omits both offsets to clear selection. Supplying only one is malformed.
            if (request.Start.HasValue != request.End.HasValue) return false;
            int start = request.Start ?? caret.Start, end = request.End ?? caret.End;
            if (start < 0 || end < 0 || start > length || end > length) return false;
            return Current() && provider.SetSelection(start, end);
        }
        if (parameter is not TextMovementRequest movement) return false;
        int unit = movement.Granularity switch { 1 => 0, 2 => 2, 4 => 3, 8 => 4, 16 => 5, _ => -1 };
        if (unit < 0) return false;
        int oldCaret = caret.Start;
        var selection = provider.GetSelection().FirstOrDefault();
        int anchor = selection is null ? oldCaret : selection.Start == oldCaret ? selection.End : selection.Start;
        int moved = caret.Move(unit, action == ActionNextText ? 1 : -1);
        if (moved == 0) return false;
        return Current() && provider.SetSelection(movement.Extend ? anchor : caret.Start, caret.Start);
    }
}
