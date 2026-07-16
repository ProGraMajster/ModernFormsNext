namespace ModernFormsNext;

/// <summary>
/// Describes editable text, selection, caret, and IME composition for a control hosted by a
/// <see cref="SkiaControlSurface"/>.
/// </summary>
/// <remarks>
/// All indexes are UTF-16 code-unit offsets, matching .NET strings and Android input-connection
/// contracts. A composition range of <c>-1, -1</c> means that no composition is active. The
/// revision is a monotonic observation token owned by the selected text document.
/// </remarks>
public readonly struct ControlSurfaceTextInputState
{
    /// <summary>Creates an immutable text-input snapshot.</summary>
    /// <param name="text">The complete editable text.</param>
    /// <param name="selectionStart">The inclusive UTF-16 selection start, or the caret offset.</param>
    /// <param name="selectionEnd">The exclusive UTF-16 selection end, or the caret offset.</param>
    /// <param name="compositionStart">The inclusive composition start, or <c>-1</c>.</param>
    /// <param name="compositionEnd">The exclusive composition end, or <c>-1</c>.</param>
    /// <param name="revision">The selected document revision.</param>
    public ControlSurfaceTextInputState(
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

    /// <summary>Gets the inclusive UTF-16 selection start, or the caret offset.</summary>
    public int SelectionStart { get; }

    /// <summary>Gets the exclusive UTF-16 selection end, or the caret offset.</summary>
    public int SelectionEnd { get; }

    /// <summary>Gets the inclusive composition start, or <c>-1</c> when inactive.</summary>
    public int CompositionStart { get; }

    /// <summary>Gets the exclusive composition end, or <c>-1</c> when inactive.</summary>
    public int CompositionEnd { get; }

    /// <summary>Gets the selected document revision captured with this snapshot.</summary>
    public long Revision { get; }

    private static void ValidateRange(string text, int start, int end, string parameterName)
    {
        if (start < 0 || end < 0 || start > text.Length || end > text.Length)
            throw new ArgumentOutOfRangeException(parameterName, "Text-input indexes must be within the UTF-16 text bounds.");
    }
}
