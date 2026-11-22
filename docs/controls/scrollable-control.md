# ScrollableControl

Provides an automatic scrolling viewport for child controls. Use it directly for a basic viewport or as a base for a specialized scrolling container.

Type `ModernFormsNext.ScrollableControl` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new ScrollableControl());
var child = control.Controls.Add(new Button { Text = "Continue", Top = 400 });
```

## Configuration example

```csharp
control.AutoScroll = true;
```

## Interaction example

```csharp
child.Click += (_, _) => control.ScrollControlIntoView(child);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `AutoScroll` | `bool` | false | Enables scrollbars for overflowing content. |
| `HorizontalScrollProperties` | `ScrollProperties` | Owned properties | Tracks the horizontal range and offset. |
| `VerticalScrollProperties` | `ScrollProperties` | Owned properties | Tracks the vertical range and offset. |

## Methods and behavior

ScrollControlIntoView(child) reveals a descendant through eligible nested scroll hosts. Call it after attachment and layout.

## Events

Scroll reports axis and old/new values for movement, including programmatic and native input paths.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Expose meaningful focus targets inside the viewport. UI Automation and Android use the canonical scroll/reveal semantics.

## Related guides and API

- [Accessibility/scroll viewports](../accessibility/scroll-viewports.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ScrollableControlPanel.cs)
- [API reference](<xref:ModernFormsNext.ScrollableControl>)
