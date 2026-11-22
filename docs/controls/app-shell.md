# AppShell

Resolves routes to Pages and maintains navigation and lifecycle state. Use it when destinations need route parameters or state capture and restoration.

Type `ModernFormsNext.AppShell` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Page`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new AppShell());
```

## Configuration example

```csharp
control.Routes.Register("orders", _ => new ContentPage { Content = new Label { Text = "Orders" } });
```

## Interaction example

```csharp
control.Navigated += (_, _) => form.Text = "Route changed";
await control.GoToAsync("orders");
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Routes` | `RouteTable` | Empty | Route table owned by the shell. |
| `CurrentPage` | `Page` | null | Active destination. |
| `CanGoBack` | `bool` | false | Whether the stack supports going back. |

## Methods and behavior

GoToAsync resolves a route and activates the destination. GoBackAsync requests back navigation. CaptureState and RestoreStateAsync integrate with app state storage.

## Events

Navigated observes successful navigation. CurrentRouteChanged tracks the effective route. Await requests on the UI dispatcher.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The Designer supports bounded page metadata and design-time placeholders. Runtime page factories, commands, lifecycle and data remain code-first.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keep route factories consistent about page ownership. NavigationBar exposes the Back command and title to users.

## Related guides and API

- [App shell routing](../app-shell-routing.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/AppShellPanel.cs)
- [API reference](<xref:ModernFormsNext.AppShell>)
