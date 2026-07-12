# MarkdownEditor

`MarkdownEditor` is a native ModernFormsNext control for editing Markdown source. It is distinct
from `MarkdownViewer`: the editor preserves and displays Markdown punctuation, while the viewer
converts source through the existing `Markdown -> Document -> layout -> SkiaSharp` pipeline.

Neither control uses HTML, WebView, WinForms, or a native platform text control.

## Basic usage

```csharp
var editor = new MarkdownEditor
{
    Dock = DockStyle.Fill,
    Markdown = "# Hello",
    ViewMode = MarkdownEditorViewMode.Split,
    ShowToolbar = true
};

editor.MarkdownChanged += (_, _) => SaveButton.Enabled = editor.Modified;
editor.PreviewLinkClicked += (_, e) => HandleLink(e.Destination);
```

`Text` is an exact alias of `Markdown`. Assigning either property programmatically clears undo
history and resets `Modified`. Source offsets (`SelectionStart` and `SelectionLength`) are UTF-16
indexes, matching normal .NET string indexing.

## Editing core

The private editing surface derives from `RichTextBox`, so Markdown editing shares the framework's
caret, selection, keyboard, mouse, clipboard, scrolling, focus, and backend IME path. Double-click
selects a word or a complete surrogate pair. `ReadOnly` still permits selection and copying but
blocks typing, cut, paste, undo/redo, and formatting commands.

Text is Unicode and arrives through the platform `TextInput`/IME path rather than being inferred
from physical keys. This preserves dead keys, composed text, emoji, and international layouts.
On Windows, Right Alt/AltGr is retained as the `AltGraph` modifier even when Windows also reports
the synthetic Control+Alt state. Editing shortcuts require shortcut Control and therefore do not
conflict with AltGr input; normal Ctrl shortcuts continue to work.

The first stage supports:

- multiline source editing and wrapping;
- TAB and ENTER configuration through `AcceptsTab` and `AcceptsReturn`;
- selection, copy, cut, and paste;
- `CanUndo`, `CanRedo`, `Undo()`, `Redo()`, and `ClearUndo()`;
- continuous typing grouped into a single undo record;
- `MaxLength`, `Modified`, and selection events.

Undo history stores changed ranges and selection states instead of a full document snapshot for
each typed character. Every formatting command is recorded as one undo operation.

## Commands

Inline commands wrap or unwrap the current selection. With an empty selection they insert paired
markers and place the caret between them:

```csharp
editor.ToggleBold();
editor.ToggleItalic();
editor.ToggleStrikethrough();
editor.ToggleInlineCode();
```

Line commands operate on every complete line touched by the selection and preserve LF or CRLF
line endings:

```csharp
editor.InsertHeading(2);
editor.ToggleBlockQuote();
editor.ToggleUnorderedList();
editor.ToggleOrderedList();
editor.ToggleTaskList();
editor.ToggleCodeBlock();
editor.Indent();
editor.Outdent();
```

Ordered-list conversion numbers selected lines sequentially from `1.`. List conversion replaces
an existing ordered, unordered, or task marker instead of stacking another marker.

Insertion commands are also available:

```csharp
editor.InsertLink("https://example.com", "Example");
editor.InsertImage("Images/icon.png", "Application icon", "Optional title");
editor.InsertHorizontalRule();
```

`InsertLink` and `InsertImage` mutate source immediately and create one undo record. They escape
Markdown label, destination, alt-text, and title delimiters without URL-encoding the whole
destination. Relative paths, HTTP/HTTPS URLs, data URIs, `mailto:`, `file:`, and other destination
forms remain host policy.

## Hosted link and image requests

The editor never opens a native dialog or file picker. `RequestInsertLink()` and
`RequestInsertImage()` raise host-facing events. The toolbar and Ctrl+K use the same public request
path. A synchronous host can fill the event data directly:

```csharp
editor.InsertLinkRequested += (_, e) =>
{
    e.Text = e.SuggestedText;
    e.Url = "https://example.com";
    e.Handled = true;
};
```

An asynchronous host obtains a deferral before its first `await`:

```csharp
var editor = new MarkdownEditor
{
    ViewMode = MarkdownEditorViewMode.Split,
    SynchronizeScrolling = true
};

editor.InsertLinkRequested += async (_, e) =>
{
    using var deferral = e.GetDeferral();
    var result = await ShowLinkDialogAsync(e.SuggestedText, e.SuggestedUrl);

    if (result is null)
    {
        e.Cancel = true;
        return;
    }

    e.Text = result.Text;
    e.Url = result.Url;
    e.Handled = true;
};
```

