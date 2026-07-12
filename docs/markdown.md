# Markdown and Documents

For editable Markdown source, formatting commands, undo/redo, and optional native preview, see
[MarkdownEditor](markdown-editor.md). `MarkdownViewer` remains a read-only renderer.

`MarkdownEditor` can request hosted link and image data without depending on a platform dialog.
Its toolbar and Ctrl+K raise the same public request events, while programmatic `InsertLink` and
`InsertImage` commands generate Markdown that round-trips through this document parser. Preview
link activations are forwarded through `PreviewLinkClicked`; destinations are never opened
automatically.

ModernFormsNext includes a native read-only document pipeline for formatted content:

```text
Markdown source
    -> MarkdownParser
    -> ModernFormsNext.Documents.Document
    -> DocumentLayoutEngine
    -> SkiaSharp renderer
    -> MarkdownViewer
```

The document model is not Markdown-specific. Markdown is parsed with Markdig, converted into
`ModernFormsNext.Documents` nodes, then rendered by `DocumentViewer`. `MarkdownViewer` does not use
HTML, WebView, native controls, WinForms, WPF, or a separate Markdown renderer.

## MarkdownViewer

```csharp
var viewer = new MarkdownViewer
{
    Dock = DockStyle.Fill,
    Markdown = """
    # ModernFormsNext

    Native **Markdown** rendering with async images.

    ![Logo](Images/icon.png "Application logo")

    Visit https://github.com/ProGraMajster/ModernFormsNext.
    """
};

viewer.LinkClicked += (_, e) =>
{
    Console.WriteLine(e.Destination);
};
```

Assigning `Markdown` parses the source into a new `Document`, invalidates cached layout, and
repaints the control. `null` and empty strings produce an empty document.

Links are rendered as links, support hover, pressed feedback, a hand cursor on pointer platforms,
and click hit testing across wrapped text. A successful primary-pointer click raises `LinkClicked`.
ModernFormsNext does not open URI destinations automatically.

## Link Interaction

Pointer down on a link enters its pressed visual state but does not activate it. Pointer up over
the same semantic link raises `LinkClicked` exactly once. Moving at least three logical pixels
(scaled through the viewer DPI pipeline) starts text selection and cancels activation; smaller
pointer jitter still counts as a click. Releasing outside the link or over a different link also
cancels activation.

```csharp
viewer.LinkClicked += (_, e) =>
{
    // The application decides whether and how to navigate.
    Console.WriteLine(e.Destination);
};
```

`DocumentViewer` never launches a browser or invokes a platform URI handler itself. Link hit
testing uses the RichTextKit glyph actually under the pointer, including either visual line of a
wrapped link. Local RichTextKit ranges use Unicode code-point indices; the separate document
selection map remains UTF-16, so emoji before a link does not shift either contract.

Desktop pointer hover uses `Cursors.Hand`. Backends that translate a primary touch pointer into
the shared `Control` input path use the same press, drag, release, and cancellation state machine.
Keyboard focus traversal and keyboard activation of individual links are not implemented yet.

## Link Colors

Link colors are resolved from `DocumentStyle` and the active theme:

```csharp
viewer.DocumentStyle.LinkColor = Theme.AccentColor;
viewer.DocumentStyle.HoveredLinkColor = Theme.AccentColor2;
viewer.DocumentStyle.PressedLinkColor = customPressedColor;
```

Pressed color has priority over hover color. The default pressed color is blended from theme and
control foreground values so it remains distinct from hover in both light and dark themes.

## Compatibility Matrix

| Markdown feature | Current behavior |
| --- | --- |
| Paragraphs and headings | Native document blocks, wrapped by RichTextKit and rendered with SkiaSharp. |
| Strong, emphasis, strong plus emphasis, strikethrough, inline code | Native inline nodes. |
| Links, auto links, email auto links | Native `LinkInline`; email auto links use `mailto:` destinations. |
| Hard and soft line breaks | Hard breaks become line breaks; soft breaks become spaces. |
| Fenced and indented code blocks | Native `CodeBlock`; language metadata is preserved. |
| Block quotes | Native quoted layout with a themed border. |
| Unordered, ordered, nested, and mixed lists | Native list layout with separate marker columns and hanging indent. |
| Task lists | Native read-only checkbox markers; text is not rendered as `[x]` source syntax. |
| Horizontal rules | Native separator elements. |
| Images | Standalone images become `ImageBlock`; mixed inline images currently render fallback text inline. |
| GFM pipe tables | Native `TableBlock` with alignment, wrapping cells, borders, and header styling. |
| Footnotes | Native footnote references and a rendered footnote section after the main content. |
| Raw HTML | Preserved as text. It is not interpreted, executed, or rendered as HTML. |
| Definition lists | Not currently exposed as semantic Markdig nodes by the configured pipeline in this repository. |
| Syntax highlighting | Native semantic highlighting for C#/CS, JSON, XML, Bash/shell, and PowerShell; unknown languages use plain code. |
| Text selection/copy | Read-only selection across document elements with mouse drag, Ctrl+A, Ctrl+C, and public selection APIs. |

