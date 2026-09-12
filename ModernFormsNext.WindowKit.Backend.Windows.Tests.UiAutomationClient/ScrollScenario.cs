using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows.Automation;

internal static class ScrollScenario
{
    internal static int Run(string handle)
    {
        if (!long.TryParse(handle, out long value) || value == 0) return 2;
        var root = AutomationElement.FromHandle(new IntPtr(value));
        AutomationElement Find(string id) => root.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, id)) ?? throw new InvalidOperationException("Missing " + id);
        var viewport = Find("uia.scroll.viewport");
        var target = Find("uia.scroll.target");
        var focus = Find("uia.scroll.focus");
        focus.SetFocus();
        bool focusBefore = focus.Current.HasKeyboardFocus;
        long foregroundBefore = GetForegroundWindow().ToInt64();
        var scroll = (ScrollPattern)viewport.GetCurrentPattern(ScrollPattern.Pattern);
        bool targetInitiallyOffscreen = target.Current.IsOffscreen;
        bool horizontal = scroll.Current.HorizontallyScrollable, vertical = scroll.Current.VerticallyScrollable;
        double horizontalView = scroll.Current.HorizontalViewSize, verticalView = scroll.Current.VerticalViewSize;
        scroll.SetScrollPercent(50, 100);
        double horizontalAfter = scroll.Current.HorizontalScrollPercent, verticalAfter = scroll.Current.VerticalScrollPercent;
        bool focusAfterPercent = focus.Current.HasKeyboardFocus;
        scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallDecrement);
        bool focusAfterAmount = focus.Current.HasKeyboardFocus;
        bool smallMovedBack = scroll.Current.VerticalScrollPercent < verticalAfter;
        ((ScrollItemPattern)target.GetCurrentPattern(ScrollItemPattern.Pattern)).ScrollIntoView();
        bool targetRevealed = !target.Current.IsOffscreen;
        bool focusPreserved = focus.Current.HasKeyboardFocus;
        long foregroundAfter = GetForegroundWindow().ToInt64();
        using var observation = JsonDocument.Parse(Find("uia.scroll.focus-observation").Current.Name);
        int canonicalScrollActions = observation.RootElement.GetProperty("Count").GetInt32();
        bool canonicalFocusPreserved = observation.RootElement.GetProperty("Preserved").GetBoolean();
        bool invalidRejected = false;
        try { scroll.SetScrollPercent(101, ScrollPattern.NoScroll); }
        catch (ArgumentException) { invalidRejected = true; }
        ((InvokePattern)Find("uia.scroll.disable").GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        bool disabledGeometryRetained = scroll.Current.HorizontallyScrollable && scroll.Current.VerticallyScrollable;
        bool disabledRejected = false;
        try { scroll.SetScrollPercent(0, ScrollPattern.NoScroll); }
        catch (ElementNotEnabledException) { disabledRejected = true; }
        Console.WriteLine(JsonSerializer.Serialize(new { TargetInitiallyOffscreen = targetInitiallyOffscreen,
            Horizontal = horizontal, Vertical = vertical, HorizontalView = horizontalView, VerticalView = verticalView,
            HorizontalAfter = horizontalAfter, VerticalAfter = verticalAfter, SmallMovedBack = smallMovedBack,
            TargetRevealed = targetRevealed, FocusBefore = focusBefore, FocusAfterPercent = focusAfterPercent,
            FocusAfterAmount = focusAfterAmount, ClientRetainedOriginalFocus = focusPreserved,
            CanonicalScrollActions = canonicalScrollActions, CanonicalFocusPreserved = canonicalFocusPreserved,
            ForegroundBefore = foregroundBefore, ForegroundAfter = foregroundAfter, InvalidRejected = invalidRejected,
            DisabledGeometryRetained = disabledGeometryRetained, DisabledRejected = disabledRejected }));
        return 0;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
}