`InsertImageRequested` follows the same pattern and exposes `Source`, `AltText`, and optional
`Title`. The host chooses a resource; the editor does not copy files or download images during the
request. An empty title is omitted from generated Markdown.

### Local image assets

An image request can either insert a reference unchanged or copy a local file through the shared
asset workflow:

```csharp
editor.InsertImageRequested += async (_, e) =>
{
    using var deferral = e.GetDeferral();
    var selected = await SelectImageAsync();
    if (selected is null)
    {
        e.Cancel = true;
        return;
    }

    e.Source = selected;
    e.SourceKind = MarkdownImageSourceKind.LocalFile;
    e.AssetOptions = new MarkdownImageAssetOptions
    {
        DestinationDirectory = applicationImageDirectory,
        MarkdownBaseDirectory = documentDirectory,
        CollisionBehavior = MarkdownImageAssetCollisionBehavior.GenerateUniqueName
    };
    e.Handled = true;
};
```

`MarkdownImageAssetProcessor` validates and copies with bounded asynchronous file I/O. PNG, JPEG,
GIF, WebP, and BMP are enabled by default. SVG is intentionally excluded because the current
SkiaSharp bitmap pipeline does not decode it. Allowed extensions, maximum encoded size, and magic
byte validation are configurable. Filenames are sanitized using a portable invalid-character set,
the source extension is retained, and generated Markdown paths use `/`.

Collision behavior can cancel, overwrite through a temporary file in the destination directory,
generate a unique numeric suffix, or use an existing validated file. Absolute Markdown sources
are rejected by default when the configured base and destination cannot form a relative path.
Set `AllowAbsoluteMarkdownSource` only when that exposure is an explicit host decision.

`InsertImageAssetAsync` exposes the same pipeline directly for host file pickers and future input
adapters. `ImageInsertFailed` reports controlled picker, validation, and I/O failures without
modifying source or history. Source changes and disposal cancel an active copy; an externally
supplied cancellation token follows normal task cancellation semantics.

Each request captures selected text, UTF-16 selection offsets, and a source-version token. A
simple existing Markdown link or image on the current line is recognized through a bounded
Markdig parse and supplied as initial values; approval replaces the complete element. If source
changes before deferred completion, or the editor becomes read-only or is disposed, the result is
rejected instead of using stale offsets. Cancel and unhandled requests restore the unchanged
selection, add no undo record, and do not set `Modified`. A successful request is one command and
places the caret after the inserted element before returning focus to the editor.

The toolbar groups undo/redo, inline formatting, headings and quotes, lists, links and images, and
block insertion with separators. Its compact typographic glyphs use the existing `ToolBar` item
model. Each item has a native tooltip, and undo/redo and editing enabled states follow the editor's
history and `ReadOnly` state. Bold, italic, strikethrough, inline code, heading, quote, and list
items show a conservative active state when the current source line can be recognized without a
full Markdown parse.

## Keyboard editing

The editor provides these formatting shortcuts. Shortcut Control explicitly excludes AltGraph, so
Polish and other international keyboard layouts keep their normal text-input behavior:

| Shortcut | Command |
| --- | --- |
| `Ctrl+B` | `ToggleBold()` |
| `Ctrl+I` | `ToggleItalic()` |
| `Ctrl+K` | `RequestInsertLink()` |
| `Ctrl+Shift+X` | `ToggleStrikethrough()` |
| `Ctrl+backtick` | `ToggleInlineCode()` |
| `Ctrl+Shift+7` | `ToggleOrderedList()` |
| `Ctrl+Shift+8` | `ToggleUnorderedList()` |

Ctrl+K is active only while the source surface has focus and editing is allowed. Shortcut Control
explicitly excludes AltGraph, so AltGr+K remains ordinary international text input.

Enter continues unordered, ordered, and task-list markers and block quotes. Ordered markers are
incremented, and a continued task is unchecked. Pressing Enter on an otherwise empty marker exits
that construct. The operation preserves LF or CRLF, replaces an active selection predictably, and
is stored as one undo record.

Within list lines, Tab and Shift+Tab indent or outdent all touched lines. Outside a list, normal
`AcceptsTab` behavior from the shared text editor remains in effect. Backspace directly after an
unordered, ordered, task, quote, or ATX-heading prefix removes only that prefix. These operations
are also single undo records.

## Preview modes

`ViewMode` accepts:

