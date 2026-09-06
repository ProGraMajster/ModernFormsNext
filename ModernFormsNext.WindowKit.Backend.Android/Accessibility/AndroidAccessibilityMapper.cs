using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

/// <summary>
/// Converts canonical semantics to transient Android node properties. No semantic hierarchy or
/// native wrappers are stored here; callers read on the UI thread at the time of a query.
/// </summary>
internal static class AndroidAccessibilityMapper
{
    internal const int Unavailable = 0x1, Selected = 0x2, Focused = 0x4, Checked = 0x10,
        Mixed = 0x20, ReadOnly = 0x40, Expanded = 0x200, Collapsed = 0x400,
        Invisible = 0x8000, Offscreen = 0x10000, Focusable = 0x100000,
        MultiSelectable = 0x1000000, Protected = 0x20000000;
    internal const int Invoke = 1, Toggle = 2, Select = 4, Expand = 8, Collapse = 16,
        SetValue = 32, Increment = 64, Decrement = 128, ScrollIntoView = 512, Focus = 1024;

    // Android action IDs belong exclusively to this backend. Large resource IDs are not flags.
    internal const int ActionFocus = 1, ActionSelect = 4, ActionClearSelection = 8,
        ActionClick = 16, ActionAccessibilityFocus = 64, ActionClearAccessibilityFocus = 128,
        ActionScrollForward = 4096, ActionScrollBackward = 8192,
        ActionExpand = 262144, ActionCollapse = 524288, ActionSetText = 2097152,
        ActionShowOnScreen = 16908342, ActionSetProgress = 16908349;

    internal static string ClassName(int type) => type switch
    {
        2 or 23 => "android.app.Dialog",
        3 or 4 or 14 or 18 or 25 => "android.view.ViewGroup",
        5 or 13 or 15 or 17 or 19 => "android.widget.TextView",
        6 => "android.widget.Button",
        7 => "android.widget.CheckBox",
        8 => "android.widget.RadioButton",
        9 => "android.widget.Switch",
        10 => "android.widget.EditText",
        11 => "android.widget.Spinner",
        12 => "android.widget.ListView",
        16 => "android.widget.TabWidget",
        20 => "android.widget.SeekBar",
        21 => "android.widget.ProgressBar",
        24 => "android.widget.ImageView",
        _ => "android.view.View"
    };

    internal static AndroidAccessibilityProperties Read(IPlatformAccessibleObject node)
    {
        int type = node.GetControlType();
        int state = node.State;
        bool sensitive = node.GetIsSensitive() || (state & Protected) != 0;
        bool edit = type == 10;
        // Never read Value at all for sensitive peers, including custom implementations whose
        // getter might return plaintext. Explicit Name/Help/Description are metadata, not Value.
        string? value = sensitive ? null : node.Value;
        string? label = node.Name;
        string? stateDescription = (state & Mixed) != 0 ? "Mixed"
            : (state & Expanded) != 0 ? "Expanded"
            : (state & Collapsed) != 0 ? "Collapsed" : null;
        // Checkable widgets already have localized Android state descriptions. In particular,
        // Switch.Value is numeric; publishing "0"/"1" overrides TalkBack's native Off/On speech.
        // Keep an explicit Mixed description for the canonical third state above.
        if (stateDescription is null && !edit && type is not (5 or 7 or 8 or 9 or 13 or 15 or 17 or 19)
            && !string.IsNullOrEmpty(value) && value != label && node.GetRangeValue() is null)
            stateDescription = value;
        return new(ClassName(type), label, edit ? value : type == 5 ? label : null,
            node.Help ?? node.Description, stateDescription, sensitive,
            (state & Unavailable) == 0, (state & Focusable) != 0,
            (state & Focused) != 0, (state & Selected) != 0,
            type is 7 or 8 or 9 || (state & (Checked | Mixed)) != 0,
            (state & Checked) != 0, edit && (state & ReadOnly) == 0,
            node.GetAccessibilityView() != 1,
            sensitive ? null : ValidRange(node.GetRangeValue()));
    }

    internal static PlatformAccessibleRangeValue? ValidRange(PlatformAccessibleRangeValue? range)
        => range is { } r && double.IsFinite(r.Minimum) && double.IsFinite(r.Maximum)
            && double.IsFinite(r.Value) && r.Minimum >= -float.MaxValue && r.Maximum <= float.MaxValue
            && r.Minimum <= r.Maximum && r.Value >= r.Minimum && r.Value <= r.Maximum ? r : null;

