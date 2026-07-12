using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModernFormsNext;

public partial class MarkdownEditor
{
    /// <summary>
    /// Decreases indentation for every selected line.
    /// </summary>
    public void Outdent() => TransformIndent(outdent: true);

    /// <summary>
    /// Adds one tab of indentation to every selected line.
    /// </summary>
    public void Indent() => TransformIndent(outdent: false);

    /// <summary>
    /// Inserts or removes block-quote markers on selected lines.
    /// </summary>
    public void ToggleBlockQuote() => ToggleSimpleLinePrefix("> ");

    /// <summary>
    /// Converts selected lines to, or removes, sequential ordered-list markers.
    /// </summary>
    /// <remarks>New markers are numbered sequentially from 1 for the selected range.</remarks>
    public void ToggleOrderedList()
    {
        ToggleListLines(
            line => TryGetOrderedMarker(line, out _),
            (line, index) => ReplaceListMarker(line, $"{index + 1}. "));
    }

    /// <summary>
    /// Converts selected lines to, or removes, unchecked task-list markers.
    /// </summary>
    public void ToggleTaskList()
    {
        ToggleListLines(
            line => TryGetTaskMarker(line, out _),
            (line, _) => ReplaceListMarker(line, "- [ ] "));
    }

    /// <summary>
    /// Converts selected lines to, or removes, unordered-list markers.
    /// </summary>
    public void ToggleUnorderedList()
    {
        ToggleListLines(
            line => TryGetUnorderedMarker(line, out _),
            (line, _) => ReplaceListMarker(line, "- "));
    }

    /// <summary>
    /// Inserts or toggles an ATX heading marker on selected lines.
    /// </summary>
    /// <param name="level">The heading level from 1 through 6.</param>
    public void InsertHeading(int level)
    {
        if (level is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(level), "Heading level must be between 1 and 6.");

        var desired = new string('#', level) + " ";
        TransformSelectedLines(lines =>
        {
            var applicable = lines.Where(line => line.Length > 0).ToArray();
            var allDesired = applicable.Length > 0 && applicable.All(line => HasHeadingPrefix(line, desired));
            return lines.Select(line => allDesired ? RemoveHeadingPrefix(line) : ReplaceHeadingPrefix(line, desired)).ToArray();
        });
    }

    internal bool TryHandleMarkdownEnter()
    {
        if (ReadOnly || !AcceptsReturn)
            return false;

        var selection = GetSurfaceSelection();
        var line = GetLineAt(selection.Start);
        if (!TryGetContinuation(line.Text, out var markerStart, out var markerLength, out var nextMarker))
            return false;

        ExecuteCommand(() =>
        {
            var contentStart = markerStart + markerLength;
            if (line.Text.AsSpan(contentStart).Trim().Length == 0 && line.CaretOffset >= contentStart)
            {
                ReplaceRange(line.Start + markerStart, markerLength, string.Empty, line.Start + markerStart, 0);
                return;
            }

            var replacement = GetPreferredNewLine() + line.Text.Substring(0, markerStart) + nextMarker;
            ReplaceRange(
                selection.Start,
                selection.Length,
                replacement,
                selection.Start + replacement.Length,
                0);
        });
        return true;
    }

    internal bool TryHandleMarkdownBackspace()
    {
        if (ReadOnly || SelectionLength != 0)
            return false;

        var selection = GetSurfaceSelection();
        var line = GetLineAt(selection.Start);
        if (!TryGetRemovablePrefix(line.Text, out var markerStart, out var markerLength)
            || line.CaretOffset != markerStart + markerLength)
        {
            return false;
        }

        ExecuteCommand(() => ReplaceRange(
            line.Start + markerStart,
            markerLength,
            string.Empty,
            line.Start + markerStart,
            0));
        return true;
    }

    internal bool TryHandleListIndent(bool outdent)
    {
        if (ReadOnly || !AcceptsTab || !IsSelectionInListContext())
            return false;

        TransformIndent(outdent);
        return true;
    }

