using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

// One native connection borrows one already-revocable framework client. This adapter never
// resolves focus and owns no text: retiring it releases the client and every native subscription.
internal sealed class AndroidTextInputSession(ITextInputClient initialClient)
{
    private ITextInputClient? client = initialClient ?? throw new ArgumentNullException(nameof(initialClient));
    internal int BatchDepth { get; private set; }
    internal int? ExtractedTextToken { get; private set; }
    internal int ExtractedTextLimit { get; private set; } = 4096;
    internal bool MonitorCursor { get; private set; }
    internal bool CursorUpdatePending { get; private set; }
    private long? extractedTextRevision;
    internal bool IsActive => client is not null;

    internal void Revoke()
    {
        client = null;
        BatchDepth = 0;
        ExtractedTextToken = null;
        MonitorCursor = false;
        CursorUpdatePending = false;
        extractedTextRevision = null;
    }

    internal TextInputState? GetState(int maximumLength = 4096, int? textStart = null)
    {
        var target = client;
        if (target is null) return null;
        var result = target.GetState(Math.Clamp(maximumLength, 0, TextInputState.MaximumTextLength), textStart);
        return ReferenceEquals(target, client) ? result : null;
    }

    internal bool Edit(Func<ITextInputClient, bool> edit)
    {
        var target = client;
        return target is not null && target.GetState(0) is not null &&
            ReferenceEquals(target, client) && edit(target);
    }

    internal bool SelectAll()
    {
        var target = client;
        if (target is null || target.GetState(0) is not { } state || !ReferenceEquals(target, client))
            return false;
        // The bounded snapshot may contain no text at all. Selection uses absolute document
        // length, not a surrounding-text window; a reentrant read must not revive this session.
        return target.SetSelection(0, state.DocumentLength);
    }

    internal bool BeginBatch()
    {
        // Broken IMEs must not overflow the nesting counter or defer synchronization forever.
        if (!IsActive || BatchDepth == 256) return false;
        BatchDepth++;
        return true;
    }

    internal bool EndBatch()
    {
        if (!IsActive || BatchDepth == 0) return false;
        BatchDepth--;
        return BatchDepth > 0;
    }

    internal void MonitorExtractedText(int token, int maximumLength)
    {
        if (!IsActive) return;
        ExtractedTextToken = token;
        ExtractedTextLimit = NormalizeLimit(maximumLength);
        extractedTextRevision = null;
    }

    internal bool ShouldPublishExtractedText(long revision)
        => IsActive && ExtractedTextToken is not null && extractedTextRevision != revision;

    internal void MarkExtractedTextPublished(long revision)
    {
        if (IsActive) extractedTextRevision = revision;
    }

    internal void MarkCursorPublished() => CursorUpdatePending = false;

    internal bool RequestCursorUpdates(int mode)
    {
        // The initial integration reports the insertion marker and selection. Unsupported
        // character/editor/line bounds filters are rejected rather than claimed as implemented.
        const int immediate = 1, monitor = 2, insertionMarkerFilter = 16;
        if (!IsActive || (mode & ~(immediate | monitor | insertionMarkerFilter)) != 0) return false;
        MonitorCursor = (mode & monitor) != 0;
        CursorUpdatePending = (mode & immediate) != 0;
        return true;
    }

    internal string? GetBefore(int length)
    {
        if (length < 0 || GetState(0) is not { } state) return null;
        var cursor = Math.Min(state.SelectionStart, state.SelectionEnd);
        var count = Math.Min(Math.Min(length, TextInputState.MaximumTextLength), cursor);
        return ReadRange(state, cursor - count, cursor, requireComplete: false)?.Text;
    }

    internal string? GetAfter(int length)
    {
        if (length < 0 || GetState(0) is not { } state) return null;
        var cursor = Math.Max(state.SelectionStart, state.SelectionEnd);
        var count = Math.Min(Math.Min(length, TextInputState.MaximumTextLength), state.DocumentLength - cursor);
        return ReadRange(state, cursor, cursor + count, requireComplete: false)?.Text;
    }

    internal string? GetSelected()
    {
        if (GetState(0) is not { } state) return null;
        var start = Math.Min(state.SelectionStart, state.SelectionEnd);
        var end = Math.Max(state.SelectionStart, state.SelectionEnd);
        if (start == end || end - start > TextInputState.MaximumTextLength) return null;
        return ReadRange(state, start, end, requireComplete: true)?.Text;
    }

    internal TextInputState? GetSurrounding(int beforeLength, int afterLength)
    {
        if (beforeLength < 0 || afterLength < 0 || GetState(0) is not { } state) return null;
        var start = Math.Min(state.SelectionStart, state.SelectionEnd);
        var end = Math.Max(state.SelectionStart, state.SelectionEnd);
        var budget = TextInputState.MaximumTextLength - (end - start);
        // Android expects all selected text. A selection larger than our resource limit is
        // explicitly unavailable; reporting clipped selection coordinates would be dishonest.
        if (budget < 0) return null;
        var before = Math.Min(Math.Min(beforeLength, start), budget / 2);
        var after = Math.Min(Math.Min(afterLength, state.DocumentLength - end), budget - before);
        before = Math.Min(Math.Min(beforeLength, start), budget - after);
        var result = ReadRange(state, start - before, end + after, requireComplete: false);
        return result is not null && ContainsSelection(result) ? result : null;
    }

    internal TextInputState? GetExtracted(int maximumLength)
    {
        var state = GetState(NormalizeLimit(maximumLength));
        return state is not null && ContainsSelection(state) ? state : null;
    }

    internal static int NormalizeLimit(int requested)
        => requested <= 0 ? 4096 : Math.Min(requested, TextInputState.MaximumTextLength);

    private TextInputState? ReadRange(TextInputState observed, int start, int end, bool requireComplete)
    {
        var state = GetState(end - start, start);
        if (state is null || state.Revision != observed.Revision ||
            state.SelectionStart != observed.SelectionStart || state.SelectionEnd != observed.SelectionEnd)
            return null;
        var actualStart = Math.Max(start, state.TextStart);
        var actualEnd = Math.Min(end, state.TextStart + state.Text.Length);
        if (actualStart > actualEnd || (requireComplete && (actualStart != start || actualEnd != end))) return null;
        var text = state.Text.Substring(actualStart - state.TextStart, actualEnd - actualStart);
        // Requested lengths use UTF-16, but a bounded response need not split a valid scalar.
        if (text.Length > 0 && char.IsLowSurrogate(text[0])) { actualStart++; text = text[1..]; }
        if (text.Length > 0 && char.IsHighSurrogate(text[^1])) text = text[..^1];
        if (requireComplete && text.Length != end - start) return null;
        return new TextInputState(text, actualStart, state.DocumentLength, state.SelectionStart,
            state.SelectionEnd, state.CompositionStart, state.CompositionEnd, state.Revision,
            state.CaretRectangle, state.Options, state.CaretBaseline);
    }

    private static bool ContainsSelection(TextInputState state)
        => Math.Min(state.SelectionStart, state.SelectionEnd) >= state.TextStart &&
           Math.Max(state.SelectionStart, state.SelectionEnd) <= state.TextStart + state.Text.Length;
}