    internal static List<int> Actions(IPlatformAccessibleObject node)
    {
        List<int> result = [];
        int state = node.State;
        if ((state & (Unavailable | Invisible)) != 0 || node.GetAccessibilityView() == 4)
            return result;
        int actions = node.GetSupportedActions();
        int type = node.GetControlType();
        if ((actions & Focus) != 0 && (state & Focused) == 0) result.Add(ActionFocus);
        if (ClickAction(node) != 0) result.Add(ActionClick);
        if ((actions & Select) != 0) result.Add(ActionSelect);
        if (CanClearSelection(node)) result.Add(ActionClearSelection);
        if ((actions & Expand) != 0 && (state & Expanded) == 0) result.Add(ActionExpand);
        if ((actions & Collapse) != 0 && (state & Collapsed) == 0) result.Add(ActionCollapse);
        if ((actions & SetValue) != 0 && (state & ReadOnly) == 0)
        {
            if (type == 10) result.Add(ActionSetText);
            else if (ValidRange(node.GetRangeValue()) is { IsReadOnly: false }
                && !node.GetIsSensitive()) result.Add(ActionSetProgress);
        }
        if ((actions & ScrollIntoView) != 0) result.Add(ActionShowOnScreen);
        // Android uses these actions for adjustable ranges too. No generic viewport scroll
        // parameter is invented: the canonical Increment/Decrement operations are sufficient.
        if (ValidRange(node.GetRangeValue()) is { IsReadOnly: false } range)
        {
            if ((actions & Increment) != 0 && range.Value < range.Maximum) result.Add(ActionScrollForward);
            if ((actions & Decrement) != 0 && range.Value > range.Minimum) result.Add(ActionScrollBackward);
        }
        return result;
    }

    private static int ClickAction(IPlatformAccessibleObject node)
    {
        int actions = node.GetSupportedActions();
        if ((actions & Invoke) != 0) return Invoke;
        if ((actions & Toggle) != 0) return Toggle;
        if ((actions & Select) != 0) return Select;
        return 0;
    }

    private static bool CanClearSelection(IPlatformAccessibleObject node)
        => SupportsIndependentSelection(node) && (node.State & Selected) != 0;

    private static bool SupportsIndependentSelection(IPlatformAccessibleObject node)
        => node is IPlatformAccessibilitySelection { CanClearSelection: true }
            && (node.GetSupportedActions() & Select) != 0
            && node.Parent is { } parent && (parent.State & MultiSelectable) != 0;

    internal static bool PerformAction(IPlatformAccessibleObject node, int action, object? parameter)
    {
        if (!Actions(node).Contains(action)) return false;
        if (action == ActionSetText)
            return parameter is string text && node.PerformUiaAction(SetValue, text);
        if (action == ActionSetProgress)
        {
            if (parameter is not double value || !double.IsFinite(value)
                || ValidRange(node.GetRangeValue()) is not { IsReadOnly: false } range
                || value < range.Minimum || value > range.Maximum) return false;
            return node.PerformUiaAction(SetValue, value);
        }
        if (parameter is not null) return false;
        if (action == ActionClearSelection)
        {
            // This is the existing canonical multi-selection path, also used by Windows/MSAA.
            // Single-select radio/tabs and unsupported clear-input-focus are never advertised.
            node.Select(16);
            return (node.State & Selected) == 0;
        }
        int canonical = action switch
        {
            ActionClick => ClickAction(node), ActionFocus => Focus, ActionSelect => Select,
            ActionExpand => Expand, ActionCollapse => Collapse, ActionShowOnScreen => ScrollIntoView,
            ActionScrollForward => Increment, ActionScrollBackward => Decrement, _ => 0
        };
        if (canonical == Select && SupportsIndependentSelection(node))
        {
            // The existing ListBox selection flags preserve other selected occurrences.
            // ACTION_SELECT is idempotent; TalkBack's click toggles just this item, matching
            // touch selection without requiring keyboard modifiers in a multi-select list.
            bool selected = action != ActionClick || (node.State & Selected) == 0;
            node.Select(selected ? 8 : 16);
            return ((node.State & Selected) != 0) == selected;
        }
        return canonical != 0 && node.PerformUiaAction(canonical);
    }

    internal static AndroidCollection? Collection(IPlatformAccessibleObject node)
    {
        // Only a flat sequence of semantic ListItems has known row/column information.
        // Trees, menus and tabs retain their hierarchy without fabricated table coordinates.
        if (node.GetControlType() != 12) return null;
        int count = node.GetChildCount();
        for (int i = 0; i < count; i++)
            if (node.GetChild(i)?.GetControlType() != 13) return null;
        return new(count, 1, (node.State & MultiSelectable) != 0 ? 2
            : (node.State & 0x200000) != 0 ? 1 : 0);
    }
}

/// <summary>One query's Android property projection; never used as an authoritative node model.</summary>
internal readonly record struct AndroidAccessibilityProperties(string ClassName, string? Label,
    string? Text, string? Help, string? StateDescription, bool Password, bool Enabled,
    bool Focusable, bool Focused, bool Selected, bool Checkable, bool Checked, bool Editable,
    bool Important, PlatformAccessibleRangeValue? Range);

internal readonly record struct AndroidCollection(int Rows, int Columns, int SelectionMode);
