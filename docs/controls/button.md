# Button

Runs an action from a click, keyboard activation, or a routed command. Use it for an explicit commit or navigation action.

Type `ModernFormsNext.Button` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Button { Text = "Save" });
```

## Configuration example

```csharp
control.AutoSize = true;
```

## Interaction example

```csharp
control.Click += (_, _) => form.Text = "Saved";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `AutoEllipsis` | `bool` | false | Shortens overflowing text when enabled. |
| `Command` | `ICommand` | null | Supplies the action and its availability. |
| `DialogResult` | `DialogResult` | None | Can close an owning modal form with a result. |

## Methods and behavior

PerformClick() requests programmatic activation through the button, including normal command handling.

## Events

Click reports activation. A command can change the enabled state, so observe command eligibility when debugging a disabled button.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Set text or an accessible name even when the button displays only an image.

## Related guides and API

- [Commands](../commands.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ButtonPanel.cs)
- [API reference](<xref:ModernFormsNext.Button>)
