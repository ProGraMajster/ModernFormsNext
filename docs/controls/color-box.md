# ColorBox

Edits saturation and value for the current hue. Pair it with HueSlider when building a custom color picker.

Type `ModernFormsNext.ColorBox` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new ColorBox());
```

## Configuration example

```csharp
control.SetColorComponents(210, 0.65f, 0.8f);
```

## Interaction example

```csharp
control.ColorChanged += (_, _) => form.Text = $"Saturation {control.Saturation:P0}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Hue` | `float` | 0 | Hue used to render the selection surface. |
| `Saturation` | `float` | 1 | Saturation component. |
| `Value` | `float` | 1 | Brightness component. |

## Methods and behavior

SetColorComponents changes all three coordinates. Use the color component values to update an independent preview or model.

## Events

ColorChanged signals an effective color selection. Guard paired updates when synchronizing several editors.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide a numeric or textual representation when the selection must be usable independently of visual color.

## Related guides and API

- [Styling](../styling.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ColorDialogPanel.cs)
- [API reference](<xref:ModernFormsNext.ColorBox>)
