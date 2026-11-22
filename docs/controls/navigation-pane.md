# NavigationPane

Displays a flat list of navigation choices with images. The application updates the active content in response to selection.

Type `ModernFormsNext.NavigationPane` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new NavigationPane());
```

## Configuration example

```csharp
control.Width = 180;
control.Dock = DockStyle.Left;
var icon = new SKBitmap(16, 16);
form.Disposed += (_, _) => icon.Dispose();
control.Items.Add(new NavigationPaneItem(icon, "Reports"));
```

## Interaction example

```csharp
control.SelectedItemChanged += (_, _) => form.Text = control.SelectedItem?.Text ?? "Choose a section";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Items` | `NavigationPaneItemCollection` | Empty | Pane choices. |
| `SelectedIndex` | `int` | -1 | Index of the selected item. |
| `SelectedItem` | `NavigationPaneItem` | null | Current item. |

## Methods and behavior

Add NavigationPaneItem values containing an image and optional text, then set SelectedIndex to restore selection.

## Events

SelectedItemChanged reports a changed choice. Keep the destination's lifecycle in the host rather than the pane.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Supply descriptive item text even if icons are the primary visual affordance.

## Related guides and API

- [Pages and navigation](../pages-and-navigation.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/NavigationPanePanel.cs)
- [API reference](<xref:ModernFormsNext.NavigationPane>)
