namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>Provides surrounding text, selection, caret, and composition to an Android IME.</summary>
/// <remarks>
/// Indexes use UTF-16 code units, matching Android's <c>InputConnection</c> contract. The type
/// contains no Android objects so bridge behavior can be tested without an emulator. The revision
/// identifies the framework document snapshot from which the values were read.
/// </remarks>
public readonly struct AndroidTextInputState
{
    /// <summary>Creates an immutable Android input snapshot.</summary>
    /// <param name="text">The complete editable text.</param>
    /// <param name="selectionStart">The inclusive selection start, or the caret offset.</param>
    /// <param name="selectionEnd">The exclusive selection end, or the caret offset.</param>
    /// <param name="compositionStart">The inclusive composition start, or <c>-1</c>.</param>
    /// <param name="compositionEnd">The exclusive composition end, or <c>-1</c>.</param>
    /// <param name="revision">The framework document revision.</param>
    public AndroidTextInputState(
        string text,
        int selectionStart,
        int selectionEnd,
        int compositionStart = -1,
        int compositionEnd = -1,
        long revision = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateRange(text, selectionStart, selectionEnd, nameof(selectionStart));
        if ((compositionStart == -1) != (compositionEnd == -1))
            throw new ArgumentOutOfRangeException(nameof(compositionStart), "Both composition indexes must be -1 when composition is inactive.");
        if (compositionStart != -1)
            ValidateRange(text, compositionStart, compositionEnd, nameof(compositionStart));
        ArgumentOutOfRangeException.ThrowIfNegative(revision);

        Text = text;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
        CompositionStart = compositionStart;
        CompositionEnd = compositionEnd;
        Revision = revision;
    }

    /// <summary>Gets the complete editable text.</summary>
    public string Text { get; }
    /// <summary>Gets the inclusive selection start, or the caret offset.</summary>
    public int SelectionStart { get; }
    /// <summary>Gets the exclusive selection end, or the caret offset.</summary>
    public int SelectionEnd { get; }
    /// <summary>Gets the inclusive composition start, or <c>-1</c> when inactive.</summary>
    public int CompositionStart { get; }
    /// <summary>Gets the exclusive composition end, or <c>-1</c> when inactive.</summary>
    public int CompositionEnd { get; }
    /// <summary>Gets the framework document revision captured with this snapshot.</summary>
    public long Revision { get; }

    /// <summary>Gets up to the requested UTF-16 length immediately before the selection.</summary>
    /// <param name="length">The maximum number of UTF-16 code units to return.</param>
    /// <returns>Text immediately before the selection or caret.</returns>
    public string GetTextBeforeCursor(int length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        var cursor = Math.Min(SelectionStart, SelectionEnd);
        var start = Math.Max(0, cursor - length);
        return Text[start..cursor];
    }

    /// <summary>Gets up to the requested UTF-16 length immediately after the selection.</summary>
    /// <param name="length">The maximum number of UTF-16 code units to return.</param>
    /// <returns>Text immediately after the selection or caret.</returns>
    public string GetTextAfterCursor(int length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        var cursor = Math.Max(SelectionStart, SelectionEnd);
        var end = Math.Min(Text.Length, cursor + length);
        return Text[cursor..end];
    }

    /// <summary>Gets the selected text, or an empty string for a collapsed caret.</summary>
    /// <returns>The selected UTF-16 range.</returns>
    public string GetSelectedText()
    {
        var start = Math.Min(SelectionStart, SelectionEnd);
        return Text.Substring(start, Math.Abs(SelectionEnd - SelectionStart));
    }

    /// <summary>
    /// Converts an Android deletion request expressed in Unicode code points into UTF-16 lengths.
    /// </summary>
    /// <param name="beforeLength">The maximum code-point count before the selection.</param>
    /// <param name="afterLength">The maximum code-point count after the selection.</param>
    /// <returns>A deletion request whose lengths use UTF-16 code units.</returns>
    /// <remarks>
    /// Android exposes both UTF-16 and code-point deletion APIs. ModernFormsNext selection indexes
    /// use UTF-16, so surrogate pairs must be kept intact while converting the latter form.
    /// </remarks>
    public AndroidTextDeletionRequest GetUtf16DeletionForCodePoints(int beforeLength, int afterLength)
    {
        if (beforeLength < 0)
            throw new ArgumentOutOfRangeException(nameof(beforeLength));
        if (afterLength < 0)
            throw new ArgumentOutOfRangeException(nameof(afterLength));

        var selectionStart = Math.Min(SelectionStart, SelectionEnd);
        var beforeStart = selectionStart;
        for (var count = 0; count < beforeLength && beforeStart > 0; count++)
        {
            beforeStart--;
            if (char.IsLowSurrogate(Text[beforeStart]) && beforeStart > 0 && char.IsHighSurrogate(Text[beforeStart - 1]))
                beforeStart--;
        }

        var selectionEnd = Math.Max(SelectionStart, SelectionEnd);
        var afterEnd = selectionEnd;
        for (var count = 0; count < afterLength && afterEnd < Text.Length; count++)
        {
            if (char.IsHighSurrogate(Text[afterEnd]) && afterEnd + 1 < Text.Length && char.IsLowSurrogate(Text[afterEnd + 1]))
                afterEnd += 2;
            else
                afterEnd++;
        }

        return new AndroidTextDeletionRequest(selectionStart - beforeStart, afterEnd - selectionEnd);
    }

    private static void ValidateRange(string text, int start, int end, string parameterName)
    {
        if (start < 0 || end < 0 || start > text.Length || end > text.Length)
            throw new ArgumentOutOfRangeException(parameterName, "Android input indexes must be within the UTF-16 text bounds.");
    }
}
