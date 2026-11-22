# ToolBar

Displays managed MenuItem actions in a toolbar. Use command sources to keep availability shared across toolbar and shortcut entry points.

Type `ModernFormsNext.ToolBar` in the `ModernFormsNext` assembly and NuGet package.
Inherits `MenuBase`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new ToolBar());
var refresh = new MenuItem("Refresh");
control.Items.Add(refresh);
```

## Configuration example

```csharp
control.Dock = DockStyle.Top;
```

## Interaction example

```csharp
refresh.Click += (_, _) => form.Text = "Refresh requested";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Items` | `MenuItemCollection` | Empty | Commands and optional drop-down entries. |
| `Style` | `ControlStyle` | Per-instance | Controls bar presentation. |

## Methods and behavior

Add items through Items. An item with child items participates in the drop-down behavior inherited from MenuBase.

## Events

Handle the item's Click or configure its Command. Do not rely on the parent Control.Click to identify a toolbar command.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Give image-only items meaningful labels and offer keyboard access through shortcuts where appropriate.

## Related guides and API

- [Commands](../commands.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/ToolBarPanel.cs)
- [API reference](<xref:ModernFormsNext.ToolBar>)
