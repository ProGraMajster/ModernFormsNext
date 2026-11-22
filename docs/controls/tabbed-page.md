# TabbedPage

Hosts several Page instances with one active page and their shared lifecycle. Use TabControl instead for closable generic content tabs.

Type `ModernFormsNext.TabbedPage` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Page`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new TabbedPage());
```

## Configuration example

```csharp
control.Pages.Add(new ContentPage { Text = "Overview" });
control.Pages.Add(new ContentPage { Text = "Activity" });
```

## Interaction example

```csharp
control.SelectedPageChanged += (_, _) => form.Text = control.SelectedPage?.Text ?? "No page";
control.SelectedIndex = 1;
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Pages` | `PageCollection` | Empty | Owned page collection. |
| `SelectedIndex` | `int` | -1 when empty | Controls the visible page. |
| `SelectedPage` | `Page` | null when empty | Currently active page. |

## Methods and behavior

TrySelectPage attempts a selection through the normal cancellable path. Add only pages available for this host to own.

## Events

SelectedPageChanging may cancel selection. SelectedPageChanged and PagesChanged observe committed changes.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The Designer supports bounded page metadata and design-time placeholders. Runtime page factories, commands, lifecycle and data remain code-first.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keep titles distinct and test focus and tab traversal when switching pages.

## Related guides and API

- [Page composition](../page-composition.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/PageCompositionPanel.cs)
- [API reference](<xref:ModernFormsNext.TabbedPage>)
