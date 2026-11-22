# Path

Displays vector geometry composed of segments, figures, combined shapes or parsed path data. Use it when a built-in shape cannot express the contour.

Type `ModernFormsNext.Path` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Shape`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new ModernFormsNext.Path());
```

## Configuration example

```csharp
control.Data = PathGeometry.Parse("M10 10 L80 10 L40 50 Z");
control.Fill = new SolidColorBrush(Color.Gold);
```

## Interaction example

```csharp
control.Resize += (_, _) => System.Diagnostics.Debug.WriteLine(control.Bounds);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Data` | `Geometry` | null | Geometry being drawn. |
| `Fill` | `Brush` | null | Optional interior brush. |
| `Stroke` | `Brush` | null | Optional outline brush. |

## Methods and behavior

PathGeometry.Parse accepts invariant path data and rejects malformed input. Set Data from a reusable geometry when configuration is structured.

## Events

Geometry changes invalidate the cache; use inherited layout events if the position follows the surrounding controls.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Pair informative icons with a named action. The Designer can edit supported declarative Path data.

## Related guides and API

- [Shapes and vector geometry](../shapes-and-vector-geometry.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ShapesPanel.cs)
- [API reference](<xref:ModernFormsNext.Path>)
