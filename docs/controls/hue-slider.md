# HueSlider

Selects a hue along a color spectrum. Compose with ColorBox or editable component inputs to make a full picker.

Type `ModernFormsNext.HueSlider` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new HueSlider());
```

## Configuration example

```csharp
control.SetHueSilently(210);
```

## Interaction example

```csharp
control.HueChanged += (_, _) => form.Text = $"Hue {control.Hue:F0}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Hue` | `float` | 0 | Current hue component. |
| `Style` | `ControlStyle` | Per-instance | Controls the slider appearance. |

## Methods and behavior

SetHueSilently(value) updates the displayed hue when synchronizing from another editor without recursively raising HueChanged.

## Events

HueChanged observes normal value edits. Silent synchronization intentionally suppresses it.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide a name and a textual or numeric hue option so visual color is not the only input path.

## Related guides and API

- [Styling](../styling.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ColorDialogPanel.cs)
- [API reference](<xref:ModernFormsNext.HueSlider>)