    private void TransformIndent(bool outdent)
    {
        if (ReadOnly)
            return;

        ExecuteCommand(() =>
        {
            var selection = GetSurfaceSelection();
            var range = GetSelectedLineRange();
            var segment = Markdown.Substring(range.Start, range.Length);
            var lines = SplitLines(segment, out var endings);
            var transformed = new string[lines.Count];
            var changes = new List<LineIndentChange>(lines.Count);
            var oldLineStart = 0;

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var removed = outdent ? GetIndentRemovalLength(line) : 0;
                var inserted = outdent ? 0 : 1;
                transformed[index] = outdent ? line.Substring(removed) : "\t" + line;
                changes.Add(new LineIndentChange(oldLineStart, removed, inserted));
                oldLineStart += line.Length + (index < endings.Count ? endings[index].Length : 0);
            }

            var replacement = JoinLines(transformed, endings);
            var mappedStart = MapPositionThroughIndentChanges(
                selection.Start,
                range.Start,
                selection.Length == 0,
                changes);
            var mappedEnd = MapPositionThroughIndentChanges(
                selection.Start + selection.Length,
                range.Start,
                caretOrEnd: true,
                changes);

            ReplaceRange(
                range.Start,
                range.Length,
                replacement,
                mappedStart,
                Math.Max(0, mappedEnd - mappedStart));
        });
    }

    private bool IsSelectionInListContext()
    {
        var range = GetSelectedLineRange();
        var lines = SplitLines(Markdown.Substring(range.Start, range.Length), out _);
        var found = false;

        foreach (var line in lines)
        {
            if (line.Length == 0)
                continue;

            found = true;
            if (!TryGetAnyListMarker(line, out _, out _))
                return false;
        }

        return found;
    }

    private static int MapPositionThroughIndentChanges(
        int position,
        int rangeStart,
        bool caretOrEnd,
        IReadOnlyList<LineIndentChange> changes)
    {
        var relative = position - rangeStart;
        var delta = 0;

        foreach (var change in changes)
        {
            if (relative < change.Start)
                break;

            if (change.RemovedLength > 0)
            {
                if (relative <= change.Start + change.RemovedLength)
                    return rangeStart + change.Start + delta;

                delta -= change.RemovedLength;
                continue;
            }

            if (relative > change.Start || caretOrEnd)
                delta += change.InsertedLength;
        }

        return position + delta;
    }

    private void ToggleListLines(Func<string, bool> isDesired, Func<string, int, string> addDesired)
    {
        TransformSelectedLines(lines =>
        {
            var applicable = lines.Where(line => line.Length > 0).ToArray();
            var remove = applicable.Length > 0 && applicable.All(isDesired);
            return lines.Select((line, index) => remove ? RemoveListMarker(line) : addDesired(line, index)).ToArray();
        });
    }

    private void ToggleSimpleLinePrefix(string prefix)
    {
        TransformSelectedLines(lines =>
        {
            var applicable = lines.Where(line => line.Length > 0).ToArray();
            var remove = applicable.Length > 0
                && applicable.All(line => line.AsSpan(CountLeadingSpaces(line)).StartsWith(prefix, StringComparison.Ordinal));
            return lines.Select(line =>
            {
                var indent = CountLeadingSpaces(line);
                return remove
                    ? line.Remove(indent, Math.Min(prefix.Length, line.Length - indent))
                    : line.Insert(indent, prefix);
            }).ToArray();
        });
    }

    private void TransformSelectedLines(Func<IReadOnlyList<string>, IReadOnlyList<string>> transform)
    {
        if (ReadOnly)
            return;

        ExecuteCommand(() =>
        {
            var range = GetSelectedLineRange();
            var segment = Markdown.Substring(range.Start, range.Length);
            var lines = SplitLines(segment, out var endings);
            var replacement = JoinLines(transform(lines), endings);
            ReplaceRange(range.Start, range.Length, replacement, range.Start, replacement.Length);
        });
    }

    private (int Start, int Length) GetSelectedLineRange()
    {
        var selection = GetSurfaceSelection();
        var start = selection.Start > 0
            ? Markdown.LastIndexOf('\n', selection.Start - 1) + 1
            : 0;
        var selectionEnd = selection.Start + selection.Length;
        if (selection.Length > 0 && selectionEnd > start && Markdown[selectionEnd - 1] == '\n')
            selectionEnd--;

        var lineBreak = Markdown.IndexOf('\n', selectionEnd);
        var end = lineBreak < 0 ? Markdown.Length : lineBreak;
        if (end > start && Markdown[end - 1] == '\r')
            end--;
        return (start, Math.Max(0, end - start));
    }

    private MarkdownLineContext GetLineAt(int position)
    {
        var source = Markdown;
        var start = position > 0 ? source.LastIndexOf('\n', position - 1) + 1 : 0;
        var lineBreak = source.IndexOf('\n', position);
        var end = lineBreak < 0 ? source.Length : lineBreak;
        if (end > start && source[end - 1] == '\r')
            end--;

        return new MarkdownLineContext(start, end, source.Substring(start, end - start), position - start);
    }

    private string GetPreferredNewLine()
        => Markdown.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static bool TryGetContinuation(
        string line,
        out int markerStart,
        out int markerLength,
        out string nextMarker)
    {
        markerStart = CountLeadingSpaces(line);

        if (TryGetTaskMarker(line, out markerLength))
        {
            nextMarker = $"{line[markerStart]} [ ] ";
            return true;
        }

        if (TryGetUnorderedMarker(line, out markerLength))
        {
            nextMarker = line.Substring(markerStart, markerLength);
            return true;
        }

        if (TryGetOrderedMarker(line, out markerLength))
        {
            var marker = line.AsSpan(markerStart, markerLength);
            var delimiter = marker[^2];
            var digits = marker[..^2];
            if (!long.TryParse(digits, out var number) || number == long.MaxValue)
                number = 0;
            nextMarker = $"{number + 1}{delimiter} ";
            return true;
        }

        var rest = line.AsSpan(markerStart);
        if (rest.StartsWith("> ", StringComparison.Ordinal))
        {
            markerLength = 2;
            nextMarker = "> ";
            return true;
        }

        markerLength = 0;
        nextMarker = string.Empty;
        return false;
    }

    private static bool TryGetRemovablePrefix(string line, out int markerStart, out int markerLength)
    {
        markerStart = CountLeadingSpaces(line);
        if (TryGetAnyListMarker(line, out _, out markerLength))
            return true;

        var rest = line.AsSpan(markerStart);
        if (rest.StartsWith("> ", StringComparison.Ordinal))
        {
            markerLength = 2;
            return true;
        }

        var hashes = 0;
        while (hashes < rest.Length && hashes < 6 && rest[hashes] == '#')
            hashes++;
        if (hashes > 0 && hashes < rest.Length && rest[hashes] == ' ')
        {
            markerLength = hashes + 1;
            return true;
        }

        markerLength = 0;
        return false;
    }

    private static bool HasHeadingPrefix(string line, string desired)
    {
        var indent = CountLeadingSpaces(line);
        return line.AsSpan(indent).StartsWith(desired, StringComparison.Ordinal);
    }

    private static string RemoveHeadingPrefix(string line)
    {
        var indent = CountLeadingSpaces(line);
        var index = indent;
        while (index < line.Length && line[index] == '#' && index - indent < 6)
            index++;
        if (index == indent)
            return line;
        if (index < line.Length && line[index] == ' ')
            index++;
        return line.Remove(indent, index - indent);
    }

    private static string ReplaceHeadingPrefix(string line, string desired)
    {
        var without = RemoveHeadingPrefix(line);
        return without.Insert(CountLeadingSpaces(without), desired);
    }

    private static int CountLeadingSpaces(string line)
    {
        var index = 0;
        while (index < line.Length && line[index] is ' ' or '\t')
            index++;
        return index;
    }

    private static int GetIndentRemovalLength(string line)
    {
        if (line.StartsWith('\t'))
            return 1;

        var count = 0;
        while (count < line.Length && count < 4 && line[count] == ' ')
            count++;
        return count;
    }

    private static IReadOnlyList<string> SplitLines(string segment, out IReadOnlyList<string> endings)
    {
        var lines = new List<string>();
        var separators = new List<string>();
        var start = 0;
        for (var index = 0; index < segment.Length; index++)
        {
            if (segment[index] != '\n')
                continue;

            var contentEnd = index > start && segment[index - 1] == '\r' ? index - 1 : index;
            lines.Add(segment.Substring(start, contentEnd - start));
            separators.Add(segment.Substring(contentEnd, index - contentEnd + 1));
            start = index + 1;
        }

        lines.Add(segment.Substring(start));
        endings = separators;
        return lines;
    }

    private static string JoinLines(IReadOnlyList<string> lines, IReadOnlyList<string> endings)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            builder.Append(lines[index]);
            if (index < endings.Count)
                builder.Append(endings[index]);
        }
        return builder.ToString();
    }

    private static string ReplaceListMarker(string line, string marker)
    {
        var without = RemoveListMarker(line);
        return without.Insert(CountLeadingSpaces(without), marker);
    }

    private static string RemoveListMarker(string line)
    {
        if (!TryGetAnyListMarker(line, out var start, out var length))
            return line;
        return line.Remove(start, length);
    }

    private static bool TryGetAnyListMarker(string line, out int start, out int length)
    {
        if (TryGetTaskMarker(line, out length)
            || TryGetUnorderedMarker(line, out length)
            || TryGetOrderedMarker(line, out length))
        {
            start = CountLeadingSpaces(line);
            return true;
        }

        start = 0;
        length = 0;
        return false;
    }

    private static bool TryGetTaskMarker(string line, out int length)
    {
        var start = CountLeadingSpaces(line);
        var rest = line.AsSpan(start);
        if (rest.Length >= 6
            && rest[0] is '-' or '*' or '+'
            && rest[1] == ' '
            && rest[2] == '['
            && rest[3] is ' ' or 'x' or 'X'
            && rest[4] == ']'
            && rest[5] == ' ')
        {
            length = 6;
            return true;
        }

        length = 0;
        return false;
    }

    private static bool TryGetUnorderedMarker(string line, out int length)
    {
        var start = CountLeadingSpaces(line);
        var rest = line.AsSpan(start);
        if (rest.Length >= 2 && rest[0] is '-' or '*' or '+' && rest[1] == ' ' && !TryGetTaskMarker(line, out _))
        {
            length = 2;
            return true;
        }

        length = 0;
        return false;
    }

    private static bool TryGetOrderedMarker(string line, out int length)
    {
        var start = CountLeadingSpaces(line);
        var index = start;
        while (index < line.Length && char.IsDigit(line[index]))
            index++;
        if (index > start && index + 1 < line.Length && line[index] is '.' or ')' && line[index + 1] == ' ')
        {
            length = index + 2 - start;
            return true;
        }

        length = 0;
        return false;
    }

    private readonly record struct MarkdownLineContext(int Start, int End, string Text, int CaretOffset);

    private readonly record struct LineIndentChange(int Start, int RemovedLength, int InsertedLength);
}
