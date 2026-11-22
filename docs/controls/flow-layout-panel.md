# FlowLayoutPanel

Lays out children in a row or column and can wrap them. Flow breaks and inherited right-to-left behavior are available without absolute positioning.

Type `ModernFormsNext.FlowLayoutPanel` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Panel`](panel.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new FlowLayoutPanel());
var first = control.Controls.Add(new Button { Text = "Export" });
```

## Configuration example

```csharp
control.SetFlowBreak(first, true);
control.Controls.Add(new Button { Text = "Options" });
```

## Interaction example

```csharp
control.Layout += (_, _) => System.Diagnostics.Debug.WriteLine(control.DisplayRectangle);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `FlowDirection` | `FlowDirection` | LeftToRight | Direction before inherited RTL is applied. |
| `WrapContents` | `bool` | true | Starts another row or column when needed. |

## Methods and behavior

SetFlowBreak(child, true) starts a new line after the child. GetFlowBreak reads that setting. Both operate on layout metadata for that child.

## Events

Use inherited Layout to observe rearrangement. Avoid changing child sizes unconditionally from the handler.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Visual order can be mirrored independently of logical child order. Check tab order when setting FlowDirection or RightToLeft.

## Related guides and API

- [Right to left layout](../right-to-left-layout.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/FlowLayoutPanelPanel.cs)
- [API reference](<xref:ModernFormsNext.FlowLayoutPanel>)
