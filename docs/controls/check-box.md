# CheckBox

Represents an independent option. Three-state mode can represent an inherited or mixed value.

Type `ModernFormsNext.CheckBox` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new CheckBox { Text = "Enable alerts" });
```

## Configuration example

```csharp
control.ThreeState = true;
```

## Interaction example

```csharp
control.CheckStateChanged += (_, _) => form.Text = control.CheckState.ToString();
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `ThreeState` | `bool` | false | Allows the indeterminate state during user cycling. |
| `AutoCheck` | `bool` | true | Updates state on activation. |
| `Checked` | `bool` | false | Projects the current check state to a Boolean. |

## Methods and behavior

Assign CheckState when restoring a mixed value. Assign Checked when the underlying setting is strictly Boolean.

## Events

CheckedChanged tracks the Boolean projection. CheckStateChanged tracks transitions between all supported states, including programmatic assignments.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keyboard activation changes the state when the control has focus. Give the option a text label.

## Related guides and API

- [Accessibility/semantic model](../accessibility/semantic-model.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/CheckBoxPanel.cs)
- [API reference](<xref:ModernFormsNext.CheckBox>)
