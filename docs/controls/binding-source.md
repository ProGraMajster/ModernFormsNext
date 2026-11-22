# BindingSource

Mediates data binding and the current record between bound controls. Use it with BindingNavigator for record navigation and editing.

Type `ModernFormsNext.DataBinding.BindingSource` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Component`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = new BindingSource();
form.Disposed += (_, _) => control.Dispose();
```

## Configuration example

```csharp
control.DataSource = new System.ComponentModel.BindingList<string> { "Active", "Archived" };
```

## Interaction example

```csharp
control.PositionChanged += (_, _) => form.Text = control.Current?.ToString() ?? "No record";
control.MoveLast();
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `DataSource` | `object` | null | Underlying data or list. |
| `Current` | `object` | null when empty | Current record. |
| `Position` | `int` | -1 when empty | Current record index. |

## Methods and behavior

MoveFirst(), MoveNext() and MoveLast() update currency. EndEdit and CancelEdit use the source's editing contract. Call ResetBindings when the underlying list cannot notify.

## Events

PositionChanged observes index changes. ListChanged carries structural notifications, and CurrentChanged observes changes to currency.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Use supported component tray metadata when available. Configure runtime data, handlers and resources in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Expose record names and action state through the bound controls. Prefer observable sources to repeated resets on large lists.

## Related guides and API

- [Data binding](../data-binding.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [API reference](<xref:ModernFormsNext.DataBinding.BindingSource>)
