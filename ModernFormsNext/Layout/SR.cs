// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ModernFormsNext.Layout;

internal class SR
{
    internal static string CatAppearance => "Appearance";

    internal static string CatBehavior => "Behavior";

    internal static string CatData => "Data";

    internal static string CatItems => "Items";

    internal static string CatPropertyChanged => "Property Changed";

    internal static string BindableComponentBindingContextChangedDescr => "Event raised when the binding context changes.";

    internal static string BindingComponentBindingContextDescr => "The binding context used to resolve data-binding managers for this component.";

    internal static string BindingNotSupported => "Data binding is not supported in this build of ModernFormsNext.";

    internal static string collectionChangedEventDescr => "Event raised after the collection changes.";

    internal static string collectionChangingEventDescr => "Event raised before the collection changes.";

    internal static string DescriptionBindingNavigator => "Displays navigation and editing commands for a BindingSource.";

    internal static string DescriptionBindingSource => "Encapsulates a data source for binding to ModernFormsNext components.";

    internal static string CannotActivateControl => "Invisible or disabled control cannot be activated.";

    internal static string CircularOwner => "A circular control reference has been made. A control cannot be owned by or parented to itself.";

    internal static string ControlNotChild => "'child' is not a child control of this parent.";

    internal static string ControlBindingContextDescr => "The binding context used to resolve data-binding managers for this control.";

    internal static string ControlFontDescr => "The font used to render text in the control.";

    internal static string ControlOnBindingContextChangedDescr => "Event raised when the control binding context changes.";

    internal static string ControlOnFontChangedDescr => "Event raised when the control font changes.";

    internal static string ControlOnHelpDescr => "Event raised when contextual help is requested for the control.";

    internal static string ControlOnQueryAccessibilityHelpDescr => "Event raised when accessibility help metadata is requested for the control.";

    internal static string ControlTagDescr => "User-defined data associated with the component.";

    internal static string DescriptionHelpProvider => "Provides contextual help metadata for controls.";

    internal static string FindKeyMayNotBeEmptyOrNull => "Key specified was either empty or null.";

    internal static string HelpInvalidURL => "The help URL '{0}' could not be resolved.";

    internal static string HelpProviderHelpKeywordDescr => "The help keyword associated with the extended control.";

    internal static string HelpProviderHelpNamespaceDescr => "The help namespace, file, or URL used by this provider.";

    internal static string HelpProviderHelpStringDescr => "The contextual help string associated with the extended control.";

    internal static string HelpProviderNavigatorDescr => "The help navigation mode used for the extended control.";

    internal static string HelpProviderShowHelpDescr => "Determines whether help is displayed for the extended control.";

    internal static string HelpUnableToLaunch => "The help target '{0}' could not be opened by the operating system.";

    internal static string IndexOutOfRange => "Index {0} is out of range.";

    internal static string InvalidArgument => "'{1}' is not a valid value for '{0}'.";

    internal static string InvalidLowBoundArgumentEx => "Value of '{1}' is not valid for '{0}'. '{0}' must be greater than or equal to {2}.";

    internal static string LayoutEngineUnsupportedType => "LayoutEngine cannot arrange objects of type '{0}'";

    internal static string OnlyOneControl => "Cannot add or insert the item '{0}' in more than one place. You must first remove it from its current location or clone it.";

    internal static string PropertyValueInvalidEntry => "One or more entries are not valid in the IDictionary parameter. Verify that all values match up to the object's properties.";

    internal static string TableLayoutPanelFullDesc => "Additional Rows or Columns cannot be created.  TableLayoutPanel is full and GrowStyle is 'FixedSize'.";

    internal static string TableLayoutPanelSpanDesc => "TableLayoutPanel cannot expand to contain the control, because the panel's GrowStyle property is set to 'FixedSize'.";

    internal static string TableLayoutSettingsConverterNoName => "Cannot convert TableLayoutSettings to string: could not find a 'Name' string property on a control.";

    internal static string TableLayoutSettingSettingsIsNotSupported => "Directly setting TableLayoutSettings is not supported.  Use individual properties instead.";

    internal static string TextParseFailedFormat => "Parse of Text('{0}') expected text in the format '{1}' did not succeed.";

    internal static string RelatedListManagerChild => "Child list for field {0} cannot be created.";

    internal static string PropertyManagerPropDoesNotExist => "Property {0} does not exist in {1}.";

