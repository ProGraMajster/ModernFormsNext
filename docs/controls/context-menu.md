# ContextMenu

Attaches a managed popup menu to a control. Organize contextual actions using MenuItem and MenuSeparatorItem models.

Type `ModernFormsNext.ContextMenu` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`MenuDropDown`](menu-drop-down.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var anchor = form.Controls.Add(new Button { Text = "Document" });
var control = new ContextMenu();
anchor.ContextMenu = control;
```

## Configuration example

```csharp
var rename = new MenuItem("Rename");
control.Items.Add(rename);
```

## Interaction example

```csharp
rename.Click += (_, _) => form.Text = "Rename requested";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Items` | `MenuItemCollection` | Empty | Actions shown in the popup. |
| `Visible` | `bool` | false before opening | Inherited popup visibility. |

## Methods and behavior

Show(Control, Point) also opens the menu explicitly at a position relative to the target. Hide dismisses the menu.

## Events

Subscribe to each MenuItem.Click or assign its Command. Attach the menu only where the actions apply.

## Platform support

Windows is the primary runtime. On experimental Android, window and popup behavior requires the bounded Activity/Form host where applicable. A standalone shared surface does not provide independent desktop windows.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Provide an equivalent accessible entry point for required actions and verify popup focus on the target backend.

## Related guides and API

- [Commands](../commands.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/MenuPanel.cs)
- [API reference](<xref:ModernFormsNext.ContextMenu>)
