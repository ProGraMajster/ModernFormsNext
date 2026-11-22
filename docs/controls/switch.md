# Switch

Displays a slider-style switch for a setting. Supports Boolean and three-state presentation, optional dragging and independent state brushes.

Type `ModernFormsNext.Switch` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Switch { AccessibleName = "Enable syncing" });
```

## Configuration example

```csharp
control.Animate = false;
```

## Interaction example

```csharp
control.ValueChanged += (_, _) => form.Text = control.Value.ToString();
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `ThreeState` | `bool` | false | Enables the extra state. |
| `AutoToggle` | `bool` | true | Changes the value on activation. |
| `IsToggled` | `bool` | false | Boolean view of the active state. |

## Methods and behavior

Toggle() requests the normal control transition. Set Value directly when restoring persisted state.

## Events

ValueChanged tracks the full value. Use Toggled for changes in the Boolean projection.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Supply a name for an icon-only switch and verify activation with both keyboard and pointer.

## Related guides and API

- [Commands](../commands.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/SwitchPanel.cs)
- [API reference](<xref:ModernFormsNext.Switch>)
