# MaskedTextBox

Applies a positional mask to typed input, for example a short numeric reference with a separator. The mask describes structure and validation handles application-specific rules.

Type `ModernFormsNext.MaskedTextBox` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`TextBox`](text-box.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new MaskedTextBox("000-000"));
```

## Configuration example

```csharp
control.HidePromptOnLeave = true;
```

## Interaction example

```csharp
control.MaskInputRejected += (_, _) => form.Text = "Enter six digits";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Mask` | `string` | Empty | Defines editable positions and literal characters. |
| `PromptChar` | `Char` | '_' | Fills unassigned positions. |
| `MaskCompleted` | `bool` | Depends on the mask | Reports whether required positions are filled. |

## Methods and behavior

ValidateText() applies the configured ValidatingType and raises the type-validation event. Inspect MaskCompleted before accepting a required value.

## Events

MaskInputRejected reports rejected input. TypeValidationCompleted is separate from ordinary TextChanged notifications.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Use a persistent label and explain the format in clear text. The IME qualification matrix enumerates the editors tested natively.

## Related guides and API

- [Text input](../text-input.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/MaskedTextBoxPanel.cs)
- [API reference](<xref:ModernFormsNext.MaskedTextBox>)