- `Editor` for source only;
- `Preview` for the native rendered document only;
- `Split` for side-by-side source and preview using `SplitContainer`.

The editor creates one source surface and one `PreviewViewer` and reuses them when modes change.
In Editor mode, source changes do not parse or lay out a hidden preview. In Preview or Split mode,
updates are debounced by `PreviewUpdateDelayMilliseconds` (220 ms by default). Set it to zero for
immediate updates.

Preview content is one-way (`MarkdownEditor` to `MarkdownViewer`) and is marked dirty while hidden.
Entering Preview or Split synchronizes it immediately. Returning to Editor stops a pending update,
and disposal stops and releases the single per-control timer. At design time preview updates are
suppressed, which also prevents preview image loading from being started by property changes.

`PreviewViewer` is the normal `MarkdownViewer`, so its `DocumentStyle`, image options,
`LinkClicked`, selection, and copy behavior remain available. URLs are not opened automatically.
`PreviewLinkClicked` forwards preview activation from the editor without changing source selection
or invoking an insertion request.

In Split mode, `SynchronizeScrolling` is enabled by default. Source and preview offsets are mapped
proportionally across their independently calculated scrollable ranges. A reentrancy guard prevents
programmatic target updates from being propagated back, and one-pixel rounding differences are
ignored to avoid jitter. Synchronization is inactive in Editor and Preview modes, has no history or
`Modified` effect, and is reapplied after split layout and preview replacement. It is intentionally
not line- or block-semantic, so documents whose rendered blocks have very different heights may be
approximately rather than exactly aligned.

`SynchronizeScrolling`, `InsertLinkRequested`, `InsertImageRequested`, and `PreviewLinkClicked`
are exposed through normal component metadata. Design mode does not raise insertion requests,
parse hidden preview updates, open dialogs, or start image loading from this control.

`RefreshPreviewImages()` and `DocumentViewer.ReloadDocumentImages()` clear per-viewer image
resources when a host replaces a file without changing its Markdown source. A copied asset forces
the visible preview through this path only when needed; source changes still use the normal single
parse and cache replacement path.

## Source highlighting

The source editor uses a lightweight, platform-neutral tokenizer for headings, emphasis,
strikethrough, inline code, fenced-code delimiters, quotes, lists, task lists, links, images, and
horizontal rules. Highlighting applies presentation ranges to the shared RichTextKit layout and
never changes `Markdown`, selection, clipboard text, or undo history.

To keep very large documents editable with the current eager RichTextKit layout, detailed source
highlighting falls back to one normal text run above 200,000 UTF-16 characters. The source,
commands, selection, undo/redo, and preview behavior are unchanged by this safeguard.
Detailed highlighting is inclusive at 200,000 characters and automatically resumes after the
source falls back below or onto that threshold.

Customize theme-aware colors through `SyntaxStyle`. Nullable colors inherit the active theme and
control style:

```csharp
editor.SyntaxStyle.CodeMarkerColor = Theme.AccentColor2;
editor.SyntaxStyle.SelectionBackgroundColor = Theme.TextSelectionBackgroundColor;
editor.PreviewStyle.ShowCodeBlockLanguage = true;
```

## Current limitations

This first stage is a Markdown source editor, not WYSIWYG. It does not provide visual block
editing, HTML editing, collaboration, track changes, minimap, multiple carets, semantic
editor/preview scroll mapping, drag-and-drop file insertion, or compiler-grade incremental parsing.
Horizontal text scrolling and native touch selection handles remain limited by the current shared
`RichTextBox` implementation. PageUp/PageDown navigation is also deferred until the shared editing
core exposes it consistently. The shared text core still performs eager full-text layout for caret
scrolling, so very large files are not yet virtualized even when detailed highlighting has fallen
back to one run. Source highlighting is lightweight and intentionally does not claim to validate
every CommonMark or GFM construct. Active toolbar states use bounded source inspection and remain
neutral for ambiguous or very long lines. The current custom designer exposes the Markdown string
property but does not yet provide a dedicated multiline dialog editor.

The framework does not yet expose stable control-level drag/drop events, so MarkdownEditor does
not claim built-in image drag/drop. The clipboard abstraction supports text and platform storage
items but has no common binary-image format across Windows and Android. Consequently Ctrl+V
remains text-only and AltGr-safe. Future drag/drop and clipboard-image adapters should resolve a
local file or stream in the host and call `InsertImageAssetAsync`, preserving the same validation,
collision, undo, cancellation, and preview behavior.
