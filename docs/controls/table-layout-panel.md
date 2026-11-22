# TableLayoutPanel

Places controls on a grid using row and column style rules. Use spans for shared headers or larger content areas.

Type `ModernFormsNext.TableLayoutPanel` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Panel`](panel.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new TableLayoutPanel { ColumnCount = 2, RowCount = 2 });
var title = control.Controls.Add(new Label { Text = "Settings" });
```

## Configuration example

```csharp
control.SetCellPosition(title, new TableLayoutPanelCellPosition(0, 0));
control.SetColumnSpan(title, 2);
```

## Interaction example

```csharp
control.Layout += (_, _) => System.Diagnostics.Debug.WriteLine(control.GetColumnWidths().Length);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `RowCount` | `int` | 0 | Initial row count. |
| `ColumnCount` | `int` | 0 | Initial column count. |
| `GrowStyle` | `TableLayoutPanelGrowStyle` | AddRows | How the grid grows when capacity is exceeded. |

## Methods and behavior

SetCellPosition takes logical column and row coordinates. SetRowSpan and SetColumnSpan reserve adjacent cells. GetControlFromPosition retrieves the occupant of a logical cell.

## Events

Use inherited Layout after a sizing pass. Defer additional mutations so the layout cannot reenter indefinitely.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Right-to-left layout can mirror the physical grid. Preserve logical tab order when coordinates are mirrored.

## Related guides and API

- [Right to left layout](../right-to-left-layout.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/TableLayoutPanelPanel.cs)
- [API reference](<xref:ModernFormsNext.TableLayoutPanel>)
