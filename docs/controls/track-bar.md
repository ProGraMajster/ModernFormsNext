# TrackBar

Selects a bounded integer using a movable slider. Use it for a relative setting such as zoom or volume.

Type `ModernFormsNext.TrackBar` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new TrackBar { AccessibleName = "Zoom" });
```

## Configuration example

```csharp
control.Maximum = 20;
control.TickFrequency = 5;
control.SnapToTicks = true;
```

## Interaction example

```csharp
control.ValueChanged += (_, _) => form.Text = $"Zoom {control.Value}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Minimum` | `int` | 0 | Smallest value. |
| `Maximum` | `int` | 10 | Largest value for a new control. |
| `Value` | `int` | 0 | Current position. |
| `SnapToTicks` | `bool` | false | Optionally discretizes movement to the tick interval. |

## Methods and behavior

Set Minimum and Maximum before Value. Use SmallChange and LargeChange to configure input increments.

## Events

ValueChanged observes value changes. Scroll describes input movement and carries scroll event data.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keep its name and associated value legible when the tick marks are sparse.

## Related guides and API

- [Accessibility/semantic model](../accessibility/semantic-model.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/TrackBarPanel.cs)
- [API reference](<xref:ModernFormsNext.TrackBar>)
