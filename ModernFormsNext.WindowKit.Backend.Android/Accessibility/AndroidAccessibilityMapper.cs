using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

/// <summary>
/// Converts canonical semantics to transient Android node properties. No semantic hierarchy or
/// native wrappers are stored here; callers read on the UI thread at the time of a query.
/// </summary>
internal static partial class AndroidAccessibilityMapper
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
        27 or 30 or 32 => "android.widget.TextView",
        28 => "android.widget.NumberPicker",
        29 => "android.widget.GridView",
        31 => "android.view.ViewGroup",
        33 => "android.widget.CalendarView",
        _ => "android.view.View"
    };

    internal static AndroidAccessibilityProperties Read(IPlatformAccessibleObject node)
    {
        int type = node.GetControlType();
        int state = node.State;
        bool inherited = HasSensitiveParent(node), sensitive = false;
        void RefreshPrivacy()
        {
            // Once protected during this read, the captured payload stays redacted even if a
            // later getter removes the marker. Own password labels keep the Phase 3 contract;
            // descendant names can be generated from private data and are not safe labels.
            inherited |= HasSensitiveParent(node);
            sensitive |= inherited || PlatformAccessibilityPrivacy.HasSensitiveAncestor(node);
        }
        string? Metadata(Func<string?> read)
        {
            RefreshPrivacy();
            if (inherited) return null;
            string? result = read();
            RefreshPrivacy();
            return inherited ? null : result;
        }
        RefreshPrivacy();
        bool edit = type == 10 || !sensitive && SupportsStringValue(node, type);
        RefreshPrivacy();
        string? value = sensitive ? null : node.Value;
        RefreshPrivacy();
        string? label = Metadata(() => node.Name);
        string? help = Metadata(() => node.Help) ?? Metadata(() => node.Description);
        RefreshPrivacy();
        var range = sensitive ? null : ReadRange(node);
        bool important = node.GetAccessibilityView() != 1;
        RefreshPrivacy();
        if (sensitive) { value = null; range = null; }
        if (inherited) { label = null; help = null; }
        string? stateDescription = (state & Mixed) != 0 ? "Mixed"
            : (state & Expanded) != 0 ? "Expanded"
            : (state & Collapsed) != 0 ? "Collapsed" : null;
        // Checkable widgets already have localized Android state descriptions. In particular,
        // Switch.Value is numeric; publishing "0"/"1" overrides TalkBack's native Off/On speech.
        // Keep an explicit Mixed description for the canonical third state above.
        if (stateDescription is null && !edit && type is not (5 or 7 or 8 or 9 or 13 or 15 or 17 or 19)
            && !string.IsNullOrEmpty(value) && value != label && range is null)
            stateDescription = value;
        if (sensitive) stateDescription = null;
        return new(ClassName(type), label, edit ? value : type == 5 ? label : null,
            help, stateDescription, sensitive,
            (state & Unavailable) == 0, (state & Focusable) != 0,
            (state & Focused) != 0, (state & Selected) != 0,
            type is 7 or 8 or 9 || (state & (Checked | Mixed)) != 0,
            (state & Checked) != 0, edit && (state & ReadOnly) == 0,
            important, range);
    }

    private static bool HasSensitiveParent(IPlatformAccessibleObject node)
        => node.Parent is { } parent && PlatformAccessibilityPrivacy.HasSensitiveAncestor(parent);

    // AutomationId is read outside the property projection by Android's native Extras path.
    // Apply the same inherited-metadata rule before and after that custom getter.
    internal static string? ReadAutomationId(IPlatformAccessibleObject node)
    {
        if (HasSensitiveParent(node)) return null;
        string? value = node.GetAutomationId();
        return HasSensitiveParent(node) ? null : value;
    }

    internal static bool CanExposeProperties(IPlatformAccessibleObject node, AndroidAccessibilityProperties properties, string? automationId)
    {
        bool sensitive = PlatformAccessibilityPrivacy.HasSensitiveAncestor(node);
        bool inherited = HasSensitiveParent(node);
        if (sensitive && !properties.Password) return false;
        return !inherited || properties.Label is null && properties.Text is null && properties.Help is null
            && properties.StateDescription is null && properties.Range is null && automationId is null;
    }

    internal static PlatformAccessibleRangeValue? ValidRange(PlatformAccessibleRangeValue? range)
        => range is { } r && double.IsFinite(r.Minimum) && double.IsFinite(r.Maximum)
            && double.IsFinite(r.Value) && r.Minimum >= -float.MaxValue && r.Maximum <= float.MaxValue
            && r.Minimum <= r.Maximum && r.Value >= r.Minimum && r.Value <= r.Maximum ? r : null;

    private static PlatformAccessibleRangeValue? ReadRange(IPlatformAccessibleObject node)
    {
        // Actions are part of native node construction too. Never invoke a protected range
        // getter simply to decide which adjustment actions the node should advertise.
        if (PlatformAccessibilityPrivacy.HasSensitiveAncestor(node)) return null;
        var range = ValidRange(node.GetRangeValue());
        return PlatformAccessibilityPrivacy.HasSensitiveAncestor(node) ? null : range;
    }

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
            if (SupportsStringValue(node, type)) result.Add(ActionSetText);
            else if (ReadRange(node) is { IsReadOnly: false }) result.Add(ActionSetProgress);
        }
        if ((actions & ScrollIntoView) != 0) result.Add(ActionShowOnScreen);
        AddTextActions(result, node);
        // Viewport actions and adjustable range actions share native IDs, but retain distinct
        // canonical semantics. A viewport explicitly advertising Scroll owns those IDs.
        if ((actions & 256) != 0 && node.GetScrollInfo() is { } viewport)
            AddViewportActions(result, viewport);
        else if (ReadRange(node) is { IsReadOnly: false } range)
        {
            if ((actions & Increment) != 0 && range.Value < range.Maximum) result.Add(ActionScrollForward);
            if ((actions & Decrement) != 0 && range.Value > range.Minimum) result.Add(ActionScrollBackward);
        }
        // A later custom getter can protect the node after earlier actions were collected.
        // Keep ordinary focus/click and write-only password input; withdraw payload-derived
        // range, viewport and text-selection capabilities from this transient result.
        if (PlatformAccessibilityPrivacy.HasSensitiveAncestor(node))
            result.RemoveAll(action => action == ActionSetProgress || IsViewportAction(action)
                || action is ActionSetTextSelection or ActionNextText or ActionPreviousText
                || action == ActionSetText && type != 10);
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

    // Text editors, actual editable grid cells and the date picker's formatted value share
    // the canonical SetValue(string) action. Numeric spinners retain SetProgress instead.
    private static bool SupportsStringValue(IPlatformAccessibleObject node, int type)
    {
        if (type == 10) return true; // Password edits remain write-only without inspecting value/range.
        if (PlatformAccessibilityPrivacy.HasSensitiveAncestor(node)) return false;
        bool supported = node.GetGridCell() is not null
            || type is 13 or 28 && ReadRange(node) is null && (node.GetSupportedActions() & SetValue) != 0;
        return !PlatformAccessibilityPrivacy.HasSensitiveAncestor(node) && supported;
    }

    private static bool CanClearSelection(IPlatformAccessibleObject node)
        => SupportsIndependentSelection(node) && (node.State & Selected) != 0;

    private static bool SupportsIndependentSelection(IPlatformAccessibleObject node)
        => node is IPlatformAccessibilitySelection { CanClearSelection: true }
            && (node.GetSupportedActions() & Select) != 0
            && node.Parent is { } parent && (parent.State & MultiSelectable) != 0;

    internal static bool PerformAction(IPlatformAccessibleObject node, int action, object? parameter, Func<bool>? isCurrent = null)
    {
        if (!Actions(node).Contains(action)) return false;
        if (action is ActionSetTextSelection or ActionNextText or ActionPreviousText)
            return PerformTextAction(node, action, parameter, isCurrent);
        if (IsViewportAction(action) && (node.GetSupportedActions() & 256) != 0 && node.GetScrollInfo() is { } scroll)
            return PerformViewportAction(node, action, parameter, scroll, isCurrent);
        if (action == ActionSetText)
            return parameter is string text && node.PerformUiaAction(SetValue, text);
        if (action == ActionSetProgress)
        {
            if (parameter is not double value || !double.IsFinite(value)
                || ReadRange(node) is not { IsReadOnly: false } range
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
        if (node.GetGridInfo() is { } grid)
            return new(grid.Rows, grid.Columns, (node.State & MultiSelectable) != 0 ? 2 : 1);
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
