# StatusBar

Shows concise read-only information at the bottom of its parent. Use it for persistent short status such as connection or save state.

Type `ModernFormsNext.StatusBar` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new StatusBar { Text = "Ready" });
```

## Configuration example

```csharp
control.Text = "Connected";
```

## Interaction example

```csharp
control.TextChanged += (_, _) => System.Diagnostics.Debug.WriteLine(control.Text);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Dock` | `DockStyle` | Bottom | Default layout position. |
| `Text` | `string` | Empty | Current displayed message. |

## Methods and behavior

Assign Text to update the surface. Use a separate controllable or focusable element when the message requires an action.

## Events

Inherited TextChanged observes programmatic updates. It is an application event; test assistive announcement separately if the status is critical.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Use short, descriptive status text and a second accessible route to important failures.

## Related guides and API

- [Control fundamentals](control-fundamentals.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/StatusBarPanel.cs)
- [API reference](<xref:ModernFormsNext.StatusBar>)
