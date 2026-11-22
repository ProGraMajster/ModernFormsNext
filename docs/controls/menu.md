# Menu

Displays a managed menu bar, with MenuItem models for actions and submenus. Use ContextMenu for actions tied to a target location.

Type `ModernFormsNext.Menu` in the `ModernFormsNext` assembly and NuGet package.
Inherits `MenuBase`.

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Menu());
var file = new MenuItem("File");
control.Items.Add(file);
```

## Configuration example

```csharp
var export = new MenuItem("Export");
file.Items.Add(export);
```

## Interaction example

```csharp
export.Click += (_, _) => form.Text = "Export requested";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Items` | `MenuItemCollection` | Empty | Top-level menu items. |
| `IsActivated` | `bool` | false | Whether menu interaction is active. |

## Methods and behavior

Use Items.Add to build the hierarchy. GetItemAtLocation translates a point in the menu to an item when one is present.

## Events

MenuItem.Click handles the action. Assign item Command properties when actions should share command availability.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Use meaningful command names and conventional menu structure. Verify keyboard path in the hosting form.

## Related guides and API

- [Commands](../commands.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/MenuPanel.cs)
- [API reference](<xref:ModernFormsNext.Menu>)
