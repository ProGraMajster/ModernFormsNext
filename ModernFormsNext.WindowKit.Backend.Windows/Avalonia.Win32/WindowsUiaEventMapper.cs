using System.Windows.Automation;
using System.Windows.Automation.Provider;
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
        WindowsUiaProviderContext context)
    {
        switch (eventId)
        {
            case EventFocus:
                RaiseEvent(provider, AutomationElementIdentifiers.AutomationFocusChangedEvent);
                break;
            case EventNameChange:
                RaisePropertyChanged(provider, AutomationElementIdentifiers.NameProperty, source.Name ?? string.Empty);
                break;
            case EventDescriptionChange:
                RaisePropertyChanged(
                    provider,
                    AutomationElementIdentifiers.HelpTextProperty,
                    source.Help ?? source.Description ?? string.Empty);
                break;
            case EventValueChange when !source.IsSensitive:
                RaisePropertyChanged(provider, ValuePatternIdentifiers.ValueProperty, source.Value ?? string.Empty);
                break;
            case EventStateChange:
                RaisePropertyChanged(
                    provider,
                    AutomationElementIdentifiers.IsEnabledProperty,
                    (source.State & 0x1) == 0);
                break;
            case EventLocationChange:
                RaisePropertyChanged(
                    provider,
                    AutomationElementIdentifiers.BoundingRectangleProperty,
                    WindowsUiaCoordinateConverter.ToBoundingRectangle(source.Bounds));
                break;
            case EventSelection:
                RaiseEvent(provider, SelectionItemPatternIdentifiers.ElementSelectedEvent);
                break;
            case EventSelectionAdd:
                RaiseEvent(provider, SelectionItemPatternIdentifiers.ElementAddedToSelectionEvent);
                break;
            case EventSelectionRemove:
                RaiseEvent(provider, SelectionItemPatternIdentifiers.ElementRemovedFromSelectionEvent);
                break;
            case EventSelectionWithin:
                RaiseEvent(provider, SelectionPatternIdentifiers.InvalidatedEvent);
                break;
            case EventReorder:
                RaiseStructureChanged(provider, StructureChangeType.ChildrenReordered);
                break;
            case EventParentChange:
                RaiseStructureChanged(provider, StructureChangeType.ChildrenReordered);
                break;
            case EventShow:
                RaisePropertyChanged(provider, AutomationElementIdentifiers.IsOffscreenProperty, false);
                RaiseStructureChanged(GetStructureParent(provider, source, context), StructureChangeType.ChildAdded);
                break;
            case EventHide:
                RaisePropertyChanged(provider, AutomationElementIdentifiers.IsOffscreenProperty, true);
                RaiseStructureChanged(GetStructureParent(provider, source, context), StructureChangeType.ChildRemoved);
                break;
        }
    }

    private static WindowsUiaProvider GetStructureParent(
        WindowsUiaProvider provider,
        IPlatformAccessibleObject source,
        WindowsUiaProviderContext context)
        => source.Parent is { } parent ? context.GetOrCreate(parent) : provider;

    private static void RaisePropertyChanged(
        IRawElementProviderSimple provider,
        AutomationProperty property,
        object newValue)
        => AutomationInteropProvider.RaiseAutomationPropertyChangedEvent(
            provider,
            new AutomationPropertyChangedEventArgs(
                property,
                AutomationElement.NotSupported,
                newValue));

    private static void RaiseEvent(IRawElementProviderSimple provider, AutomationEvent eventId)
        => AutomationInteropProvider.RaiseAutomationEvent(
            eventId,
            provider,
            new AutomationEventArgs(eventId));

    private static void RaiseStructureChanged(WindowsUiaProvider provider, StructureChangeType changeType)
        => AutomationInteropProvider.RaiseStructureChangedEvent(
            provider,
            new StructureChangedEventArgs(changeType, provider.GetRuntimeId()));
}
