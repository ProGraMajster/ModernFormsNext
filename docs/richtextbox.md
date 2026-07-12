# RichTextBox

`RichTextBox` is a multiline text editor with WinForms-style rich text APIs. The ModernFormsNext implementation is platform-neutral: it reuses the existing `TextBox` editing model and renders formatted text through SkiaSharp and RichTextKit instead of creating a native WinForms or Windows RichEdit control.

## Basic Usage

```csharp
var editor = new RichTextBox
{
    Dock = DockStyle.Fill,
    Text = "ModernFormsNext rich text"
};

editor.Select(0, "ModernFormsNext".Length);
editor.SelectionFont = new Font("Segoe UI", 12, FontStyle.Bold);
editor.SelectionColor = SKColors.DodgerBlue;
editor.DeselectAll();
```

## Formatting

Formatting is stored independently from the plain `Text` value. Assigning `Text` replaces the document with plain text and clears explicit run formatting. Editing operations keep formatting attached to the original character ranges as text is inserted or removed.

Supported formatting members include:

- `SelectionFont`
- `SelectionColor`
- `SelectionBackColor`
- `SelectedText`
- `SelectedRtf`
- `Rtf`
- `ZoomFactor`
- `SelectionAlignment`
- `SelectionType`

## Loading And Saving

`LoadFile` and `SaveFile` support `RichText`, `RichNoOleObjs`, `PlainText`, `TextTextOleObjs`, and `UnicodePlainText`.

The RTF implementation intentionally supports a portable subset: text, font family, font size, bold, italic, underline, strikeout, foreground color, and background color. Unsupported RTF destinations are ignored.

## Compatibility Notes

ModernFormsNext exposes common WinForms-style enums and events such as `RichTextBoxFinds`, `RichTextBoxStreamType`, `RichTextBoxScrollBars`, `RichTextBoxSelectionTypes`, `ContentsResized`, `SelectionChanged`, and `VScroll`.

The following native RichEdit behaviors are not implemented yet: OLE objects, protected range enforcement, automatic URL link activation, bullet rendering, paragraph indentation rendering, custom tab stop rendering, and IME language option behavior. Those members are stored where useful so code can migrate without pulling Windows-specific behavior into the shared framework.
