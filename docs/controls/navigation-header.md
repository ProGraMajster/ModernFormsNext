# NavigationHeader

Displays a title, Back action and configured toolbar for NavigationPage or AppShell. Access the host's instance, which manages its lifetime and Back command.

Type `ModernFormsNext.NavigationHeader` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var host = form.Controls.Add(new NavigationPage(new ContentPage()));
var control = host.NavigationBar;
```

## Configuration example

```csharp
host.NavigationBarTitle = "Orders";
control.TitleView = new Label { Text = "Open orders" };
```

## Interaction example

```csharp
control.BackButton.Click += (_, _) => form.Text = "Back requested";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `TitleLabel` | `Label` | Owned label | Built-in title element. |
| `BackButton` | `Button` | Owned button | Triggers the host's Back command. |
| `TitleView` | `Control` | null | Optional replacement title. |

## Methods and behavior

Configure header visibility, placement and padding through the host. The header instance has no public constructor.

## Events

Use the host's Navigating or Navigated events for navigation outcomes. BackButton.Click observes the button interaction itself.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Set supported navigation header metadata on the owning NavigationPage or AppShell. Obtain the generated header instance at runtime for custom content. The header is not independently constructible.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Keep an accessible Back action and title when supplying custom content or actions.

## Related guides and API

- [Pages and navigation](../pages-and-navigation.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/PageNavigationPanel.cs)
- [API reference](<xref:ModernFormsNext.NavigationHeader>)
