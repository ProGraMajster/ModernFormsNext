# ListBox

Displays a collection of text-representable rows with configurable selection. Use a CollectionView when you need a custom item container and data virtualization.

Type `ModernFormsNext.ListBox` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new ListBox());
control.Items.Add("Queued");
control.Items.Add("Running");
```

## Configuration example

```csharp
control.SelectedIndex = 0;
```

## Interaction example

```csharp
control.SelectedIndexChanged += (_, _) => form.Text = control.SelectedItem?.ToString() ?? "No selection";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `SelectedIndex` | `int` | -1 | Index of the selected row. |
| `ItemHeight` | `int` | 21 logical units | Controls the height used for each row. |
| `SelectionMode` | `SelectionMode` | One | Controls whether multiple rows can be selected. |

## Methods and behavior

FindString searches the item text. GetItemRectangle and GetIndexAtLocation expose row geometry for custom interaction.

## Events

SelectedIndexChanged reports selection changes. Use SelectedItems when multiple selection is enabled.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide readable item text and verify focus and selection after changing a collection with the keyboard.

## Related guides and API

- [Virtualization](../virtualization.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ListBoxPanel.cs)
- [API reference](<xref:ModernFormsNext.ListBox>)
