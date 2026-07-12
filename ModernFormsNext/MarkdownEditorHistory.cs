using System;
using System.Collections.Generic;

namespace ModernFormsNext;

internal enum MarkdownEditKind
{
    Typing,
    Delete,
    Command
}

internal readonly record struct MarkdownSelection(int Start, int Length);

internal sealed record MarkdownEditRecord(
    int Start,
    string RemovedText,
    string InsertedText,
    MarkdownSelection BeforeSelection,
    MarkdownSelection AfterSelection,
    MarkdownEditKind Kind);

internal sealed class MarkdownEditorHistory
{
    private readonly List<MarkdownEditRecord> records = new();

    public bool CanRedo => Position < records.Count;

    public bool CanUndo => Position > 0;

    public int Position { get; private set; }

    public void Clear()
    {
        records.Clear();
        Position = 0;
    }

    public void Push(MarkdownEditRecord record, bool allowTypingMerge)
    {
        if (Position < records.Count)
            records.RemoveRange(Position, records.Count - Position);

        if (allowTypingMerge && TryMergeTyping(record))
            return;

        records.Add(record);
        Position = records.Count;
    }

    public MarkdownEditRecord TakeUndo()
    {
        if (!CanUndo)
            throw new InvalidOperationException("There is no Markdown edit to undo.");

        return records[--Position];
    }

    public MarkdownEditRecord TakeRedo()
    {
        if (!CanRedo)
            throw new InvalidOperationException("There is no Markdown edit to redo.");

        return records[Position++];
    }

    private bool TryMergeTyping(MarkdownEditRecord next)
    {
        if (Position == 0
            || next.Kind != MarkdownEditKind.Typing
            || next.RemovedText.Length != 0)
        {
            return false;
        }

        var previous = records[Position - 1];
        if (previous.Kind != MarkdownEditKind.Typing
            || previous.RemovedText.Length != 0
            || previous.InsertedText.Length >= 4096
            || previous.Start + previous.InsertedText.Length != next.Start
            || previous.AfterSelection.Length != 0
            || next.BeforeSelection.Length != 0)
        {
            return false;
        }

        records[Position - 1] = previous with
        {
            InsertedText = previous.InsertedText + next.InsertedText,
            AfterSelection = next.AfterSelection
        };
        return true;
    }
}
