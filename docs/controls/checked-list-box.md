# CheckedListBox

Adds an independent check state to each ListBox row. The highlighted selection and the checked items represent different information.

Type `ModernFormsNext.CheckedListBox` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`ListBox`](list-box.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new CheckedListBox());
control.Items.Add("Notify owner");
```

## Configuration example

```csharp
control.CheckOnClick = true;
```

## Interaction example

```csharp
control.ItemCheck += (_, _) => form.Text = "Notification options edited";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `CheckOnClick` | `bool` | false | Controls whether one click immediately changes a row check state. |
| `CheckedItems` | `CheckedListBox.CheckedItemCollection` | Empty | Tracks the checked item objects. |
| `CheckedIndices` | `CheckedListBox.CheckedIndexCollection` | Empty | Tracks their current indices. |

## Methods and behavior

SetItemChecked(index, value) applies a check state. Use GetItemCheckState(index) when indeterminate matters.

## Events

ItemCheck provides the affected row and old/new check state. Read its event data for the pending transition.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Use descriptive, distinguishable row text and check selection separately from the check state.

## Related guides and API

- [Accessibility/semantic model](../accessibility/semantic-model.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/CheckedListBoxPanel.cs)
- [API reference](<xref:ModernFormsNext.CheckedListBox>)
