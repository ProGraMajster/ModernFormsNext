# DocumentViewer

Displays a structured Document with selectable text, links and images. Use MarkdownViewer to construct document blocks from Markdown source.

Type `ModernFormsNext.DocumentViewer` in the `ModernFormsNext` assembly and NuGet package.
Inherits [`ScrollControl`](scroll-control.md).

Each example is a continuation of the previous one in a method on the UI
thread, with an existing `Form form`. See [example setup](control-fundamentals.md#example-setup)
for imports, ownership and startup.

## Quick start

```csharp
var control = form.Controls.Add(new DocumentViewer());
```

## Configuration example

```csharp
control.Dock = DockStyle.Fill;
control.MaxImageDownloadBytes = 2 * 1024 * 1024;
```

## Interaction example

```csharp
control.LinkClicked += (_, _) => form.Text = "Document link activated";
```

## Properties

| Property | C# type | Initial value | Description |
| --- | --- | --- | --- |
| `Document` | `Document` | Empty model | Model being displayed. |
| `SelectedText` | `string` | Empty without a selected range | Text in the selection. |

## Methods and behavior

Select(start, length), SelectAll() and Copy() operate on displayed text. InvalidateDocumentLayout() requests a new layout after model changes.

## Events

LinkClicked lets the host apply its own link policy. Do not open untrusted destinations without checking them.

## Platform support

The control logic and managed rendering are shared. Windows is the primary runtime. Android is experimental; check the supported host, input and accessibility scenarios before relying on a specific native behavior.
See the [platform matrix](../platforms/android.md) for Android release evidence.

## Designer support

Built-in data preview can use supported serializable properties. Wire application event handling and runtime state in code. Toolbox presence and editability follow the type and property metadata.
See the [Visual Studio host guide](../visual-studio-designer-host.md).

## Accessibility and practical considerations

The viewer exposes document and viewport semantics. Set conservative image byte and pixel limits for untrusted content.

## Related guides and API

- [Markdown](../markdown.md)
- [Control fundamentals](control-fundamentals.md) and [shared accessibility](../accessibility/semantic-model.md)
- [API reference](<xref:ModernFormsNext.DocumentViewer>)
