# Panel

Groups controls on a managed surface. Dock and anchor its children, or enable scrolling when content can exceed the viewport.

Type `ModernFormsNext.Panel` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`ScrollableControl`](scrollable-control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Panel());
control.Controls.Add(new Label { Text = "Summary" });
```

## Configuration example

```csharp
control.Dock = DockStyle.Fill;
control.AutoScroll = true;
```

## Interaction example

```csharp
control.Scroll += (_, args) => form.Text = $"Scroll position {args.NewValue}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `AutoScroll` | `bool` | false | Enables scrolling for overflowing children. |
| `Dock` | `DockStyle` | None | Controls how the panel occupies its parent's space. |

## Methods and behavior

Use ScrollControlIntoView(child) for an eligible descendant. Use an explicit layout engine if you need wrapping or cells.

## Events

Scroll reports movement on either axis. Layout and child events are inherited from Control.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keep interactive children discoverable through their individual names and tab order.

## Related guides and API

- [Control fundamentals](control-fundamentals.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/PanelPanel.cs)
- [API reference](<xref:ModernFormsNext.Panel>)
