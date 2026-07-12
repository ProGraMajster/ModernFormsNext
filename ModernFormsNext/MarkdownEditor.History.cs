using System;

namespace ModernFormsNext;

public partial class MarkdownEditor
{
    internal bool TrackSurfaceEdit(Func<bool> edit, MarkdownEditKind kind)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (applyingHistory || programmaticTextChange || editDepth > 0)
            return edit();

        BeginEdit(kind);
        try
        {
            return edit();
        }
        finally
        {
            EndEdit();
        }
    }

    private void ApplyHistoryRecord(MarkdownEditRecord record, bool undo)
    {
        applyingHistory = true;
        try
        {
            var removeLength = undo ? record.InsertedText.Length : record.RemovedText.Length;
            var insertion = undo ? record.RemovedText : record.InsertedText;
            var selection = undo ? record.BeforeSelection : record.AfterSelection;
            editorSurface.Text = Markdown.Remove(record.Start, removeLength).Insert(record.Start, insertion);
            editorSurface.Select(selection.Start, selection.Length);
        }
        finally
        {
            applyingHistory = false;
        }
    }

    private void BeginEdit(MarkdownEditKind kind)
    {
        if (editDepth++ > 0)
            return;

        editKind = kind;
        editBeforeText = Markdown;
        editBeforeSelection = GetSurfaceSelection();
    }

    private void EndEdit()
    {
        if (--editDepth > 0)
            return;

        var afterText = Markdown;
        if (editBeforeText == afterText)
            return;

        var record = CreateEditRecord(editBeforeText, afterText, editBeforeSelection, GetSurfaceSelection(), editKind);
        if (cleanHistoryPosition > history.Position)
            cleanHistoryPosition = -1;

        var allowTypingMerge = editKind == MarkdownEditKind.Typing
            && cleanHistoryPosition != history.Position;
        history.Push(record, allowTypingMerge);
        UpdateModifiedFromHistory();
        UpdateToolbarState();
    }

    private void ExecuteCommand(Action command)
    {
        if (ReadOnly)
            return;

        BeginEdit(MarkdownEditKind.Command);
        try
        {
            command();
        }
        finally
        {
            EndEdit();
        }
    }

    private static MarkdownEditRecord CreateEditRecord(
        string before,
        string after,
        MarkdownSelection beforeSelection,
        MarkdownSelection afterSelection,
        MarkdownEditKind kind)
    {
        if (kind == MarkdownEditKind.Typing)
        {
            var removedLength = beforeSelection.Length;
            var insertedLength = Math.Max(0, after.Length - before.Length + removedLength);
            return new MarkdownEditRecord(
                beforeSelection.Start,
                removedLength > 0 ? before.Substring(beforeSelection.Start, removedLength) : string.Empty,
                insertedLength > 0 ? after.Substring(beforeSelection.Start, insertedLength) : string.Empty,
                beforeSelection,
                afterSelection,
                kind);
        }

        if (kind == MarkdownEditKind.Delete)
        {
            var removedLength = Math.Max(0, before.Length - after.Length);
            var start = beforeSelection.Length > 0 ? beforeSelection.Start : afterSelection.Start;
            return new MarkdownEditRecord(
                start,
                removedLength > 0 ? before.Substring(start, removedLength) : string.Empty,
                string.Empty,
                beforeSelection,
                afterSelection,
                kind);
        }

        var prefix = 0;
        while (prefix < before.Length && prefix < after.Length && before[prefix] == after[prefix])
            prefix++;

        var beforeEnd = before.Length - 1;
        var afterEnd = after.Length - 1;
        while (beforeEnd >= prefix && afterEnd >= prefix && before[beforeEnd] == after[afterEnd])
        {
            beforeEnd--;
            afterEnd--;
        }

        var removed = beforeEnd - prefix + 1;
        var inserted = afterEnd - prefix + 1;
        return new MarkdownEditRecord(
            prefix,
            removed > 0 ? before.Substring(prefix, removed) : string.Empty,
            inserted > 0 ? after.Substring(prefix, inserted) : string.Empty,
            beforeSelection,
            afterSelection,
            kind);
    }

    private MarkdownSelection GetSurfaceSelection()
    {
        var length = editorSurface.SelectionLength;
        var start = length > 0
            ? Math.Min(editorSurface.SelectionStart, editorSurface.SelectionEnd)
            : editorSurface.document.CursorIndex;
        return new MarkdownSelection(Math.Clamp(start, 0, Markdown.Length), length);
    }

    private void ReplaceRange(int start, int length, string replacement, int selectionStart, int selectionLength)
    {
        editorSurface.Select(start, length);
        editorSurface.SelectedText = replacement;
        editorSurface.Select(selectionStart, selectionLength);
    }

    private void SetModified(bool value, bool moveCleanMarker)
    {
        if (moveCleanMarker)
            cleanHistoryPosition = value ? -1 : history.Position;
        if (modified == value)
            return;

        modified = value;
        OnModifiedChanged(EventArgs.Empty);
    }

    private void UpdateModifiedFromHistory()
        => SetModified(cleanHistoryPosition < 0 || history.Position != cleanHistoryPosition, moveCleanMarker: false);
}
