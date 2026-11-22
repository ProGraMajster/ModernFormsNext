# Control

Provides the common control tree, bounds, styling and input contract. Instantiate it as a minimal surface, or derive a reusable control when behavior belongs to your component.

Type `ModernFormsNext.Control` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Component`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Control());
```

## Configuration example

```csharp
control.BackColor = SKColors.AliceBlue;
control.Size = new Size(120, 40);
```

## Interaction example

```csharp
control.Resize += (_, _) => form.Text = control.Size.ToString();
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Dock` | `DockStyle` | None | How the control consumes its parent's layout area. |
| `Visible` | `bool` | Effective host/ancestor state | Combines local visibility with host and ancestor visibility. |
| `TabStop` | `bool` | true | Allows tab traversal when the control can receive focus. |

## Methods and behavior

Use Controls.Add to attach children. Batch sibling changes with SuspendLayout and ResumeLayout. Invalidate schedules repaint, separate from performing layout.

## Events

Layout observes layout passes. ParentChanged is raised once per actual reassignment. Do not mutate the tree from a background thread.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

A generic container has no button or edit semantics. Use the appropriate role or a built-in control for actionable children.

## Related guides and API

- [Control fundamentals](control-fundamentals.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [API reference](<xref:ModernFormsNext.Control>)
