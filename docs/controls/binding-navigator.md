# BindingNavigator

Adds an editable record position, count and record actions over a BindingSource. It follows the source's currency and edit permissions.

Type `ModernFormsNext.DataBinding.BindingNavigator` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`FlowLayoutPanel`](flow-layout-panel.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var records = new BindingSource { DataSource = new System.ComponentModel.BindingList<string> { "Alpha", "Beta" } };
form.Disposed += (_, _) => records.Dispose();
var control = form.Controls.Add(new BindingNavigator(records));
```

## Configuration example

```csharp
control.CountItemFormat = "of {0}";
```

## Interaction example

```csharp
records.PositionChanged += (_, _) => form.Text = $"Record {records.Position + 1}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `BindingSource` | `BindingSource` | null | Record cursor controlled by the navigator. |
| `CountItemFormat` | `string` | of {0} | Formats the record count label. |

## Methods and behavior

RefreshItems() synchronizes control parts with the active source. BeginInit() and EndInit() bracket configuration when constructing replacement parts.

## Events

BindingSourceChanged observes a different source. Subscribe to PositionChanged on the BindingSource for record movement.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The standard Designer previews available metadata and serializable appearance. Supply runtime data, templates, delegates and native integrations in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Give replacement buttons clear action names. Dispose the BindingSource when its owner is finished with it.

## Related guides and API

- [Binding navigator](../binding-navigator.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/BindingNavigatorPanel.cs)
- [API reference](<xref:ModernFormsNext.DataBinding.BindingNavigator>)