## Lists

Unordered lists are rendered from semantic `ListBlock` nodes. The renderer does not draw the source
marker back as Markdown text. Default bullets depend on nesting depth:

- level 0: `•`
- level 1: `◦`
- level 2: `▪`

Deeper levels repeat that sequence. Ordered lists keep numeric markers and respect
`ListBlock.StartNumber`. List markers are separate layout elements, so wrapped list item text hangs
under the content column instead of under the marker. Task list items use native checkbox elements.

## Code Blocks

Fenced code block language identifiers are preserved in `CodeBlock.Language`:

````markdown
```csharp
var viewer = new MarkdownViewer();
```
````

Only the first non-whitespace info token is stored, so a fence such as:

````markdown
```   csharp   metadata
```
````

stores `csharp`. A fence without a language stores `null`.

Fenced code blocks are highlighted natively when `CodeBlock.Language` identifies one of these
language families:

- `csharp`, `cs`,
- `json`,
- `xml`,
- `bash`, `shell`, `sh`,
- `powershell`, `ps1`, `pwsh`.

The built-in highlighters are deliberately lightweight lexical highlighters, not full compilers.
Unknown languages and code blocks without language metadata use the normal code foreground color.
Highlighting only splits the existing source into styled RichTextKit runs; it does not alter
`CodeBlock.Text`, selection offsets, or copied text.

`DocumentStyle.CodeStyle` exposes nullable semantic colors for keywords, strings, numbers,
comments, types, properties, and punctuation. Defaults derive from `Theme`. The optional
`DocumentStyle.ShowCodeBlockLanguage` header is `false` by default. When enabled, a fenced block
with language metadata displays a non-selectable language label and separator above the source;
indented code and fences without metadata have no header.

`DocumentStyle.CodeBlockWrap` controls whether long code lines wrap. The default is `false`, so
preformatted line semantics are preserved and long lines are clipped to the code block bounds.

## Text Selection and Copy

`DocumentViewer` and `MarkdownViewer` are read-only, but their rendered text can be selected. The
selection uses one logical UTF-16 text index across headings, paragraphs, links, code blocks,
quotes, list items, table cells, and footnotes. Layout-only spacing, table borders, task checkbox
graphics, horizontal rules, and image bitmaps are not selectable.

```csharp
viewer.Select(0, 12);
Console.WriteLine(viewer.SelectedText);

viewer.SelectAll();
viewer.Copy();
viewer.ClearSelection();
```

Mouse drag selects in either direction and can cross layout elements. A click on a link raises
`LinkClicked`; dragging from link text starts selection and suppresses link activation. Double
click cancels the second pending link press and selects a word, so its second pointer-up does not
activate the link again. When the viewer has focus, Ctrl+A selects all text, Ctrl+C copies a
non-empty selection through the platform clipboard abstraction, and Escape clears selection.

RichTextKit renders the selection separately for every text element, preserving wrapped lines,
heading metrics, code blocks, links, and table cells. `DocumentStyle.SelectionBackgroundColor`
customizes the highlight; the default is `Theme.TextSelectionBackgroundColor`.

Desktop mouse selection is fully supported. A backend that translates a primary touch pointer to
the shared mouse/pointer events can use the same basic drag model, but native touch selection
handles, a magnifier, and platform context menus are not implemented yet.

## Tables

GFM pipe tables are converted to `TableBlock`, `DocumentTableRow`, and `DocumentTableCell`. Column
alignment from the Markdown separator row is preserved as `DocumentTextAlignment`.

Columns are sized from their content. The layout measures a minimum width from the longest visible
token and a preferred width from unwrapped cell content, including headers. Available space is
distributed between those bounds, so a description column naturally receives more room than a
short status column. Under extremely narrow widths, columns remain positive and content wraps.

Cell text keeps the alignment declared by the Markdown separator row. Headers remain bold, cell
text is clipped to table bounds, and the complete table is fitted to the available document width.
This is a predictable native layout algorithm, not the full browser/CSS table algorithm, and it
does not add a horizontal scrollbar.

## Images

Standalone Markdown images are converted to `ImageBlock`:

```markdown
![ModernFormsNext](Images/icon.png "Logo")
```

The node stores:

- `Source`,
- `AltText`,
- `Title`.

`DocumentViewer` loads images asynchronously through a per-viewer cache. Loading is not performed
during paint or layout. While an image is loading, or if loading fails, the viewer draws a
placeholder containing the alt text or source.

