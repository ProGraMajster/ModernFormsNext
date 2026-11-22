# TimePicker

Edits a time of day without a calendar date. Use an explicit culture or format where a fixed convention is important.

Type `ModernFormsNext.TimePicker` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new TimePicker { Time = new TimeOnly(9, 0) });
```

## Configuration example

```csharp
control.MinimumTime = new TimeOnly(8, 0);
control.MaximumTime = new TimeOnly(18, 0);
control.StepSeconds = 900;
```

## Interaction example

```csharp
control.TimeChanged += (_, _) => form.Text = control.Text;
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `MinimumTime` | `TimeOnly` | 00 | 00:Beginning of the selectable range. |
| `MaximumTime` | `TimeOnly` | TimeOnly.MaxValue | End of the range. |
| `StepSeconds` | `int` | 60 | Step used by interactive increments. |

## Methods and behavior

FocusEditor() requests focus for the internal text field. Initialize the range before restoring user input that might exceed the configured bounds; out-of-range assignment throws.

## Events

TimeChanged reports changes to Time, including programmatic changes when the value actually differs.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Set the field name and test locale-specific text entry on the intended runtime.

## Related guides and API

- [Search and time](../search-and-time.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/SearchTimePanel.cs)
- [API reference](<xref:ModernFormsNext.TimePicker>)
