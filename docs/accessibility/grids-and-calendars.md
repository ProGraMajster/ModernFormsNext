# Accessible grids and calendars

`DataGridView` and `DateTimePicker` extend the canonical `AccessibleObject`
hierarchy. Their logical parts are the controls' existing rows, cells, headers,
checkbox, step buttons and calendar surface. Reading semantics never creates an
editor, scrolls, changes selection or opens a window. All access is UI-thread-affine.

## Grid and table contracts

`AccessibleObject.GridProvider` optionally supplies current row/column counts,
`GetItem(row, column)`, traversal order and table header associations.
`AccessibleObject.GridCell` supplies the containing canonical grid peer, zero-based
coordinates, positive spans and immutable header associations. Existing enum values
are preserved; DataGrid, DataItem, Header, HeaderItem and Calendar are additive.

```csharp
var grid = new DataGridView
{
    AccessibleName = "Order lines",
    AccessibleAutomationId = "order.lines",
    RowHeadersVisible = true,
    SelectionMode = DataGridViewSelectionMode.CellSelect
};
grid.Columns.Add("Product", 200);
grid.Columns.Add("Quantity", 100);
grid.Rows.Add("Keyboard", "2");

// Inspect the actual cell without creating a visual TextBox or changing focus.
var item = grid.AccessibilityObject.GridProvider!.GetItem(0, 1)!;
var coordinates = item.GridCell!;

// The normal editing path validates/commits the string and raises normal events.
item.PerformAction(AccessibleActions.SetValue, "3");
```

Hidden columns are omitted from semantic column coordinates. Headers use current
header text; row headers use the ordinal already drawn by the grid. Scrolled-out
rows/cells remain semantic items with clipped/empty bounds and Offscreen state;
they are not marked Invisible merely because they need revealing. ScrollIntoView
uses the grid's existing scrollbars and preserves ordinary selection behavior.

Rows/cells/header peers retain their identity through sorting. A removed/reinserted
row, column or cell receives a new attachment lifetime; an old peer becomes inert.
A sparse row can expose a blank logical slot without adding a model cell. An
accepted edit materializes that slot; clients then reacquire its concrete cell peer.
Only requested peers are allocated while traversing children. This works with the
current managed row model; it does not implement the future recycler in issue #55.

Grid cell selection commits both coordinates before `SelectionChanged`. Row headers
activate their row, and column headers use the ordinary sortable-header action.
An active editor captures its actual row, column, cell and bound model object.
Sorting cannot redirect a commit to whichever row now occupies the old index.
Replacement/removal, closed windows and reentrant callbacks cancel obsolete work.
If an edit callback opens a replacement editor for the same cell, an outer semantic
SetValue request returns false and leaves that replacement's text and session intact.
Only the exact edit session accepted by that request can receive and commit its value.
Cleanup completes even when application selection or edit callbacks throw; the
original failure, or an aggregate of multiple failures, is propagated afterwards.

DataSource-generated rows are fully bound before their Reorder notification. A
getter or callback that replaces DataSource invalidates the old population request.
Readable indexed properties are not auto-generated. This remains the existing
reflection-based IList binding, with culture-aware scalar conversion, not a new
binding manager or arbitrary collection virtualization API.

One cell can associate at most 64 non-null row headers and 64 column headers;
construction validates and copies these lists, stopping oversized enumeration.
Windows rejects table header axes over 65,536 before extracting all their peers,
and checks the returned list before allocating its native SAFEARRAY.

Windows maps these contracts to Grid/GridItem and, where headers exist,
Table/TableItem. Android maps collection dimensions, cell coordinates/spans,
selection and header metadata. An editable grid cell uses Android SetText through
the same string SetValue action; a read-only cell does not advertise that mutation.
Sensitive ancestors suppress grid metadata and native payload reads. Automation
snapshots carry detached grid metadata with canonical runtime IDs and bounded
same-root associations; optional JSON fields preserve protocol version 1.

## Date picker and calendar

```csharp
var date = new DateTimePicker
{
    AccessibleName = "Delivery date",
    ShowCheckBox = true,
    Checked = false,
    Format = DateTimePickerFormat.Short
};

// The checkbox stays available while the date is unchecked.
date.AccessibilityObject.GetChild(0)!.PerformAction(AccessibleActions.Toggle);
date.AccessibilityObject.PerformAction(AccessibleActions.SetValue, "2026-09-12");
```

SetValue reuses `Text` and its current-culture parser, supported date limits and
empty-string reset. Programmatic assignment activates an optional checkbox.
Increment/decrement reuse the existing format-specific step (minutes for Time,
the documented Custom heuristic, otherwise days) and clamp at the date limits.
Space toggles the checkbox even when it was unchecked. Disabled controls reject
these actions. ShowUpDown exposes the actual two step buttons; ordinary mode exposes
the actual calendar button.

Expand/Collapse is available only with an existing usable Form popup host. A
windowless Android surface supports the value, checkbox and step actions without
advertising a native popup it cannot create. General Android Form/popup hosting is
still tracked by issue #72; these peers do not introduce another hosting system.

The real popup calendar exposes previous/next, month/year titles, Today, day headers
and its date/month/year grid. Day names and full date labels follow the current
culture and the existing calendar's first-day-of-week layout. Disabled dates and
out-of-range month/year choices remain readable and cannot be selected.
Dates outside the culture calendar's own supported interval use an invariant
Gregorian date/month label, so adjacent disabled cells remain readable at a boundary.

Arrow keys move the focused date (one day horizontally, one week vertically), or the
focused month/year cell. PageUp/PageDown use the existing month/year/range navigation;
Home/End select the focused page edge; Enter/Space activates the focused cell.
Escape returns to the day view or closes it. Keyboard focus is drawn independently
of the selected value. Selecting a date preserves the original time's complete
tick precision and DateTime.Kind, clamping the time at MinDate/MaxDate boundaries.

Calendar peers belong to a specific presentation generation. Moving to another
month/view retires the previous date peers; clients reacquire the current grid
instead of treating a retained slot as a new date. The cache holds only the current
page (at most 54 peers). Popup ownership is retired before hide/dispose/CloseUp
callbacks. Closing, hiding, reparenting or disposing during DropDown cannot resume
an obsolete Show, and cleanup cannot erase a replacement popup opened by a callback.
Each picker session owns its native popup and controls; disposal destroys that native
window and releases the calendar tree even when another cleanup callback fails.

The popup retains its separate native semantic root and real parent hierarchy. It
inherits sensitivity from the logical picker owner and its current ancestors on
every read. Enabling protection after opening suppresses the popup name, calendar value,
selected-date result, grid capability and cell associations; retained grid providers
return zero dimensions and no items or headers. Removing protection restores reads
for the same live session. A failed owner privacy getter or a callback that retires
the session also blocks these reads. Ordinary focus and navigation remain available.
The calendar supplies date/grid semantics; it does not expose an editable text
document or text-range capability. See [privacy rules](semantic-model.md#names-values-and-sensitive-data).

Picker/calendar bounds, hit testing and painting share the same scaled rectangles,
including queries before the first paint. The implementation continues the existing
calendar rendering model; it does not claim support for a separate non-Gregorian
calendar engine. Automated TestHost/native-provider tests and emulator/device
observations are reported separately in the Phase 4 validation record.
