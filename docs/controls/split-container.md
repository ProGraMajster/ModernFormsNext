# SplitContainer

Hosts two managed panels separated by a movable splitter. Put content in Panel1 or Panel2 rather than positioning it across the divider.

Type `ModernFormsNext.SplitContainer` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new SplitContainer());
control.Panel1.Controls.Add(new Label { Text = "Files" });
```

## Configuration example

```csharp
control.SplitterDistance = 160;
control.Panel2.Controls.Add(new Label { Text = "Preview" });
```

## Interaction example

```csharp
control.Panel1.Resize += (_, _) => form.Text = $"Pane width {control.Panel1.Width}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Panel1` | `Panel` | Owned Panel | First pane. |
| `Panel2` | `Panel` | Owned Panel | Second pane. |
| `Orientation` | `Orientation` | Horizontal | Selects the arrangement of the panes. |

## Methods and behavior

Set the pane minimum sizes before restoring a splitter position. Keep the position in logical layout units.

## Events

Observe a panel's Resize event to track geometry after dragging or programmatic resizing.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide distinct pane landmarks and test focus across both panes. Offer another way to resize when pointer interaction is unavailable.

## Related guides and API

- [Control fundamentals](control-fundamentals.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/SplitContainerPanel.cs)
- [API reference](<xref:ModernFormsNext.SplitContainer>)