Supported sources are:

- absolute `http` URLs,
- absolute `https` URLs,
- absolute `file` URIs,
- relative file paths resolved from `AppContext.BaseDirectory`,
- `data:` URIs.

The cache deduplicates sources per viewer, tracks pending/success/failure states, cancels pending
loads when the document changes, and suppresses callbacks after disposal. Configure limits through
the stable per-viewer `ImageLoadOptions` instance:

```csharp
var viewer = new MarkdownViewer();

viewer.ImageLoadOptions.MaxDownloadBytes = 2 * 1024 * 1024;
viewer.ImageLoadOptions.MaxDecodedPixels = 8_000_000;
viewer.ImageLoadOptions.RequestTimeout = TimeSpan.FromSeconds(5);

viewer.Markdown = markdown;
```

Defaults are 10 MiB encoded data, 32 million decoded pixels, and a 15 second HTTP/HTTPS request
timeout. A zero timeout disables the per-request timeout. These limits reduce uncontrolled
resource use while loading document images; they are not a security sandbox.

Changing an option cancels pending loads and retries pending or failed resources with one new
options snapshot. Successfully loaded images remain in the per-viewer cache. The compatibility
properties `MaxImageDownloadBytes`, `MaxImagePixelCount`, and `ImageRequestTimeout` address the same
options instance and do not create a second source of truth.

Images render at natural size unless they are wider than the available document width. Wider images
are scaled down proportionally; small images are not enlarged by default.

When a host replaces a local file without changing the document source, call
`ReloadDocumentImages()` to cancel stale pending loads and rebuild the per-viewer image cache.
`MarkdownEditor.RefreshPreviewImages()` provides the corresponding editor-preview operation.

Mixed inline images inside a paragraph currently render their fallback text inline. RichTextKit
0.4.167 exposes styled font runs and a password-mode replacement character, but no public inline
object contract with custom measurement, baseline participation, and drawing callbacks. A native
text-image-text run would therefore require a RichTextKit fork or an absolutely positioned overlay
that does not participate in line layout. Neither workaround is used; true inline images remain
deferred until the text layer has a real object-run abstraction.

## Raw HTML

`MarkdownViewer` is not an HTML engine. Raw HTML is not executed, interpreted, sanitized, or
rendered as HTML. `HtmlInline` and `HtmlBlock` nodes are converted to text so applications can make
an explicit later decision about HTML support without hidden WebView behavior.

## DocumentViewer and Plain Text

Use `DocumentViewer` directly when content already exists as a ModernFormsNext document or comes
from another input format:

```csharp
var document = new Document(new DocumentBlock[]
{
    new HeadingBlock(1, new DocumentInline[] { new TextInline("Release notes") }),
    new ImageBlock("Images/icon.png", "Product icon")
});

var viewer = new DocumentViewer
{
    Document = document
};
```

`Document.GetPlainText()` converts the complete document to readable text for export. It includes
list markers, table cell text, image fallback text, and footnote references. `SelectedText` uses the
layout's logical selectable-text map and returns only the current selection.

## Styling

`DocumentStyle` controls document-specific metrics and colors, including heading scales, code
colors, quote borders, list indentation, paragraph spacing, horizontal rules, image spacing, image
placeholder color, table padding, table borders, table header backgrounds, and selection highlight.
Defaults resolve through `Theme` and the viewer's `CurrentStyle` where possible.

```csharp
viewer.DocumentStyle.LinkColor = Theme.AccentColor2;
viewer.DocumentStyle.HoveredLinkColor = Theme.AccentColor;
viewer.DocumentStyle.PressedLinkColor = customPressedColor;
viewer.DocumentStyle.CodeFontFamily = "Cascadia Mono";
viewer.DocumentStyle.CodeStyle.KeywordColor = customKeywordColor;
viewer.DocumentStyle.ShowCodeBlockLanguage = true;
viewer.DocumentStyle.TableCellPadding = 8;
viewer.DocumentStyle.CodeBlockWrap = true;
```

## MarkdownViewer, RichTextBox, and MarkdownEditor

`MarkdownViewer` is a read-only renderer for Markdown converted into the shared document model.
`RichTextBox` is the general rich-text editing control. `MarkdownEditor` reuses its shared editing
core to edit Markdown source and can display this same `MarkdownViewer` in Preview or Split mode.
See [MarkdownEditor](markdown-editor.md) for commands, undo/redo, source highlighting, and current
editor limitations.

Current intentional limits:

- no `DocumentEditor`,
- no HTML engine,
- no native touch selection handles or magnifier,
- no keyboard link traversal or keyboard link activation,
- no true inline image object flow inside wrapped text,
- built-in syntax highlighting is lexical rather than compiler-grade semantic analysis.
