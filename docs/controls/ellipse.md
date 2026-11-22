# Ellipse

Draws an ellipse inside its logical bounds using shared shape fill and stroke. Use Circle when the visual outline should stay circular.

Type `ModernFormsNext.Ellipse` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Shape`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Ellipse { Size = new Size(140, 80) });
```

## Configuration example

```csharp
control.Fill = new SolidColorBrush(Color.CornflowerBlue);
control.Stroke = new SolidColorBrush(Color.Navy);
```

## Interaction example

```csharp
control.Resize += (_, _) => System.Diagnostics.Debug.WriteLine(control.Bounds);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Fill` | `Brush` | null | Optional interior brush. |
| `Stroke` | `Brush` | null | Optional outline brush. |
| `StrokeThickness` | `float` | 1 | Line width in logical units. |

## Methods and behavior

Set the inherited Size and shape brushes to control the result. A filled shape participates in geometry-aware hit testing.

## Events

There is no ellipse-specific event. Use inherited Resize or pointer events when the shape is part of an interactive surface.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Treat decorative geometry as decoration. Pair meaningful information with text or an accessible control.

## Related guides and API

- [Shapes and vector geometry](../shapes-and-vector-geometry.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ShapesPanel.cs)
- [API reference](<xref:ModernFormsNext.Ellipse>)
