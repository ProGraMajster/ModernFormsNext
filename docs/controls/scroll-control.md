# ScrollControl

Provides internal scrollbar support for a control that draws a document or editor viewport. Use ScrollableControl to scroll a collection of ordinary child controls.

Type `ModernFormsNext.ScrollControl` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new ScrollControl());
```

## Configuration example

```csharp
control.ScrollBars = ScrollBars.Vertical;
```

## Interaction example

```csharp
control.VerticalScrollBar.ValueChanged += (_, _) => form.Text = $"Offset {control.VerticalScrollBar.Value}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `ScrollBars` | `ScrollBars` | None on ScrollControl | Determines which scrollbars are available. |
| `VerticalScrollBar` | `VerticalScrollBar` | Owned scrollbar | Provides the vertical range and value. |

## Methods and behavior

Derive a specialized viewport to set the range from measured content. Use the scrollbar properties to inspect the effective offset.

## Events

Observe ValueChanged on the owned scrollbar to track vertical movement. Control sizing events remain inherited.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Publish scroll and reveal semantics when extending this base for a custom viewport.

## Related guides and API

- [Accessibility/scroll viewports](../accessibility/scroll-viewports.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [API reference](<xref:ModernFormsNext.ScrollControl>)
