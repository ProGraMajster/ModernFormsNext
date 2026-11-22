# CollectionView

Binds a data sequence to reusable control containers. The viewport creates the controls it needs and can recycle detached containers as it scrolls.

Type `ModernFormsNext.CollectionView` in the `ModernFormsNext` assembly and NuGet package.
Inherits `ItemsControl`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new CollectionView());
```

## Configuration example

```csharp
control.ItemsSource = new[] { "Alpha", "Beta", "Gamma" };
control.SelectedIndex = 1;
```

## Interaction example

```csharp
control.ItemInvoked += (_, args) => form.Text = $"Item {args.Index}: {args.Item}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `ItemsSource` | `IList` | null | Provides the items. |
| `ItemTemplate` | `IItemContainerFactory<Control>` | null | Defines container creation and binding when supplied. |
| `SelectedItem` | `object` | null | Current logical selection. |

## Methods and behavior

RefreshItems() re-reads a source that does not send collection-change notifications. Use the virtualization snapshot APIs for diagnostics and the template guide for reuse.

## Events

ItemInvoked reports the data item and index. The shared selection APIs keep the logical selection independent from realized controls.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The standard Designer previews available metadata and serializable appearance. Supply runtime data, templates, delegates and native integrations in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Expose meaningful item labels and keep template reset behavior compatible with recycling. Offscreen rows can have logical semantics without a persistent child Control.

## Large observable sources

Keep an indexed observable source on the UI dispatcher. Selection belongs to
the source occurrence, independently of the realized row. When changing the
same instance of a plain list, call `RefreshItems()` to notify the control.

```csharp
var entries = new System.Collections.ObjectModel.ObservableCollection<string>();
for (var index = 0; index < 10_000; index++)
    entries.Add($"Entry {index}");
control.CacheItemCount = 2;
control.ItemSize = new Size(240, 32);
control.ItemsSource = entries;
control.SelectedIndex = 9000;
control.ScrollToItem(9000);
```

A real application can assign an `IItemContainerFactory<Control>` to
`ItemTemplate`. Create an unparented row, bind the current occurrence in
`PrepareContainer`, and clear its subscriptions and transient state in
`ClearContainer`. The host owns returned containers. The detailed guide
shows a full factory and selection behavior across indexed source mutations.

## Related guides and API

- [Collection view](../collection-view.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/CollectionViewPanel.cs)
- [API reference](<xref:ModernFormsNext.CollectionView>)
