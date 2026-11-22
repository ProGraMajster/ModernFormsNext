# GroupBox

Frames related child controls with a labeled caption and separate content styling. Use it to make the purpose of a set of inputs easy to discover.

Type `ModernFormsNext.GroupBox` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Control`](control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new GroupBox { Text = "Delivery" });
control.Controls.Add(new CheckBox { Text = "Send updates" });
```

## Configuration example

```csharp
control.CaptionForegroundColor = SKColors.Navy;
```

## Interaction example

```csharp
control.ControlAdded += (_, _) => form.Text = control.Text;
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `CaptionBackgroundBrush` | `Brush` | null | Optional caption brush. |
| `ContentBackgroundBrush` | `Brush` | null | Optional content-area brush. |
| `ShowShadow` | `bool` | false | Enables the control's group shadow. |

## Methods and behavior

Use Controls.Add to populate the content area. The DisplayRectangle describes the portion used for child layout.

## Events

Inherited layout and child events observe structure changes. Name the group through Text and use distinct names for its children.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Group captions supplement each child's own accessible description. Keep sufficient contrast in both regions.

## Related guides and API

- [Shadows and typography](../shadows-and-typography.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/GroupBoxPanel.cs)
- [API reference](<xref:ModernFormsNext.GroupBox>)
