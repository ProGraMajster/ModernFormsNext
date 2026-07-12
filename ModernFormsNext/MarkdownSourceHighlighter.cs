using System;
using System.Collections.Generic;

namespace ModernFormsNext;

internal enum MarkdownSourceSpanKind
{
    HeadingMarker,
    EmphasisMarker,
    CodeMarker,
    QuoteMarker,
    ListMarker,
    LinkText,
    LinkTarget,
    ImageMarker
}

internal readonly record struct MarkdownSourceSpan(int Start, int Length, MarkdownSourceSpanKind Kind)
{
    public int End => Start + Length;
}

/// <summary>
/// Performs lightweight, source-preserving Markdown tokenization for editor presentation.
/// </summary>
internal sealed class MarkdownSourceHighlighter
{
    public IReadOnlyList<MarkdownSourceSpan> Highlight(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var spans = new List<MarkdownSourceSpan>();
        var lineStart = 0;
        var inFence = false;
        var fenceCharacter = '\0';

        while (lineStart < source.Length)
        {
            var lineEnd = source.IndexOf('\n', lineStart);
            if (lineEnd < 0)
                lineEnd = source.Length;

            var contentEnd = lineEnd > lineStart && source[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;
            var first = SkipSpaces(source, lineStart, contentEnd, 3);
            var fenceLength = CountRepeated(source, first, contentEnd, first < contentEnd ? source[first] : '\0');
            var isFence = first < contentEnd
                && (source[first] == '`' || source[first] == '~')
                && fenceLength >= 3;

            if (isFence && (!inFence || source[first] == fenceCharacter))
            {
                Add(spans, first, contentEnd - first, MarkdownSourceSpanKind.CodeMarker, source.Length);
                inFence = !inFence;
                fenceCharacter = inFence ? source[first] : '\0';
            }
            else if (!inFence)
            {
                HighlightLinePrefix(source, lineStart, contentEnd, first, spans);
                HighlightInline(source, lineStart, contentEnd, spans);
            }

            lineStart = lineEnd < source.Length ? lineEnd + 1 : source.Length;
        }

        return spans;
    }

    private static void HighlightLinePrefix(
        string source,
        int lineStart,
        int lineEnd,
        int first,
        List<MarkdownSourceSpan> spans)
    {
        if (first >= lineEnd)
            return;

        var headingLength = CountRepeated(source, first, lineEnd, '#');
        if (headingLength is >= 1 and <= 6
            && (first + headingLength == lineEnd || char.IsWhiteSpace(source[first + headingLength])))
        {
            Add(spans, first, headingLength, MarkdownSourceSpanKind.HeadingMarker, source.Length);
            return;
        }

        if (source[first] == '>')
        {
            Add(spans, first, Math.Min(2, lineEnd - first), MarkdownSourceSpanKind.QuoteMarker, source.Length);
            return;
        }

        if (IsHorizontalRule(source, first, lineEnd))
        {
            Add(spans, first, lineEnd - first, MarkdownSourceSpanKind.EmphasisMarker, source.Length);
            return;
        }

        var markerLength = GetListMarkerLength(source, first, lineEnd);
        if (markerLength > 0)
            Add(spans, first, markerLength, MarkdownSourceSpanKind.ListMarker, source.Length);
    }

    private static void HighlightInline(string source, int start, int end, List<MarkdownSourceSpan> spans)
    {
        for (var index = start; index < end; index++)
        {
            if (source[index] == '`')
            {
                var close = source.IndexOf('`', index + 1, end - index - 1);
                if (close >= 0)
                {
                    Add(spans, index, close - index + 1, MarkdownSourceSpanKind.CodeMarker, source.Length);
                    index = close;
                }

                continue;
            }

            var isImage = source[index] == '!' && index + 1 < end && source[index + 1] == '[';
            if (source[index] == '[' || isImage)
            {
                var labelStart = isImage ? index + 2 : index + 1;
                var closeLabel = source.IndexOf(']', labelStart, end - labelStart);
                if (closeLabel >= 0 && closeLabel + 1 < end && source[closeLabel + 1] == '(')
                {
                    var closeTarget = source.IndexOf(')', closeLabel + 2, end - closeLabel - 2);
                    if (closeTarget >= 0)
                    {
                        if (isImage)
                            Add(spans, index, 2, MarkdownSourceSpanKind.ImageMarker, source.Length);
                        else
                            Add(spans, index, 1, MarkdownSourceSpanKind.LinkText, source.Length);

                        Add(spans, labelStart, closeLabel - labelStart, MarkdownSourceSpanKind.LinkText, source.Length);
                        Add(spans, closeLabel, 2, isImage ? MarkdownSourceSpanKind.ImageMarker : MarkdownSourceSpanKind.LinkText, source.Length);
                        Add(spans, closeLabel + 2, closeTarget - closeLabel - 2, MarkdownSourceSpanKind.LinkTarget, source.Length);
                        Add(spans, closeTarget, 1, isImage ? MarkdownSourceSpanKind.ImageMarker : MarkdownSourceSpanKind.LinkTarget, source.Length);
                        index = closeTarget;
                        continue;
                    }
                }
            }

            if (source[index] is '*' or '_' or '~')
            {
                var marker = source[index];
                var markerLength = index + 1 < end && source[index + 1] == marker ? 2 : 1;
                if (marker == '~' && markerLength == 1)
                    continue;

                var close = FindRepeated(source, marker, markerLength, index + markerLength, end);
                if (close >= 0 && close < end)
                {
                    Add(spans, index, markerLength, MarkdownSourceSpanKind.EmphasisMarker, source.Length);
                    Add(spans, close, markerLength, MarkdownSourceSpanKind.EmphasisMarker, source.Length);
                    index += markerLength - 1;
                }
            }
        }
    }

    private static int GetListMarkerLength(string source, int start, int end)
    {
        if (source[start] is '-' or '*' or '+')
        {
            if (start + 1 >= end || !char.IsWhiteSpace(source[start + 1]))
                return 0;

            if (start + 4 < end
                && source[start + 2] == '['
                && source[start + 3] is ' ' or 'x' or 'X'
                && source[start + 4] == ']')
            {
                return Math.Min(6, end - start);
            }

            return Math.Min(2, end - start);
        }

        var index = start;
        while (index < end && char.IsDigit(source[index]))
            index++;

        return index > start
            && index + 1 < end
            && source[index] is '.' or ')'
            && char.IsWhiteSpace(source[index + 1])
                ? index + 2 - start
                : 0;
    }

    private static bool IsHorizontalRule(string source, int start, int end)
    {
        var marker = source[start];
        if (marker is not ('-' or '*' or '_'))
            return false;

        var count = 0;
        for (var index = start; index < end; index++)
        {
            if (source[index] == marker)
                count++;
            else if (!char.IsWhiteSpace(source[index]))
                return false;
        }

        return count >= 3;
    }

    private static int SkipSpaces(string source, int start, int end, int maximum)
    {
        var index = start;
        while (index < end && index - start < maximum && source[index] == ' ')
            index++;

        return index;
    }

    private static int CountRepeated(string source, int start, int end, char value)
    {
        var index = start;
        while (index < end && source[index] == value)
            index++;

        return index - start;
    }

    private static int FindRepeated(string source, char value, int count, int start, int end)
    {
        for (var index = start; index + count <= end; index++)
        {
            var matched = true;
            for (var offset = 0; offset < count; offset++)
            {
                if (source[index + offset] == value)
                    continue;

                matched = false;
                break;
            }

            if (matched)
                return index;
        }

        return -1;
    }

    private static void Add(
        List<MarkdownSourceSpan> spans,
        int start,
        int length,
        MarkdownSourceSpanKind kind,
        int sourceLength)
    {
        if (length <= 0 || start < 0 || start >= sourceLength)
            return;

        spans.Add(new MarkdownSourceSpan(start, Math.Min(length, sourceLength - start), kind));
    }
}
