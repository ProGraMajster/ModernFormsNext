# ListView

Shows an item collection of icon/text rows or tiles. Existing applications can keep eager drawing or opt into shared virtualization for large data sets.

Type `ModernFormsNext.ListView` in the `ModernFormsNext` assembly and NuGet package.
Inherits `ItemsControl`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new ListView());
control.Items.Add(new ListViewItem { Text = "Item A" });
```

## Configuration example

```csharp
control.VirtualizationEnabled = true;
control.ItemSize = new Size(132, 40);
```

## Interaction example

```csharp
control.ItemDoubleClicked += (_, _) => form.Text = control.SelectedItem?.Text ?? "No selection";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `VirtualizationEnabled` | `bool` | false | Selects the opt-in realization path. |
| `ItemSize` | `Size` | 76 by 76 logical units | Controls item geometry. |
| `SelectedItem` | `ListViewItem` | null | Current item selection. |

## Methods and behavior

Inspect the inherited virtualization snapshot after layout when diagnosing realized ranges. Configure geometry through ItemsLayout and ItemSize.

## Events

ItemDoubleClicked reports item activation by double click. Retain item identity instead of indexing only the visible window.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The standard Designer previews available metadata and serializable appearance. Supply runtime data, templates, delegates and native integrations in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Verify logical selection and keyboard reveal when switching an existing application to virtual mode. Large-list guidance is in the virtualization guide.

## Programmatic reveal in virtual mode

Use logical indices to address the item collection, including rows outside
the current viewport. Inspect the snapshot after the next layout or scroll
to see the realized range and recycling counters.

```csharp
control.Dock = DockStyle.Fill;
for (var index = 0; index < 10_000; index++)
    control.Items.Add(new ListViewItem { Text = $"Record {index + 1}" });
control.SelectedItem = control.Items[9999];
control.ScrollToItem(9999);
var snapshot = control.GetVirtualizationSnapshot();
System.Diagnostics.Debug.WriteLine(snapshot.RealizedRange);
```

Changing selection alone does not guarantee scrolling. Use a separate reveal
request. A container from `GetItemContainer(index)` is owned by the host
and can later represent a different item. Do not keep one as a model key.

## Related guides and API

- [Virtualization](../virtualization.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/VirtualizationPanel.cs)
- [API reference](<xref:ModernFormsNext.ListView>)
