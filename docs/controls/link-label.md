# LinkLabel

Renders a caption containing one or more link ranges. The application decides what action a link triggers.

Type `ModernFormsNext.LinkLabel` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Label`](label.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new LinkLabel { Text = "Open help" });
```

## Configuration example

```csharp
control.LinkColor = SKColors.SteelBlue;
```

## Interaction example

```csharp
control.LinkClicked += (_, _) => { control.LinkVisited = true;
form.Text = "Help opened"; };
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `LinkVisited` | `bool` | false | Marks the link as visited. |
| `LinkArea` | `LinkArea` | Whole text when no explicit range is configured | Selects the text span used for a single link. |

## Methods and behavior

Configure Links for several ranges or LinkArea for one. Interpret the link data in the handler and apply your own navigation policy.

## Events

LinkClicked identifies the activated link. Updating visited styling does not itself activate a link.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Use a descriptive destination name. The platform adapters expose the documented link semantics.

## Related guides and API

- [Accessibility/current controls](../accessibility/current-controls.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/LinkLabelPanel.cs)
- [API reference](<xref:ModernFormsNext.LinkLabel>)
