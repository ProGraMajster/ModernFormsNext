using System;

namespace ModernFormsNext;

public partial class MarkdownEditor
{
    /// <summary>
    /// Clears all undo and redo records without changing the source.
    /// </summary>
    public void ClearUndo()
    {
        history.Clear();
        cleanHistoryPosition = Modified ? -1 : 0;
        UpdateToolbarState();
    }

    /// <summary>
    /// Copies the selected source text to the platform clipboard.
    /// </summary>
    public void Copy() => editorSurface.Copy();

    /// <summary>
    /// Copies and removes the selected source text as one undo operation.
    /// </summary>
    public void Cut()
    {
        if (!ReadOnly)
            ExecuteCommand(editorSurface.Cut);
    }

    /// <summary>
    /// Pastes source text from the platform clipboard as one undo operation.
    /// </summary>
    public void Paste()
    {
        if (!ReadOnly)
            ExecuteCommand(editorSurface.Paste);
    }

    /// <summary>
    /// Reapplies the next edit in the undo history.
    /// </summary>
    public void Redo()
    {
        if (ReadOnly || !history.CanRedo)
            return;

        ApplyHistoryRecord(history.TakeRedo(), undo: false);
        UpdateModifiedFromHistory();
        UpdateToolbarState();
    }

    /// <summary>
    /// Selects a UTF-16 source range and scrolls the caret into view.
    /// </summary>
    /// <param name="start">The zero-based source index.</param>
    /// <param name="length">The number of UTF-16 characters to select.</param>
    public void Select(int start, int length) => editorSurface.Select(start, length);

    /// <summary>
    /// Selects all Markdown source.
    /// </summary>
    public void SelectAll() => editorSurface.SelectAll();

    /// <summary>
    /// Gives keyboard focus to the source editing surface.
    /// </summary>
    public new void Select() => editorSurface.Select();

    /// <summary>
    /// Reverts the latest edit in the undo history.
    /// </summary>
    public void Undo()
    {
        if (ReadOnly || !history.CanUndo)
            return;

        ApplyHistoryRecord(history.TakeUndo(), undo: true);
        UpdateModifiedFromHistory();
        UpdateToolbarState();
    }

    /// <summary>
    /// Inserts or removes bold markers around the selection.
    /// </summary>
    public void ToggleBold() => ToggleInline("**", "**");

    /// <summary>
    /// Inserts or removes italic markers around the selection.
    /// </summary>
    public void ToggleItalic() => ToggleInline("*", "*");

    /// <summary>
    /// Inserts or removes inline-code markers around the selection.
    /// </summary>
    public void ToggleInlineCode() => ToggleInline("`", "`");

    /// <summary>
    /// Inserts or removes strikethrough markers around the selection.
    /// </summary>
    public void ToggleStrikethrough() => ToggleInline("~~", "~~");

    /// <summary>
    /// Inserts or removes fenced-code markers around selected lines.
    /// </summary>
    public void ToggleCodeBlock()
    {
        if (ReadOnly)
            return;

        ExecuteCommand(() =>
        {
            var range = GetSelectedLineRange();
            var source = Markdown;
            var content = source.Substring(range.Start, range.Length);
            var newline = GetPreferredNewLine();
            var opening = "```" + newline;
            var closing = newline + "```";

            if (range.Start >= opening.Length
                && source.AsSpan(range.Start - opening.Length, opening.Length).SequenceEqual(opening)
                && range.Start + range.Length + closing.Length <= source.Length
                && source.AsSpan(range.Start + range.Length, closing.Length).SequenceEqual(closing))
            {
                ReplaceRange(
                    range.Start - opening.Length,
                    opening.Length + range.Length + closing.Length,
                    content,
                    range.Start - opening.Length,
                    content.Length);
                return;
            }

            var replacement = opening + content + closing;
            ReplaceRange(range.Start, range.Length, replacement, range.Start + opening.Length, content.Length);
        });
    }

