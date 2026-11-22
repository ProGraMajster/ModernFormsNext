# Line

Draws an open segment between points in local logical coordinates. Use it for dividers and annotations.

Type `ModernFormsNext.Line` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Shape`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Line());
```

## Configuration example

```csharp
control.EndPoint = new PointF(105, 28);
control.Stroke = new SolidColorBrush(Color.Navy);
control.StrokeThickness = 2;
```

## Interaction example

```csharp
control.Resize += (_, _) => System.Diagnostics.Debug.WriteLine(control.Bounds);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `StartPoint` | `PointF` | (8, 20) | Segment origin. |
| `EndPoint` | `PointF` | (112, 20) | Segment end. |
| `Fill` | `Brush` | null | Ignored on an open segment. |

## Methods and behavior

Assign the endpoints after deciding the local coordinate range. The visible outline is configured with Stroke.

## Events

Observe inherited layout events when the segment's geometry follows a resizable region.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Use it as decoration unless paired with a named interactive element.

## Related guides and API

- [Shapes and vector geometry](../shapes-and-vector-geometry.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ShapesPanel.cs)
- [API reference](<xref:ModernFormsNext.Line>)
