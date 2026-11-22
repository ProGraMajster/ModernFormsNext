# FlyoutPage

Composes a flyout page and a detail page. The display policy chooses overlay or split behavior for the available space.

Type `ModernFormsNext.FlyoutPage` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Page`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new FlyoutPage());
```

## Configuration example

```csharp
control.Flyout = new ContentPage { Content = new Label { Text = "Sections" } };
control.Detail = new ContentPage { Content = new Label { Text = "Overview" } };
```

## Interaction example

```csharp
control.FlyoutOpenChanged += (_, _) => form.Text = control.IsFlyoutOpen ? "Menu open" : "Menu closed";
await control.SetFlyoutOpenAsync(true);
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Flyout` | `Page` | null | Secondary navigation/content page. |
| `Detail` | `Page` | null | Main page. |
| `EffectiveDisplayMode` | `FlyoutDisplayMode` | Derived from host width and policy | Actual layout mode. |

## Methods and behavior

TrySetFlyout and TrySetDetail change the hosted pages. SetFlyoutOpenAsync requests open/close with the configured transition.

## Events

FlyoutOpenChanged tracks open state. EffectiveDisplayModeChanged tracks a change between responsive presentations.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The Designer supports bounded page metadata and design-time placeholders. Runtime page factories, commands, lifecycle and data remain code-first.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Use the exposed toggle or close command for keyboard access. Test the focus path in overlay and split modes.

## Related guides and API

- [Page composition](../page-composition.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/PageCompositionPanel.cs)
- [API reference](<xref:ModernFormsNext.FlyoutPage>)
