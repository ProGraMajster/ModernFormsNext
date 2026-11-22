# RadioButton

Selects one option from a group. Group related options under the same parent and use separate containers for unrelated groups.

Type `ModernFormsNext.RadioButton` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new RadioButton { Text = "Compact" });
```

## Configuration example

```csharp
control.AutoSize = true;
```

## Interaction example

```csharp
control.CheckedChanged += (_, _) => { if (control.Checked) form.Text = "Compact"; };
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `AutoCheck` | `bool` | true | Selects this radio on activation. |
| `Checked` | `bool` | false | Marks the radio as selected. |

## Methods and behavior

Assign Checked to select the initial option from saved preferences. Keep sibling choices in the same container.

## Events

CheckedChanged can report both selection and deselection. Check the value before applying the selected option.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Give each radio button a distinct label and test tab order against the surrounding group.

## Related guides and API

- [Accessibility/semantic model](../accessibility/semantic-model.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/RadioButtonPanel.cs)
- [API reference](<xref:ModernFormsNext.RadioButton>)
