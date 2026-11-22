# MarkdownEditor

Edits Markdown source with optional live preview, an optional toolbar and semantic editing commands. Manage files and asset policy in the application.

Type `ModernFormsNext.MarkdownEditor` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`Panel`](panel.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new MarkdownEditor { Markdown = "# Notes\n\nWrite here." });
```

## Configuration example

```csharp
control.ShowToolbar = true;
```

## Interaction example

```csharp
control.MarkdownChanged += (_, _) => form.Text = "Draft changed";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Markdown` | `string` | Empty | Alias for the Markdown source text. |
| `ReadOnly` | `bool` | false | Suppresses editing. |
| `PreviewUpdateDelayMilliseconds` | `int` | 220 | Controls the preview debounce interval. |

## Methods and behavior

Use ToggleBold(), InsertHeading(level) or InsertLink(...) to edit the current selection. Undo and Redo track editor history.

## Events

MarkdownChanged exposes source changes. PreviewLinkClicked routes a preview interaction to the host. InsertLinkRequested and InsertImageRequested allow customized UI.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android support is experimental. The platform matrix lists the qualified host, input and accessibility scenarios.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Text editing and preview semantics are separate. Use the IME and accessibility guides for native coverage limits.

## Related guides and API

- [Markdown editor](../markdown-editor.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/MarkdownEditorPanel.cs)
- [API reference](<xref:ModernFormsNext.MarkdownEditor>)
