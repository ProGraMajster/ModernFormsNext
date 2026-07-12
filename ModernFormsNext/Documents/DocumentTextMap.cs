using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ModernFormsNext.Documents;

// Selection offsets are UTF-16 string offsets. RichTextKit uses code-point offsets, so conversion
// is deliberately isolated here instead of leaking a second indexing convention into the viewer.
internal sealed class DocumentTextMap
{
    public static DocumentTextMap Empty { get; } = new(string.Empty, Array.Empty<DocumentTextLayoutElement>());

    public DocumentTextMap(string text, IReadOnlyList<DocumentTextLayoutElement> elements)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Elements = elements ?? throw new ArgumentNullException(nameof(elements));
    }

    public IReadOnlyList<DocumentTextLayoutElement> Elements { get; }

    public int Length => Text.Length;

    public string Text { get; }

    public string GetText(int start, int length)
    {
        start = Math.Clamp(start, 0, Text.Length);
        length = Math.Clamp(length, 0, Text.Length - start);
        return length == 0 ? string.Empty : Text.Substring(start, length);
    }

    public int HitTest(Point documentPoint)
    {
        if (Elements.Count == 0)
            return 0;

        DocumentTextLayoutElement? nearest = null;
        var nearestDistance = long.MaxValue;

        foreach (var element in Elements)
        {
            if (element.Bounds.Contains(documentPoint))
                return HitTestElement(element, documentPoint);

            var dx = documentPoint.X < element.Bounds.Left
                ? element.Bounds.Left - documentPoint.X
                : documentPoint.X > element.Bounds.Right
                    ? documentPoint.X - element.Bounds.Right
                    : 0;
            var dy = documentPoint.Y < element.Bounds.Top
                ? element.Bounds.Top - documentPoint.Y
                : documentPoint.Y > element.Bounds.Bottom
                    ? documentPoint.Y - element.Bounds.Bottom
                    : 0;
            var distance = ((long)dx * dx) + ((long)dy * dy);

            if (distance < nearestDistance)
            {
                nearest = element;
                nearestDistance = distance;
            }
        }

        if (nearest is null)
            return 0;

        if (documentPoint.Y < nearest.Bounds.Top)
            return nearest.DocumentTextStart;

        if (documentPoint.Y > nearest.Bounds.Bottom)
            return nearest.DocumentTextStart + nearest.DocumentTextLength;

        return HitTestElement(nearest, documentPoint);
    }

    public TextSelection GetSelection(DocumentTextLayoutElement element, int selectionStart, int selectionLength, SkiaSharp.SKColor color)
    {
        var selectionEnd = selectionStart + selectionLength;
        var elementStart = element.DocumentTextStart;
        var elementEnd = elementStart + element.DocumentTextLength;
        var start = Math.Max(selectionStart, elementStart);
        var end = Math.Min(selectionEnd, elementEnd);

        if (start >= end)
            return TextSelection.Empty;

        var localStart = Utf16ToCodePointIndex(element.Text, start - elementStart);
        var localEnd = Utf16ToCodePointIndex(element.Text, end - elementStart);
        return new TextSelection(localStart, localEnd, color);
    }

    internal static int CodePointToUtf16Index(string text, int codePointIndex)
    {
        var utf16Index = 0;
        var currentCodePoint = 0;

        while (utf16Index < text.Length && currentCodePoint < codePointIndex)
        {
            utf16Index += char.IsHighSurrogate(text[utf16Index])
                && utf16Index + 1 < text.Length
                && char.IsLowSurrogate(text[utf16Index + 1])
                    ? 2
                    : 1;
            currentCodePoint++;
        }

        return utf16Index;
    }

    internal static int Utf16ToCodePointIndex(string text, int utf16Index)
    {
        utf16Index = Math.Clamp(utf16Index, 0, text.Length);
        var codePointIndex = 0;
        var current = 0;

        while (current < utf16Index)
        {
            current += char.IsHighSurrogate(text[current])
                && current + 1 < text.Length
                && char.IsLowSurrogate(text[current + 1])
                    ? 2
                    : 1;
            codePointIndex++;
        }

        return codePointIndex;
    }

    private static int HitTestElement(DocumentTextLayoutElement element, Point documentPoint)
    {
        var x = documentPoint.X - element.TextOrigin.X;
        var y = documentPoint.Y - element.TextOrigin.Y;
        var hit = element.TextBlock.HitTest(x, y);
        var localCodePoint = hit.IsNone ? 0 : hit.ClosestCodePointIndex;
        var localUtf16 = CodePointToUtf16Index(element.Text, localCodePoint);
        return element.DocumentTextStart + Math.Clamp(localUtf16, 0, element.DocumentTextLength);
    }
}

internal sealed class DocumentTextMapBuilder
{
    private readonly StringBuilder text = new();
    private readonly List<DocumentTextLayoutElement> elements = new();

    public int Length => text.Length;

    public int Append(string value)
    {
        var start = text.Length;
        text.Append(value);
        return start;
    }

    public void AddElement(DocumentTextLayoutElement element)
        => elements.Add(element);

    public DocumentTextMap Build()
        => text.Length == 0 && elements.Count == 0
            ? DocumentTextMap.Empty
            : new DocumentTextMap(text.ToString(), elements.ToArray());

    public void EnsureBlockSeparation()
    {
        if (text.Length == 0)
            return;

        if (text[^1] != '\n')
            text.Append("\n\n");
        else if (text.Length == 1 || text[^2] != '\n')
            text.Append('\n');
    }

    public void EnsureLineBreak()
    {
        if (text.Length > 0 && text[^1] != '\n')
            text.Append('\n');
    }
}
