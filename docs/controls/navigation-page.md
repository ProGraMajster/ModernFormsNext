# NavigationPage

Owns a stack of pages and an optional navigation header. Use asynchronous stack operations so ownership and lifecycle are serialized.

Type `ModernFormsNext.NavigationPage` in the `ModernFormsNext` assembly and NuGet package.
Inherits `Page`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new NavigationPage(new ContentPage { Content = new Label { Text = "Home" } }));
```

## Configuration example

```csharp
control.NavigationBarTitle = "Projects";
```

## Interaction example

```csharp
control.Navigated += (_, _) => form.Text = "Page changed";
await control.PushAsync(new ContentPage { Content = new Label { Text = "Details" } });
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `CurrentPage` | `Page` | null without a root | Current visible page. |
| `CanGoBack` | `bool` | false without a pushed page | Whether a back request can pop. |
| `NavigationBarVisible` | `bool` | true | Displays the optional header. |

## Methods and behavior

PushAsync(page) places a page on the stack. PopAsync() goes back when possible. PopToRootAsync() unwinds to the first page.

## Events

Navigating can cancel a stack transition. Navigated and StackChanged observe successful committed changes.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

The Designer supports bounded page metadata and design-time placeholders. Runtime page factories, commands, lifecycle and data remain code-first.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Use BackCommand or the header's Back button. Give each destination a useful title and focus target.

## Related guides and API

- [Pages and navigation](../pages-and-navigation.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/PageNavigationPanel.cs)
- [API reference](<xref:ModernFormsNext.NavigationPage>)
