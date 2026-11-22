# Ribbon

Organizes top-level actions by ribbon tab and item group. Use a ToolBar for a small, flat command set.

Type `ModernFormsNext.Ribbon` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new Ribbon());
```

## Configuration example

```csharp
control.ShowTabs = true;
control.TabPages.Add("Home");
control.TabPages.Add("View");
```

## Interaction example

```csharp
control.SelectedTabPageIndexChanged += (_, _) => form.Text = $"Ribbon tab {control.SelectedTabPageIndex}";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `TabPages` | `RibbonTabPageCollection` | Empty | Ribbon tabs and their item groups. |
| `ShowTabs` | `bool` | false | Whether the tab selectors are shown. |

## Methods and behavior

Populate a RibbonTabPage with RibbonItemGroup models and item actions. Update SelectedTabPageIndex when restoring a choice.

## Events

SelectedTabPageIndexChanged reports selection across the configured tabs. Individual MenuItem actions have their own handlers.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Name pages and commands and test a keyboard path to the essential actions in a complex ribbon.

## Related guides and API

- [Commands](../commands.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/RibbonPanel.cs)
- [API reference](<xref:ModernFormsNext.Ribbon>)
