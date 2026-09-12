using System;

namespace ModernFormsNext;

partial class TextBoxDocument
{
    // Allocated only when an accessibility text provider is first requested. A document with no
    // text-range clients pays no journal allocation. Entries never contain text or owner objects.
    private AccessibleEditJournal? accessibleEdits;
    internal AccessibleEditJournal AccessibilityEdits => accessibleEdits ??= new();

    private void RecordAccessibleTextEdit(int start, int removed, int inserted)
    {
        if (removed != 0 || inserted != 0)
            accessibleEdits?.Record(start, removed, inserted);
    }

    private void RecordAccessibleTextReplacement(string previous, string current)
    {
        if (accessibleEdits is null) return;
        int start = 0;
        while (start < previous.Length && start < current.Length && previous[start] == current[start]) start++;
        int oldEnd = previous.Length, newEnd = current.Length;
        while (oldEnd > start && newEnd > start && previous[oldEnd - 1] == current[newEnd - 1]) {
            oldEnd--; newEnd--;
        }
        RecordAccessibleTextEdit(start, oldEnd - start, newEnd - start);
    }

    internal sealed class AccessibleEditJournal
    {
        internal const int Capacity = 1024;
        private readonly Edit[] entries = new Edit[Capacity];
        internal long Revision { get; private set; }
        internal long Generation { get; private set; }

        internal void Reset()
        {
            Generation++;
            Revision = 0;
            Array.Clear(entries);
        }

        internal void Record(int start, int removed, int inserted)
        {
            long next = checked(Revision + 1);
            entries[(int)((next - 1) % Capacity)] = new(start, removed, inserted);
            Revision = next;
        }

        internal void Rebase(long generation, ref long revision, ref int start, ref int end)
        {
            if (generation != Generation || revision > Revision || Revision - revision > Capacity)
                throw new InvalidOperationException("The accessible text range is stale; obtain a fresh range.");
            for (long next = revision + 1; next <= Revision; next++) {
                var edit = entries[(int)((next - 1) % Capacity)];
                bool collapsed = start == end;
                start = Apply(start, edit, rightAffinity: collapsed);
                end = collapsed ? start : Apply(end, edit, rightAffinity: true);
                if (start > end) start = end;
            }
            revision = Revision;
        }

        private static int Apply(int offset, Edit edit, bool rightAffinity)
        {
            int oldEnd = checked(edit.Start + edit.Removed);
            if (offset < edit.Start) return offset;
            if (offset > oldEnd || edit.Removed > 0 && offset == oldEnd)
                return checked(offset + edit.Inserted - edit.Removed);
            return rightAffinity ? checked(edit.Start + edit.Inserted) : edit.Start;
        }

        private readonly record struct Edit(int Start, int Removed, int Inserted);
    }
}
