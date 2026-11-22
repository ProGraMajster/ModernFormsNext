# CarouselView

Shows one data page at a time and retains a configurable number of nearby item containers. Use it for swipeable cards or discrete previews.

Type `ModernFormsNext.CarouselView` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new CarouselView { ItemsSource = new[] { "Overview", "Details" } });
```

## Configuration example

```csharp
control.CacheItemCount = 1;
```

## Interaction example

```csharp
control.SelectedIndexChanged += (_, _) => form.Text = control.SelectedItem?.ToString() ?? "No page";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `ItemsSource` | `IList` | null | Items displayed as pages. |
| `SelectedIndex` | `int` | -1 | Selected page when a source is present. |
| `ItemTemplate` | `IItemContainerFactory<Control>` | null | Creates and binds page containers when provided. |

## Methods and behavior

MoveNext() and MovePrevious() request a neighboring page. GetItemContainer(index) can return null for an unrealized item.

## Events

SelectedIndexChanged observes the effective page. ItemInvoked reports interaction with a displayed item.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The standard Designer previews available metadata and serializable appearance. Supply runtime data, templates, delegates and native integrations in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide a keyboard/button path in addition to swipe gestures for important content. Keep the cache small for costly templates.

## Related guides and API

- [Collection utilities](../collection-utilities.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/CollectionUtilitiesPanel.cs)
- [API reference](<xref:ModernFormsNext.CarouselView>)
