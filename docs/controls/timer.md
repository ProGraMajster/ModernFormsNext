# Timer

Runs periodic UI-dispatcher callbacks. Use it to refresh a small visual status without moving control access to a worker thread.

Type `ModernFormsNext.Timer` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Component`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = new ModernFormsNext.Timer();
form.Disposed += (_, _) => control.Dispose();
```

## Configuration example

```csharp
control.Interval = 1000;
```

## Interaction example

```csharp
control.Tick += (_, _) => form.Text = DateTime.Now.ToShortTimeString();
control.Start();
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Enabled` | `bool` | false | Whether the timer is running. |
| `Interval` | `int` | 100 | Milliseconds between requested ticks. |

## Methods and behavior

Start() enables ticking. Stop() stops it. Keep it owned by the form or component container for the duration of its use.

## Events

Tick runs through the UI timer. It is unsuitable for exact scheduling and delayed ticks should not block the dispatcher.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Use supported component tray metadata when available. Configure runtime data, handlers and resources in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Use measured elapsed time when needed. Do not flash or overwrite important accessible status with frequent updates.

## Related guides and API

- [Control fundamentals](control-fundamentals.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [API reference](<xref:ModernFormsNext.Timer>)
