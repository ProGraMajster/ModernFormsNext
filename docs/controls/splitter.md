# Splitter

Supplies a draggable divider when the surrounding application manages its own pane sizes. SplitContainer wires two panes together for the common case.

Type `ModernFormsNext.Splitter` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Splitter());
```

## Configuration example

```csharp
control.SplitterWidth = 6;
```

## Interaction example

```csharp
control.Drag += (_, _) => form.Text = "Adjusting panes";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `SplitterWidth` | `int` | 0 | Initial configured thickness of the divider. |
| `Orientation` | `Orientation` | Horizontal | Controls drag direction. |

## Methods and behavior

Use the Drag event to apply the movement to your panes. Configure Orientation to match your layout.

## Events

Drag is raised during pointer movement. Impose minimum pane sizes in the handler or use a SplitContainer.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide keyboard-accessible resize commands where the divider is essential to operation.

## Related guides and API

- [Control fundamentals](control-fundamentals.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/SplitContainerPanel.cs)
- [API reference](<xref:ModernFormsNext.Splitter>)
