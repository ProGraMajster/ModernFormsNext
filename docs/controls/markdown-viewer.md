# MarkdownViewer

Converts Markdown source to a document and displays it with DocumentViewer's selection and rendering. The enabled syntax and assets follow the viewer guide.

Type `ModernFormsNext.MarkdownViewer` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`DocumentViewer`](document-viewer.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new MarkdownViewer { Markdown = "# Release notes\n\nRead the changelog." });
```

## Configuration example

```csharp
control.Dock = DockStyle.Fill;
control.MaxImagePixelCount = 2_000_000;
```

## Interaction example

```csharp
control.LinkClicked += (_, _) => form.Text = "Preview link selected";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Markdown` | `string` | Empty | Source supplied to the parser. |
| `DocumentStyle` | `DocumentStyle` | Instance style | Controls document presentation. |

## Methods and behavior

Assign Markdown to change the source. Use inherited ReloadDocumentImages() to refresh resources when their underlying contents change.

## Events

Inherited LinkClicked reports link activation. Handle navigation explicitly and apply the app's image-loading policy.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

Use semantic headings and descriptive link text. Guard image work with byte and pixel limits.

## Related guides and API

- [Markdown](../markdown.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [ControlGallery example](https://github.com/ProGraMajster/ModernFormsNext/blob/master/samples/ControlGallery/Panels/MarkdownViewerPanel.cs)
- [API reference](<xref:ModernFormsNext.MarkdownViewer>)
