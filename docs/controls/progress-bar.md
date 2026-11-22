# ProgressBar

Displays determinate progress in a bounded range. Update it from completed work measured by your application.

Type `ModernFormsNext.ProgressBar` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new ProgressBar());
```

## Configuration example

```csharp
control.Maximum = 4;
control.Step = 1;
```

## Interaction example

```csharp
var next = form.Controls.Add(new Button { Text = "Complete item" });
next.Click += (_, _) => control.Increment(control.Step);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Minimum` | `int` | 0 | Beginning of the range. |
| `Maximum` | `int` | 100 | End of the range. |
| `Value` | `int` | 0 | Current progress. |
| `Step` | `int` | 10 | Increment for step-based work. |

## Methods and behavior

Increment(delta) advances the value within the range. Marshal updates to the UI thread when work completes elsewhere.

## Events

The application owns progress notifications and moves the bar in response to those notifications. There is no ProgressBar-specific completion event.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide a readable status description, particularly for work that can complete or fail without focus changing.

## Related guides and API

- [Control fundamentals](control-fundamentals.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ProgressBarPanel.cs)
- [API reference](<xref:ModernFormsNext.ProgressBar>)
