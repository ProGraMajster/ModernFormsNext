# TabPage

Contains content and metadata for a single TabControl tab. Use the parent control to manage selection and closing.

Type `ModernFormsNext.TabPage` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Panel`](panel.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var tabs = form.Controls.Add(new TabControl());
var control = tabs.AddTab("Reports");
```

## Configuration example

```csharp
control.Controls.Add(new Label { Text = "Latest reports" });
control.Pinned = true;
```

## Interaction example

```csharp
tabs.SelectedIndexChanged += (_, _) => form.Text = tabs.SelectedTabPage?.Text ?? "Reports";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Text` | `string` | Empty | Tab label. |
| `Closable` | `bool` | false | Whether the page is eligible to be closed. |
| `Pinned` | `bool` | false | Marks the tab as pinned. |

## Methods and behavior

Use the parent's AddTab or AddClosableTab methods to create and own the page. Use inherited Controls.Add for the page body.

## Events

Observe the parent selection and closing events. Page child and layout events continue to describe the content surface.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Give the tab a useful text label even when an icon is used.

## Related guides and API

- [Control fundamentals](control-fundamentals.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/TabControlPanel.cs)
- [API reference](<xref:ModernFormsNext.TabPage>)
