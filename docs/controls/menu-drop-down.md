# MenuDropDown

Displays a menu list in a managed popup. Usually a parent MenuItem owns the drop-down; use ContextMenu to attach one directly to a control.

Type `ModernFormsNext.MenuDropDown` in the `ModernFormsNext` assembly and NuGet package.
Inherits `MenuBase`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = new MenuDropDown();
var anchor = form.Controls.Add(new Button { Text = "Actions" });
```

## Configuration example

```csharp
var export = new MenuItem("Export");
control.Items.Add(export);
```

## Interaction example

```csharp
anchor.Click += (_, _) => control.Show(anchor, new Point(0, anchor.Height));
export.Click += (_, _) => form.Text = "Export requested";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Items` | `MenuItemCollection` | Empty | Actions and child submenus. |
| `Visible` | `bool` | false before opening | Current popup visibility. |

## Methods and behavior

Show(Control, Point) opens relative to the anchor. Hide() dismisses the popup. A valid window host must be available at the time of opening.

## Events

Listen to item Click events for actions. Visibility follows the popup lifecycle.

## Platform support

Windows is the primary runtime. On experimental Android, window and popup behavior requires the bounded Activity/Form host where applicable. A standalone shared surface does not provide independent desktop windows.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Place important actions on a discoverable trigger and verify focus returns after dismissal.

## Related guides and API

- [Commands](../commands.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [API reference](<xref:ModernFormsNext.MenuDropDown>)