    /// <summary>
    /// Inserts a Markdown link at the current selection.
    /// </summary>
    /// <param name="url">The link destination.</param>
    /// <param name="text">Optional visible label. The selection or a default label is used when omitted.</param>
    public void InsertLink(string url, string? text = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (ReadOnly)
            return;

        ExecuteCommand(() =>
        {
            var selection = GetSurfaceSelection();
            var label = text ?? (SelectedText.Length > 0 ? SelectedText : "link text");
            var replacement = CreateLinkMarkdown(label, url);
            var escapedLabelLength = MarkdownEditorMarkdownEscaping.EscapeLabel(label).Length;
            ReplaceRange(selection.Start, selection.Length, replacement, selection.Start + 1, escapedLabelLength);
        });
    }

    /// <summary>
    /// Inserts a Markdown image at the current selection.
    /// </summary>
    /// <param name="source">The image source.</param>
    /// <param name="altText">Optional alternative text. The selection or a default label is used when omitted.</param>
    public void InsertImage(string source, string? altText = null)
        => InsertImage(source, altText, null);

    /// <summary>
    /// Inserts a Markdown image with an optional title at the current selection.
    /// </summary>
    /// <param name="source">The relative path, URI, or data URI used as the image source.</param>
    /// <param name="altText">Optional alternative text. The selection or a default label is used when omitted.</param>
    /// <param name="title">Optional image title. Empty titles are omitted from the generated Markdown.</param>
    public void InsertImage(string source, string? altText, string? title)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReadOnly)
            return;

        ExecuteCommand(() =>
        {
            var selection = GetSurfaceSelection();
            var label = altText ?? (SelectedText.Length > 0 ? SelectedText : "image");
            var replacement = CreateImageMarkdown(label, source, title);
            var escapedLabelLength = MarkdownEditorMarkdownEscaping.EscapeLabel(label).Length;
            ReplaceRange(selection.Start, selection.Length, replacement, selection.Start + 2, escapedLabelLength);
        });
    }

    /// <summary>
    /// Inserts a Markdown horizontal rule on its own line near the caret.
    /// </summary>
    public void InsertHorizontalRule()
    {
        if (ReadOnly)
            return;

        ExecuteCommand(() =>
        {
            var selection = GetSurfaceSelection();
            var newline = GetPreferredNewLine();
            var prefix = selection.Start > 0 && Markdown[selection.Start - 1] != '\n' ? newline : string.Empty;
            var suffixIndex = selection.Start + selection.Length;
            var suffix = suffixIndex < Markdown.Length && Markdown[suffixIndex] != '\r' && Markdown[suffixIndex] != '\n'
                ? newline
                : string.Empty;
            var replacement = prefix + "---" + suffix;
            ReplaceRange(selection.Start, selection.Length, replacement, selection.Start + replacement.Length, 0);
        });
    }

    private void ToggleInline(string opening, string closing)
    {
        if (ReadOnly)
            return;

        ExecuteCommand(() =>
        {
            var selection = GetSurfaceSelection();
            var source = Markdown;

            if (selection.Start >= opening.Length
                && selection.Start + selection.Length + closing.Length <= source.Length
                && source.AsSpan(selection.Start - opening.Length, opening.Length).SequenceEqual(opening)
                && source.AsSpan(selection.Start + selection.Length, closing.Length).SequenceEqual(closing))
            {
                var content = source.Substring(selection.Start, selection.Length);
                ReplaceRange(
                    selection.Start - opening.Length,
                    opening.Length + selection.Length + closing.Length,
                    content,
                    selection.Start - opening.Length,
                    content.Length);
                return;
            }

            if (selection.Length >= opening.Length + closing.Length
                && source.AsSpan(selection.Start, opening.Length).SequenceEqual(opening)
                && source.AsSpan(selection.Start + selection.Length - closing.Length, closing.Length).SequenceEqual(closing))
            {
                var contentStart = selection.Start + opening.Length;
                var contentLength = selection.Length - opening.Length - closing.Length;
                var content = source.Substring(contentStart, contentLength);
                ReplaceRange(selection.Start, selection.Length, content, selection.Start, content.Length);
                return;
            }

            var selected = selection.Length > 0 ? source.Substring(selection.Start, selection.Length) : string.Empty;
            var replacement = opening + selected + closing;
            ReplaceRange(
                selection.Start,
                selection.Length,
                replacement,
                selection.Start + opening.Length,
                selected.Length);
        });
    }
}
