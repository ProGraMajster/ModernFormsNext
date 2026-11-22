# RichTextBox

Edits styled runs and paragraphs, with RTF and plain-text import/export. Use it when formatting and selection belong in the same editor.

Type `ModernFormsNext.RichTextBox` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`TextBox`](text-box.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new RichTextBox { Text = "Project notes" });
```

## Configuration example

```csharp
control.Select(0, 7);
control.SelectionColor = SKColors.SteelBlue;
```

## Interaction example

```csharp
control.SelectionChanged += (_, _) => form.Text = $"{control.SelectionLength} selected";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `SelectionLength` | `int` | 0 | Number of characters in the current selection. |
| `DetectUrls` | `bool` | false | Enables recognized URLs. |
| `ZoomFactor` | `float` | 1 | Scales the editor view. |

## Methods and behavior

Use Select(start, length) to choose a range. Find searches the editor content. LoadFile and SaveFile use the selected stream type. Consult the rich text guide for the RTF subset.

## Events

SelectionChanged tracks caret and range motion. ContentsResized reports content layout changes. HScroll and VScroll observe respective scroll changes.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Windows and Android adapt the shared text range provider. Keep custom formatting distinct from accessible names and actions.

## Related guides and API

- [Richtextbox](../richtextbox.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/RichTextBoxPanel.cs)
- [API reference](<xref:ModernFormsNext.RichTextBox>)
