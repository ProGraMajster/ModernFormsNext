# DataGridView

Displays managed columns and rows with selection, scrolling, sorting and supported cell editing. Choose explicit columns when display order or formatting matters.

Type `ModernFormsNext.DataGridView` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new DataGridView());
```

## Configuration example

```csharp
control.DataSource = new[] { new { Customer = "Northwind", OpenOrders = 3 } };
control.ReadOnly = true;
```

## Interaction example

```csharp
control.SelectionChanged += (_, _) => form.Text = $"Row {control.SelectedRowIndex}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `DataSource` | `IList` | null | Optional bound data. |
| `ReadOnly` | `bool` | false | Suppresses editing when true. |
| `CurrentCell` | `DataGridViewCell` | null | Current cell when a row and column are selected. |

## Methods and behavior

Use BeginEdit, EndEdit or CancelEdit around the current editable cell. SortByColumn orders the selected column using grid semantics. GetCellBounds exposes its visible geometry.

## Events

CellBeginEdit, CellEndEdit and CellValueChanged cover the edit lifecycle. Observe SelectionChanged separately from a committed value.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The standard Designer previews available metadata and serializable appearance. Supply runtime data, templates, delegates and native integrations in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

The grid exposes row/column, header, selection and value semantics. Check accessibility and editing for each custom cell presentation.

## Editable rows and explicit columns

Use explicit columns and rows when the application owns the values rather
than projecting a source. Pick `CellSelect` when the user's operation acts
on one cell.

```csharp
var editable = form.Controls.Add(new DataGridView
{
    SelectionMode = DataGridViewSelectionMode.CellSelect,
    RowHeadersVisible = true
});
editable.Columns.Add("Customer", 160);
editable.Columns.Add("Open orders", 90);
editable.Rows.Add("Northwind", "3");
editable.SelectedRowIndex = 0;
editable.SelectedColumnIndex = 1;
editable.CellValueChanged += (_, _) => form.Text = "Orders updated";
```

For a bound read-write grid, make sure the model property is writable and
the cell type supports editing. A read-only model is useful for the
projection shown above. When selecting a cell programmatically, use
`BeginEdit()` to request editing and inspect the result before assuming an
editor is open. The native accessibility guide documents value and reveal
patterns for the default cell path.

## Related guides and API

- [Accessibility/grids and calendars](../accessibility/grids-and-calendars.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/DataGridViewPanel.cs)
- [API reference](<xref:ModernFormsNext.DataGridView>)