    internal static string DataBindingAddNewNotSupportedOnPropertyManager => "AddNew is not supported for property to property binding.";

    internal static string DataBindingRemoveAtNotSupportedOnPropertyManager => "RemoveAt is not supported for property-to-property binding.";

    internal static string BindingsCollectionAdd1 => "dataBinding already belongs to this BindingsCollection.";

    internal static string BindingsCollectionAdd2 => "dataBinding belongs to another BindingsCollection.";

    internal static string BadDataSourceForComplexBinding => "Complex DataBinding accepts as a data source either an IList or an IListSource.";

    internal static string BindingManagerBadIndex => "Index is out of bounds.";

    internal static string CommandIdNotAllocated => "A command ID could not be allocated.";

    internal static string CurrencyManagerCantAddNew => "Items cannot be added to the data source because it does not implement IBindingList.";

    internal static string DataBindingCycle => "Binding to '{0}' would create a cycle in the binding graph.";

    internal static string DataBindingPushDataException => "No item in the list could accept the current binding values.";

    internal static string ListBindingBindField => "Cannot bind to the property or column '{0}' on the data source.";

    internal static string ListBindingBindProperty => "Cannot bind to the property '{0}' on the target component.";

    internal static string ListBindingBindPropertyReadOnly => "Cannot bind to the property '{0}' because it is read-only.";

    internal static string ListBindingFormatFailed => "The value could not be converted to the target property type.";

    internal static string ListManagerBadPosition => "Position must be between 0 and Count - 1.";

    internal static string ListManagerEmptyList => "The list is empty.";

    internal static string ListManagerNoValue => "There is no value at index {0}.";

    internal static string ListManagerSetDataSource => "Cannot set the data source to an object of type '{0}'. Complex binding requires an IList or IListSource.";

    internal static string BindingNavigatorAddNewItemPropDescr => "The ToolStripItem on the BindingNavigator that raises the 'Add new' action.";
    
    internal static string BindingNavigatorAddNewItemText => "Add new";
    
    internal static string BindingNavigatorBindingSourcePropDescr => "The BindingSource that the BindingNavigator navigates.";
    
    internal static string BindingNavigatorCountItemFormat => "of {0}";
    
    internal static string BindingNavigatorCountItemFormatPropDescr => "Formatting to apply to count displayed in the CountItem ToolStrip item.";
    
    internal static string BindingNavigatorCountItemPropDescr => "The ToolStripItem on the BindingNavigator that displays the total number of items.";
    
    internal static string BindingNavigatorCountItemTip => "Total number of items";
    
    internal static string BindingNavigatorDeleteItemPropDescr => "The ToolStripItem on the BindingNavigator that raises the 'Delete' action.";
    
    internal static string BindingNavigatorDeleteItemText => "Delete";

    internal static string BindingNavigatorMoveFirstItemPropDescr => "The ToolStripItem on the BindingNavigator that raises the 'Move first' action.";
    
    internal static string BindingNavigatorMoveFirstItemText => "Move first";
    
    internal static string BindingNavigatorMoveLastItemPropDescr => "The ToolStripItem on the BindingNavigator that raises the 'Move last' action.";
    
    internal static string BindingNavigatorMoveLastItemText => "Move last";
    
    internal static string BindingNavigatorMoveNextItemPropDescr => "The ToolStripItem on the BindingNavigator that raises the 'Move next' action.";
    
    internal static string BindingNavigatorMoveNextItemText => "Move next";
    
    internal static string BindingNavigatorMovePreviousItemPropDescr => "The ToolStripItem on the BindingNavigator that raises the 'Move previous' action.";
    
    internal static string BindingNavigatorMovePreviousItemText => "Move previous";
    
    internal static string BindingNavigatorPositionAccessibleName => "Position";
    
    internal static string BindingNavigatorPositionItemPropDescr => "The ToolStripItem on the BindingNavigator that displays the current position.";

    internal static string BindingNavigatorPositionItemTip => "Current position";

    internal static string BindingNavigatorRefreshItemsEventDescr => "Event raised when BindingNavigator ToolStrip items need to be refreshed to reflect current state of data.";

    internal static string BindingNavigatorToolStripName => "Binding Navigator";

    internal static string BindingsCollectionBadIndex => "Binding {0       } cannot be found.";

