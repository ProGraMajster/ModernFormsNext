# ContentPage

Wraps one content control in a Page lifecycle. Use it as the unit of navigation rather than embedding Form windows in a navigation host.

Type `ModernFormsNext.ContentPage` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Page`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = new ContentPage();
var button = new Button { Text = "Done" };
```

## Configuration example

```csharp
control.Content = button;
form.Controls.Add(control);
```

## Interaction example

```csharp
button.Click += (_, _) => form.Text = "Page complete";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Content` | `Control` | null | Content belonging to the page. |
| `Dock` | `DockStyle` | None before attaching | Controls how the page fills its host when composed. |

## Methods and behavior

Set Content to replace the owned visual content. Use PageHost or a navigation host to manage activation and reveal.

## Events

The page participates in the Page lifecycle. Observe events on the content control for user interaction.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The Designer supports bounded page metadata and design-time placeholders. Runtime page factories, commands, lifecycle and data remain code-first.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Place focus on a meaningful child when showing a page. Give the host or navigation header a clear title.

## Related guides and API

- [Pages and navigation](../pages-and-navigation.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/PageNavigationPanel.cs)
- [API reference](<xref:ModernFormsNext.ContentPage>)
