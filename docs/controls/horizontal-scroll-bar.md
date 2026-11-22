# HorizontalScrollBar

Exposes a horizontal integer scrolling range for a custom viewport. A ScrollableControl already supplies its own scrolling infrastructure.

Type `ModernFormsNext.HorizontalScrollBar` in the `ModernFormsNext` assembly and NuGet package.
Inherits `ScrollBar`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new HorizontalScrollBar());
```

## Configuration example

```csharp
control.Maximum = 500;
control.LargeChange = 100;
```

## Interaction example

```csharp
control.ValueChanged += (_, _) => form.Text = $"Horizontal offset {control.Value}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Minimum` | `int` | 0 | First range value. |
| `Maximum` | `int` | 100 | Highest configured range value. |
| `SmallChange` | `int` | 1 | Increment for a small movement. |
| `Value` | `int` | 0 | Current clamped value. |

## Methods and behavior

Set the range and LargeChange using units that match the viewport content. Assign Value to request a position.

## Events

ValueChanged observes the effective clamped value. Use Scroll to inspect the associated movement event.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide an accessible name that identifies the region this bar scrolls.

## Related guides and API

- [Accessibility/scroll viewports](../accessibility/scroll-viewports.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ScrollBarPanel.cs)
- [API reference](<xref:ModernFormsNext.HorizontalScrollBar>)
