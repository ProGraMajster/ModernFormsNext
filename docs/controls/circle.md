# Circle

Draws a circular shape centered in the available bounds. Use it for markers and visual badges where stretching into an ellipse is undesirable.

Type `ModernFormsNext.Circle` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Ellipse`](ellipse.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Circle { Size = new Size(64, 64) });
```

## Configuration example

```csharp
control.Fill = new SolidColorBrush(Color.SeaGreen);
```

## Interaction example

```csharp
control.Resize += (_, _) => System.Diagnostics.Debug.WriteLine(control.Size);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Fill` | `Brush` | null | Interior brush. |
| `Stroke` | `Brush` | null | Outline brush. |
| `StrokeThickness` | `float` | 1 | Line width in logical units. |

## Methods and behavior

Set the extent through Size. Configure Stroke and StrokeThickness if the marker needs a visible outline.

## Events

Use inherited layout or input events. The control has no independent value or action event.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Do not use a colored circle as the only indication of a state.

## Related guides and API

- [Shapes and vector geometry](../shapes-and-vector-geometry.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [API reference](<xref:ModernFormsNext.Circle>)
