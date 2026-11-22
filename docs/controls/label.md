# Label

Displays short static text and optionally an image. Use it for captions, field descriptions and read-only status.

Type `ModernFormsNext.Label` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Label { Text = "Server status" });
```

## Configuration example

```csharp
control.AutoEllipsis = true;
control.Width = 120;
```

## Interaction example

```csharp
control.TextChanged += (_, _) => System.Diagnostics.Debug.WriteLine(control.Text);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `AutoEllipsis` | `bool` | false | Truncates overflowing text when enabled. |
| `Multiline` | `bool` | false | Allows multiple display lines. |
| `UseMnemonic` | `bool` | true | Enables mnemonic interpretation. |

## Methods and behavior

Use inherited GetPreferredSize(Size) to measure the caption for the available layout.

## Events

TextAlignChanged reports alignment changes. Inherited TextChanged tracks new captions.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keep meaningful status accessible when using ellipsis. Do not use a static label as an action surface.

## Related guides and API

- [Shadows and typography](../shadows-and-typography.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/LabelPanel.cs)
- [API reference](<xref:ModernFormsNext.Label>)
