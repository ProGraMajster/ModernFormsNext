using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

/// <summary>
/// Translates canonical accessibility notifications to Windows UI Automation events.
/// </summary>
internal static class WindowsUiaEventMapper
{
    private const int EventShow = 0x8002;
    private const int EventHide = 0x8003;
    private const int EventReorder = 0x8004;
    private const int EventFocus = 0x8005;
    private const int EventSelection = 0x8006;
    private const int EventSelectionAdd = 0x8007;
    private const int EventSelectionRemove = 0x8008;
    private const int EventSelectionWithin = 0x8009;
    private const int EventStateChange = 0x800A;
    private const int EventLocationChange = 0x800B;
    private const int EventNameChange = 0x800C;
    private const int EventDescriptionChange = 0x800D;
    private const int EventValueChange = 0x800E;
    private const int EventParentChange = 0x800F;

    public static void Raise(
        WindowsUiaProvider provider,
        IPlatformAccessibleObject source,
        int eventId,
        WindowsUiaProviderContext context,
        IWindowsUiaEventSink eventSink)
    {
        switch (eventId)
        {
            case EventFocus:
                RaiseEvent(eventSink, provider, WindowsUiaIds.AutomationFocusChangedEvent);
                break;
            case EventNameChange:
                RaisePropertyChanged(eventSink, provider, WindowsUiaIds.NameProperty, source.Name ?? string.Empty);
                break;
            case EventDescriptionChange:
                RaisePropertyChanged(
                    eventSink,
                    provider,
                    WindowsUiaIds.HelpTextProperty,
                    source.Help ?? source.Description ?? string.Empty);
                break;
            case EventValueChange when !source.GetIsSensitive():
                if (source.GetRangeValue() is { } range)
                    RaisePropertyChanged(eventSink, provider, WindowsUiaIds.RangeValueProperty, range.Value);
                else
                    RaisePropertyChanged(eventSink, provider, WindowsUiaIds.ValueProperty, source.Value ?? string.Empty);
                break;
            case EventStateChange:
                RaiseStateProperties(eventSink, provider, source);
                break;
            case EventLocationChange:
                RaisePropertyChanged(
                    eventSink,
                    provider,
                    WindowsUiaIds.BoundingRectangleProperty,
                    WindowsUiaCoordinateConverter.ToBoundingRectangle(source.Bounds));
                break;
            case EventSelection:
                RaiseEvent(eventSink, provider, WindowsUiaIds.ElementSelectedEvent);
                break;
            case EventSelectionAdd:
                RaiseEvent(eventSink, provider, WindowsUiaIds.ElementAddedToSelectionEvent);
                break;
            case EventSelectionRemove:
                RaiseEvent(eventSink, provider, WindowsUiaIds.ElementRemovedFromSelectionEvent);
                break;
            case EventSelectionWithin:
                RaiseEvent(eventSink, provider, WindowsUiaIds.SelectionInvalidatedEvent);
                break;
            case EventReorder:
                WindowsUiaStructureChange change = provider.ConsumeStructureChange();
                RaiseStructureChanged(
                    eventSink,
                    change.Provider,
                    change.ChangeType,
                    change.RuntimeId);
                break;
            case EventParentChange:
                RaiseStructureChanged(
                    eventSink,
                    GetStructureParent(provider, source, context),
                    StructureChangeType.ChildrenReordered,
                    runtimeId: null);
                break;
            case EventShow:
                RaisePropertyChanged(eventSink, provider, WindowsUiaIds.IsOffscreenProperty, false);
                RaiseStructureChanged(
                    eventSink,
                    GetStructureParent(provider, source, context),
                    StructureChangeType.ChildAdded,
                    runtimeId: null);
                break;
            case EventHide:
                RaisePropertyChanged(eventSink, provider, WindowsUiaIds.IsOffscreenProperty, true);
                RaiseStructureChanged(
                    eventSink,
                    GetStructureParent(provider, source, context),
                    StructureChangeType.ChildRemoved,
                    provider.GetRuntimeId());
                break;
        }
    }

    private static WindowsUiaProvider GetStructureParent(
        WindowsUiaProvider provider,
        IPlatformAccessibleObject source,
        WindowsUiaProviderContext context)
        => source.Parent is { } parent ? context.GetOrCreate(parent) : provider;

