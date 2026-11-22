# VerticalScrollBar

Exposes a vertical integer scrolling range for a custom viewport. Keep its viewport step and content position synchronized.

Type `ModernFormsNext.VerticalScrollBar` in the `ModernFormsNext` assembly and NuGet package.
Inherits `ScrollBar`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new VerticalScrollBar());
```

## Configuration example

```csharp
control.Maximum = 300;
control.LargeChange = 60;
```

## Interaction example

```csharp
control.ValueChanged += (_, _) => form.Text = $"Vertical offset {control.Value}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Minimum` | `int` | 0 | First range value. |
| `Maximum` | `int` | 100 | Highest configured range value. |
| `LargeChange` | `int` | 10 | Step for a page-style movement. |
| `Value` | `int` | 0 | Current clamped value. |

## Methods and behavior

Set the range before restoring Value. Use SmallChange for line movement and LargeChange for viewport movement.

## Events

ValueChanged observes the effective value. Scroll exposes movement data for the bar's orientation.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Name the bar for its viewport and ensure content also supports keyboard reveal.

## Related guides and API

- [Accessibility/scroll viewports](../accessibility/scroll-viewports.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ScrollBarPanel.cs)
- [API reference](<xref:ModernFormsNext.VerticalScrollBar>)
