# PageHost

Displays one Page and owns its placement in the visual tree. Use it for a replaceable region when a navigation stack is unnecessary.

Type `ModernFormsNext.PageHost` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new PageHost());
```

## Configuration example

```csharp
control.Dock = DockStyle.Fill;
control.TrySetPage(new ContentPage { Content = new Label { Text = "Overview" } });
```

## Interaction example

```csharp
control.ControlAdded += (_, _) => form.Text = "Overview loaded";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Page` | `Page` | null | Currently hosted page. |
| `Dock` | `DockStyle` | None | Can fill a layout region when configured. |

## Methods and behavior

TrySetPage(page) attempts to host the page and reports whether it succeeded. Use a page without another active host.

## Events

Observe the hosted page's lifecycle for activation and the control tree events for attachment.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The Designer supports bounded page metadata and design-time placeholders. Runtime page factories, commands, lifecycle and data remain code-first.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Preserve focus when exchanging the active region. The hosted page provides its own named interactive descendants.

## Related guides and API

- [Pages and navigation](../pages-and-navigation.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/PageNavigationPanel.cs)
- [API reference](<xref:ModernFormsNext.PageHost>)
