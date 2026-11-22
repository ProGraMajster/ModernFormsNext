# Polygon

Draws a closed shape by joining an ordered sequence of points. The last point automatically connects to the first.

Type `ModernFormsNext.Polygon` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Shape`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Polygon());
```

## Configuration example

```csharp
control.Points.Add(new PointF(8, 48));
control.Points.Add(new PointF(48, 8));
control.Points.Add(new PointF(88, 48));
control.Fill = new SolidColorBrush(Color.Gold);
```

## Interaction example

```csharp
control.Resize += (_, _) => System.Diagnostics.Debug.WriteLine(control.Bounds);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Points` | `PointCollection` | Empty | Observable list of vertices. |
| `FillRule` | `GeometryFillRule` | Winding | Determines how overlaps are filled. |

## Methods and behavior

Mutate Points or replace the collection to rebuild the contour. Use at least three non-collinear points for a filled region.

## Events

The points collection reports mutations used by rendering. The control's normal layout and input events are inherited.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Describe meaningful shapes elsewhere in the interface. Interactive geometry has separate hit-test and accessibility contracts.

## Related guides and API

- [Shapes and vector geometry](../shapes-and-vector-geometry.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ShapesPanel.cs)
- [API reference](<xref:ModernFormsNext.Polygon>)
