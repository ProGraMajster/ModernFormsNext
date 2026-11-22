# Polyline

Draws an open stroke through a sequence of points. Use Polygon when the contour should close and fill automatically.

Type `ModernFormsNext.Polyline` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Shape`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Polyline());
```

## Configuration example

```csharp
control.Points.Add(new PointF(8, 32));
control.Points.Add(new PointF(28, 12));
control.Points.Add(new PointF(68, 24));
control.Stroke = new SolidColorBrush(Color.RoyalBlue);
```

## Interaction example

```csharp
control.Resize += (_, _) => System.Diagnostics.Debug.WriteLine(control.Bounds);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Points` | `PointCollection` | Empty | Observable path vertices. |
| `Fill` | `Brush` | null | Ignored for this open contour. |
| `StrokeThickness` | `float` | 1 | Inherited width in logical units. |

## Methods and behavior

Replace or mutate Points on the UI thread. Keep updates bounded for frequent chart-style changes because point edits invalidate geometry.

## Events

Use inherited control events. A change to the points is a geometry mutation and does not represent user activation.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide accessible text describing any data whose trend is shown only as a stroke.

## Related guides and API

- [Shapes and vector geometry](../shapes-and-vector-geometry.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ShapesPanel.cs)
- [API reference](<xref:ModernFormsNext.Polyline>)
