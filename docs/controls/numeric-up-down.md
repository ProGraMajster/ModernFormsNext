# NumericUpDown

Edits a bounded decimal value using an embedded text field and step buttons. Use commit notifications when the distinction between typing and accepting matters.

Type `ModernFormsNext.NumericUpDown` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new NumericUpDown());
```

## Configuration example

```csharp
control.Minimum = 0;
control.Maximum = 100;
control.Increment = 5;
```

## Interaction example

```csharp
control.ValueCommitted += (_, _) => form.Text = $"Quantity {control.Value}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Increment` | `decimal` | 1 | Amount added or subtracted by the step actions. |
| `DecimalPlaces` | `int` | 0 | Digits shown after the decimal separator. |
| `Value` | `decimal` | 0 | Current numeric value. |

## Methods and behavior

UpButton() and DownButton() apply a step respecting the range and edit contract. Set the bounds before setting Value.

## Events

ValueChanged tracks the effective value. ValueCommitted is for accepted edits. The editor inherits the effective typography of its owner.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

The native adapters expose a numeric range/stepper and its logical parts. Verify value announcement on the intended host.

## Related guides and API

- [Accessibility/current controls](../accessibility/current-controls.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/NumericUpDownPanel.cs)
- [API reference](<xref:ModernFormsNext.NumericUpDown>)
