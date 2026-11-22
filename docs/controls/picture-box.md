# PictureBox

Displays an SKBitmap with configurable sizing. It can also load an image from its location using the control's load path.

Type `ModernFormsNext.PictureBox` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new PictureBox { AccessibleName = "Preview" });
```

## Configuration example

```csharp
control.Size = new Size(240, 120);
```

## Interaction example

```csharp
control.SizeModeChanged += (_, _) => form.Text = "Image fit changed";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Image` | `SKBitmap` | null | Bitmap displayed by the control. |
| `ImageLocation` | `string` | null | Optional source for Load() |
| `IsErrored` | `bool` | false | State after an unsuccessful load. |

## Methods and behavior

Assign Image for a caller-owned bitmap. To use the control's path/URL loader, assign ImageLocation or call Load(string). Observe IsErrored when using the loader.

## Events

SizeModeChanged tracks fit configuration. Use your own failure path when decoding images outside the control.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Name meaningful images and retain text for information conveyed by decorative imagery. Avoid large decodes in a paint callback.

## Related guides and API

- [Control fundamentals](control-fundamentals.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/PictureBoxPanel.cs)
- [API reference](<xref:ModernFormsNext.PictureBox>)
