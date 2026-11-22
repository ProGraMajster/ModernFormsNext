# RefreshView

Hosts scrollable content with pull progress, a threshold and a refresh state. Code and command sources can request the same action.

Type `ModernFormsNext.RefreshView` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`ScrollableControl`](scrollable-control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new RefreshView());
control.Controls.Add(new Label { Text = "Recent activity" });
```

## Configuration example

```csharp
control.AutoScroll = true;
control.PullThreshold = 72;
```

## Interaction example

```csharp
control.RefreshRequested += (_, _) => { form.Text = "Activity refreshed"; };
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `IsRefreshing` | `bool` | false | Reports whether the assigned AsyncCommand is executing. |
| `Command` | `ICommand` | null | Optional routed refresh command. |
| `PullProgress` | `Double` | 0 | Current normalized gesture progress. |

## Methods and behavior

RequestRefresh() requests the normal refresh action. CancelRefresh() requests cancellation when the active command permits it.

## Events

RefreshRequested reports accepted requests. RefreshStateChanged and PullProgressChanged expose busy and gesture changes.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The standard Designer previews available metadata and serializable appearance. Supply runtime data, templates, delegates and native integrations in code.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Expose a visible refresh action for keyboard users and keep the busy state tied to the actual operation lifetime.

## Related guides and API

- [Collection utilities](../collection-utilities.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/CollectionUtilitiesPanel.cs)
- [API reference](<xref:ModernFormsNext.RefreshView>)