    private static void RaiseStateProperties(
        IWindowsUiaEventSink eventSink,
        WindowsUiaProvider provider,
        IPlatformAccessibleObject source)
    {
        const int stateUnavailable = 0x1;
        const int stateSelected = 0x2;
        const int stateChecked = 0x10;
        const int stateMixed = 0x20;
        const int stateExpanded = 0x200;
        const int stateCollapsed = 0x400;
        const int actionSelect = 1 << 2;
        const int actionExpand = 1 << 3;
        const int actionCollapse = 1 << 4;

        int state = source.State;
        int actions = source.GetSupportedActions();
        int controlType = source.GetControlType();

        RaisePropertyChanged(
            eventSink,
            provider,
            WindowsUiaIds.IsEnabledProperty,
            (state & stateUnavailable) == 0);

        if (controlType is 7 or 8 or 9)
        {
            int toggleState = (state & stateMixed) != 0
                ? (int)ToggleState.Indeterminate
                : (state & stateChecked) != 0
                    ? (int)ToggleState.On
                    : (int)ToggleState.Off;
            RaisePropertyChanged(eventSink, provider, WindowsUiaIds.ToggleStateProperty, toggleState);
        }

        if ((actions & (actionExpand | actionCollapse)) != 0
            || (state & (stateExpanded | stateCollapsed)) != 0)
        {
            int expandCollapseState = (state & stateExpanded) != 0
                ? (int)ExpandCollapseState.Expanded
                : (state & stateCollapsed) != 0
                    ? (int)ExpandCollapseState.Collapsed
                    : (int)ExpandCollapseState.LeafNode;
            RaisePropertyChanged(
                eventSink,
                provider,
                WindowsUiaIds.ExpandCollapseStateProperty,
                expandCollapseState);
        }

        if ((actions & actionSelect) != 0 || (state & stateSelected) != 0)
        {
            RaisePropertyChanged(
                eventSink,
                provider,
                WindowsUiaIds.SelectionItemIsSelectedProperty,
                (state & stateSelected) != 0);
        }
    }

    private static void RaisePropertyChanged(
        IWindowsUiaEventSink eventSink,
        WindowsUiaProvider provider,
        int propertyId,
        object newValue)
        => eventSink.RaiseAutomationPropertyChangedEvent(
            provider,
            propertyId,
            oldValue: null,
            newValue);

    private static void RaiseEvent(
        IWindowsUiaEventSink eventSink,
        WindowsUiaProvider provider,
        int eventId)
        => eventSink.RaiseAutomationEvent(provider, eventId);

    private static void RaiseStructureChanged(
        IWindowsUiaEventSink eventSink,
        WindowsUiaProvider provider,
        StructureChangeType changeType,
        int[]? runtimeId)
        => eventSink.RaiseStructureChangedEvent(
            provider,
            changeType,
            runtimeId);
}

internal interface IWindowsUiaEventSink
{
    bool ClientsAreListening { get; }

    void RaiseAutomationEvent(WindowsUiaProvider provider, int eventId);

    void RaiseAutomationPropertyChangedEvent(
        WindowsUiaProvider provider,
        int propertyId,
        object? oldValue,
        object? newValue);

    void RaiseStructureChangedEvent(
        WindowsUiaProvider provider,
        StructureChangeType changeType,
        int[]? runtimeId);
}

internal sealed class WindowsUiaNativeEventSink : IWindowsUiaEventSink
{
    public static WindowsUiaNativeEventSink Instance { get; } = new();

    public bool ClientsAreListening => WindowsUiaNativeMethods.ClientsAreListening;

    public void RaiseAutomationEvent(WindowsUiaProvider provider, int eventId)
        => WindowsUiaNativeMethods.RaiseAutomationEvent(provider, eventId);

    public void RaiseAutomationPropertyChangedEvent(
        WindowsUiaProvider provider,
        int propertyId,
        object? oldValue,
        object? newValue)
        => WindowsUiaNativeMethods.RaiseAutomationPropertyChangedEvent(
            provider,
            propertyId,
            oldValue,
            newValue);

    public void RaiseStructureChangedEvent(
        WindowsUiaProvider provider,
        StructureChangeType changeType,
        int[]? runtimeId)
        => WindowsUiaNativeMethods.RaiseStructureChangedEvent(provider, changeType, runtimeId);
}