    internal static string BindingsCollectionDup => "This causes two bindings in the collection to bind to the same property.";

    internal static string BindingsCollectionForeign => "Binding does not belong to this BindingsCollection.";

    internal static string BindingSourceAddingNewEventHandlerDescr => "Event raised when the user calls AddNew on the BindingSource";

    internal static string BindingSourceAllowNewDescr => "Determines whether the BindingSource allows new items to be added to the list.";
    
    internal static string BindingSourceBadSortString => "Sort string not valid.";
    
    internal static string BindingSourceBindingCompleteEventHandlerDescr => "Event raised after data has been exchanged between the data source and a control property bound to that data source.";
    
    internal static string BindingSourceBindingListWrapperAddToReadOnlyList => "Item cannot be added to a read-only or fixed-size list.";
    
    internal static string BindingSourceBindingListWrapperNeedAParameterlessConstructor => "AddNew cannot be called on the '{0}' type. This type does not have a public default constructor. You can call AddNew on the '{0}' type if you handle the AddingNew event and create the appropriate object.";
    
    internal static string BindingSourceBindingListWrapperNeedToSetAllowNew => "AddNew cannot be called on the '{0}' type. This type does not have a public default constructor. You can call AddNew on the '{0}' type if you set AllowNew=true and handle the AddingNew event.";

    internal static string NoAllowNewOnReadOnlyList => "Items cannot be added to a read-only or fixed-size list.";
    
    internal static string BindingSourceCurrentChangedEventHandlerDescr => "Event raised when the value of Current changes.";
    
    internal static string BindingSourceCurrentItemChangedEventHandlerDescr => "Event raised when the value of Current changes, or a property of the current item changes.";
    
    internal static string BindingSourceDataErrorEventHandlerDescr => "Event raised when an exception thrown during data binding is handled internally by the CurrencyManager.";
    
    internal static string BindingSourceDataMemberChangedEventHandlerDescr => "Event raised when the DataMember changes.";
    
    internal static string BindingSourceDataMemberDescr => "Indicates a sub-list of the DataSource that the BindingSource is bound to.";

    internal static string BindingSourceDataSourceChangedEventHandlerDescr => "Event raised when the DataSource changes.";

    internal static string BindingSourceDataSourceDescr => "Indicates the source of data for the BindingSource.";

    internal static string BindingSourceFilterDescr => "Indicates a database column expression used to filter the set of rows returned by the data source.";
    
    internal static string BindingSourceInstanceError => "BindingSource unable to create list based on the Type specified in the DataSource property.";

    internal static string BindingSourceItemChangedEventModeDescr => "Controls how the BindingSource raises the ListChanged event as a result of changing an item in the BindingSource.";


    internal static string BindingSourceItemTypeIsValueType => "Cannot add null to BindingSource if the underlying list stores value types.";

    internal static string BindingSourceItemTypeMismatchOnAdd => "Objects added to a BindingSource's list must all be of the same type.";


    internal static string BindingSourceListChangedEventHandlerDescr => "Event raised when a change occurs in the BindingSource's list.";

    internal static string BindingSourcePositionChangedEventHandlerDescr => "Event raised when the value of Position changes.";


    internal static string BindingSourceRecursionDetected => "BindingSource cannot be its own data source. Do not set the DataSource and DataMember properties to values that refer back to BindingSource.";

    internal static string BindingSourceRemoveCurrentNoCurrentItem => "Current item cannot be removed from the list because there is no current item.";


    internal static string BindingSourceRemoveCurrentNotAllowed => "Current item cannot be removed from the list because the list does not allow removal of items.";


    internal static string BindingSourceSortDescr => "Indicates names of database columns used to sort the set of rows returned by the data source.";
    
    internal static string BindingSourceSortStringPropertyNotInIBindingList => "Sort string contains a property that is not in the IBindingList.";

    internal static string OperationRequiresIBindingList => "This operation requires a data source that implements IBindingList.";

    internal static string OperationRequiresIBindingListView => "This operation requires a data source that implements IBindingListView.";

    
    internal static string DataSourceDataMemberPropNotFound => "DataMember property '{0}' cannot be found on the DataSource.";
    
    internal static string DataSourceLocksItems => "Items collection cannot be modified when the DataSource property is set.";


    internal static string ICurrencyManagerProviderDescr => "Provides custom binding management for components.";

}
