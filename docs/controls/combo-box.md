# ComboBox

Presents a compact selection from an item collection and opens a managed list popup. Use it when only one existing item should be selected.

Type `ModernFormsNext.ComboBox` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new ComboBox());
control.Items.Add("Daily");
control.Items.Add("Weekly");
```

## Configuration example

```csharp
control.SelectedIndex = 0;
```

## Interaction example

```csharp
control.SelectedIndexChanged += (_, _) => form.Text = control.SelectedItem?.ToString() ?? "Choose frequency";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `SelectedIndex` | `int` | -1 | Index of the selection, or minus one when empty. |
| `DroppedDown` | `bool` | false | Whether the popup is open. |

## Methods and behavior

Update Items and assign SelectedIndex to restore a stored choice after populating the list.

## Events

SelectedIndexChanged also follows programmatic selection. DropDownOpened and DropDownClosed observe popup lifetime.

## Platform support

Windows is the primary runtime. On experimental Android, window and popup behavior requires the bounded Activity/Form host where applicable. A standalone shared surface does not provide independent desktop windows.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Set the accessible name independently of the selected value, and test the popup in the target window host.

## Related guides and API

- [Text input](../text-input.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ComboBoxPanel.cs)
- [API reference](<xref:ModernFormsNext.ComboBox>)
