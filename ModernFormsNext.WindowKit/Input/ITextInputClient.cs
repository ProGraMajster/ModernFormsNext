namespace ModernFormsNext.WindowKit.Input;

/// <summary>Exposes an existing editor to native text services without owning its document or focus.</summary>
/// <remarks>
/// All members require the owning UI thread. Hosts lend a revocable instance for one focus session;
/// retain that instance rather than resolving a new focus target for late native callbacks. A retired
/// instance returns null state and false for edits. Implementations reject nested editing operations
/// instead of queuing unbounded native input. No method dispatches command shortcuts.
/// </remarks>
public interface ITextInputClient
{
    /// <summary>Occurs after text, ranges, options or caret geometry have changed.</summary>
    event EventHandler? StateChanged;

    /// <summary>Occurs after a semantic composition transition; contains no entered text.</summary>
    event EventHandler<TextCompositionEventArgs>? CompositionChanged;

    /// <summary>Gets one bounded, detached snapshot, or null after session retirement.</summary>
    /// <param name="maximumTextLength">Maximum UTF-16 units, from zero through TextInputState.MaximumTextLength.</param>
    /// <param name="textStart">Optional absolute document offset; null selects a window around the caret.</param>
    /// <returns>A snapshot whose actual TextStart may move to avoid splitting a surrogate pair.</returns>
    TextInputState? GetState(int maximumTextLength = 4096, int? textStart = null);

    /// <summary>Replaces the composition or selection with committed text and ends composition.</summary>
    /// <param name="text">Complete Unicode text, including an empty replacement.</param>
    /// <param name="newCursorPosition">Positive offsets are relative to insertion end minus one; other offsets to insertion start.</param>
    /// <returns>Whether the current editable client accepted the operation.</returns>
    bool CommitText(string text, int newCursorPosition = 1);

    /// <summary>Starts or replaces provisional text using the same document editing operations.</summary>
    /// <param name="text">Complete current provisional Unicode text.</param>
    /// <param name="newCursorPosition">Positive offsets are relative to insertion end minus one; other offsets to insertion start.</param>
    /// <returns>Whether the current editable client accepted the operation.</returns>
    bool SetComposingText(string text, int newCursorPosition = 1);

    /// <summary>Marks a clipped absolute UTF-16 range without moving selection or changing text.</summary>
    /// <param name="start">First edge of the range.</param>
    /// <param name="end">Other edge of the range.</param>
    /// <returns>Whether the current editable client accepted the operation.</returns>
    bool SetComposingRegion(int start, int end);

    /// <summary>Sets oriented absolute UTF-16 selection edges within the document.</summary>
    /// <param name="start">Selection anchor.</param>
    /// <param name="end">Selection caret.</param>
    /// <returns>Whether the current client accepted the operation.</returns>
    bool SetSelection(int start, int end);

    /// <summary>Accepts the visible provisional text and drops composition markers.</summary>
    /// <returns>Whether the current client accepted the operation.</returns>
    bool FinishComposition();

    /// <summary>Restores the original replaced fragment and selection when its checkpoint is still current.</summary>
    /// <returns>Whether the current client accepted the operation.</returns>
    /// <remarks>An obsolete checkpoint must never overwrite a later application edit.</remarks>
    bool CancelComposition();

    /// <summary>Deletes around, but not through, the current selection using the editor's Unicode-safe deletion.</summary>
    /// <param name="beforeLength">Nonnegative requested units before selection; may expand to preserve a complete text element.</param>
    /// <param name="afterLength">Nonnegative requested units after selection; may expand to preserve a complete text element.</param>
    /// <param name="inCodePoints">True for Unicode scalar counts; false for UTF-16 unit counts.</param>
    /// <returns>Whether the current editable client accepted the operation.</returns>
    bool DeleteSurroundingText(int beforeLength, int afterLength, bool inCodePoints = false);

    /// <summary>Performs an editor action, or returns false when the host should provide its normal fallback.</summary>
    /// <param name="action">Semantic action requested by the keyboard.</param>
    /// <returns>Whether the action was handled.</returns>
    bool PerformEditorAction(TextInputAction action);
}
