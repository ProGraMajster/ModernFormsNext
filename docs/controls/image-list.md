# ImageList

Stores shared bitmap assets for consumers that accept ImageList images by key or index. Set a consistent image size for the consuming controls.

Type `ModernFormsNext.ImageList` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Component`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = new ImageList();
form.Disposed += (_, _) => control.Dispose();
var button = form.Controls.Add(new Button { Text = "Save", ImageList = control });
```

## Configuration example

```csharp
control.ImageSize = new SKSize(24, 24);
```

## Interaction example

```csharp
button.Click += (_, _) => form.Text = "Save requested";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Images` | `ImageCollection` | Empty | Bitmap collection. |
| `ImageSize` | `SKSize` | 32 by 32 | Nominal image dimensions. |

## Methods and behavior

Populate Images with available assets. Configure ImageKey or ImageIndex on consuming controls such as Button or Label.

## Events

The component provides image data. Listen to events on the consuming controls and keep the list alive while they refer to it.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Use supported component tray metadata when available. Configure runtime data, handlers and resources in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide text or an accessible name for actions represented by icons. Share reusable assets and follow their ownership contract.

## Related guides and API

- [Control fundamentals](control-fundamentals.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ImageListPanel.cs)
- [API reference](<xref:ModernFormsNext.ImageList>)
