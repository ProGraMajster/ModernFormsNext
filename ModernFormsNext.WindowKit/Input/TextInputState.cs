namespace ModernFormsNext.WindowKit.Input;

/// <summary>Contains a bounded detached text-service snapshot of an existing document.</summary>
/// <remarks>
/// All ranges are absolute UTF-16 document offsets, including ranges outside Text's bounded slice.
/// CaretRectangle uses logical pixels relative to the client; the host's session proxy transforms
/// it into host client coordinates. Revision describes document text/ranges, not geometry/options.
/// This object intentionally contains user text for native editing and must not be logged.
/// </remarks>
public sealed class TextInputState
{
    /// <summary>Maximum UTF-16 units returned by one native surrounding-text request.</summary>
    public const int MaximumTextLength = 65536;

    /// <summary>Creates an immutable snapshot with absolute document ranges.</summary>
    /// <param name="text">The bounded surrounding-text slice.</param>
    /// <param name="textStart">Absolute offset of the slice.</param>
    /// <param name="documentLength">Complete document length in UTF-16 units.</param>
    /// <param name="selectionStart">Absolute selection anchor.</param>
    /// <param name="selectionEnd">Absolute selection caret.</param>
    /// <param name="compositionStart">Absolute composition start, or -1 when absent.</param>
    /// <param name="compositionEnd">Absolute composition end, or -1 when absent.</param>
    /// <param name="revision">Nonnegative document observation token.</param>
    /// <param name="caretRectangle">Finite logical caret geometry in the coordinate system of the lending client.</param>
    /// <param name="options">Current immutable editor capabilities and hints.</param>
    /// <param name="caretBaseline">Optional finite logical y coordinate of the actual shaped caret line's baseline.</param>
    public TextInputState(string text, int textStart, int documentLength,
        int selectionStart, int selectionEnd, int compositionStart, int compositionEnd,
        long revision, Rect caretRectangle, TextInputOptions options, double? caretBaseline = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(documentLength);
        if (text.Length > MaximumTextLength || textStart < 0 || textStart > documentLength ||
            text.Length > documentLength - textStart)
            throw new ArgumentOutOfRangeException(nameof(textStart), "The bounded slice must fit within the document.");
        if (selectionStart < 0 || selectionEnd < 0 || selectionStart > documentLength || selectionEnd > documentLength)
            throw new ArgumentOutOfRangeException(nameof(selectionStart), "Selection must fit within the document.");
        if (!((compositionStart == -1 && compositionEnd == -1) ||
              (compositionStart >= 0 && compositionEnd >= compositionStart && compositionEnd <= documentLength)))
            throw new ArgumentOutOfRangeException(nameof(compositionStart), "Composition must be absent or ordered within the document.");
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        if (!double.IsFinite(caretRectangle.X) || !double.IsFinite(caretRectangle.Y) ||
            !double.IsFinite(caretRectangle.Width) || !double.IsFinite(caretRectangle.Height) ||
            caretRectangle.Width < 0 || caretRectangle.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(caretRectangle));
        if (!Enum.IsDefined(options.Scope) || !Enum.IsDefined(options.Capitalization) || !Enum.IsDefined(options.Action))
            throw new ArgumentOutOfRangeException(nameof(options));
        if (caretBaseline is { } baseline && !double.IsFinite(baseline))
            throw new ArgumentOutOfRangeException(nameof(caretBaseline));
        Text = text;
        TextStart = textStart;
        DocumentLength = documentLength;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
        CompositionStart = compositionStart;
        CompositionEnd = compositionEnd;
        Revision = revision;
        CaretRectangle = caretRectangle;
        Options = options;
        CaretBaseline = caretBaseline;
    }

    /// <summary>Gets the bounded text slice; do not include it in diagnostics.</summary>
    public string Text { get; }
    /// <summary>Gets the absolute UTF-16 origin of Text.</summary>
    public int TextStart { get; }
    /// <summary>Gets the complete UTF-16 document length.</summary>
    public int DocumentLength { get; }
    /// <summary>Gets the absolute selection anchor.</summary>
    public int SelectionStart { get; }
    /// <summary>Gets the absolute selection caret.</summary>
    public int SelectionEnd { get; }
    /// <summary>Gets the absolute composition start, or -1.</summary>
    public int CompositionStart { get; }
    /// <summary>Gets the absolute composition end, or -1.</summary>
    public int CompositionEnd { get; }
    /// <summary>Gets whether a composition range is present.</summary>
    public bool HasComposition => CompositionStart >= 0;
    /// <summary>Gets the document text/range observation token.</summary>
    public long Revision { get; }
    /// <summary>Gets the logical caret rectangle in the lending client's coordinate system.</summary>
    public Rect CaretRectangle { get; }
    /// <summary>Gets the actual shaped line's logical baseline y coordinate, or null when unavailable.</summary>
    public double? CaretBaseline { get; }
    /// <summary>Gets current editor capabilities and hints.</summary>
    public TextInputOptions Options { get; }

    /// <summary>Returns the same detached state with transformed caret geometry.</summary>
    /// <param name="rectangle">Caret rectangle in the host's logical client coordinates.</param>
    /// <param name="caretBaseline">Optional baseline transformed into the same coordinate system.</param>
    /// <returns>A snapshot sharing the same immutable text slice and options.</returns>
    public TextInputState WithCaretRectangle(Rect rectangle, double? caretBaseline = null) => new(Text, TextStart, DocumentLength,
        SelectionStart, SelectionEnd, CompositionStart, CompositionEnd, Revision, rectangle, Options, caretBaseline);
}
