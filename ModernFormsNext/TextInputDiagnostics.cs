using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext;

/// <summary>Contains detached text-session metadata without entered text or control references.</summary>
public sealed class TextInputDiagnostics
{
    internal TextInputDiagnostics(long generation, bool active, bool hasClient, bool hasNativeMethod,
        long revision, int selectionStart, int selectionEnd, int compositionStart, int compositionEnd,
        TextInputScope scope, IReadOnlyList<string> recentEvents)
    {
        Generation = generation; IsActive = active; HasClient = hasClient; HasNativeMethod = hasNativeMethod;
        Revision = revision; SelectionStart = selectionStart; SelectionEnd = selectionEnd;
        CompositionStart = compositionStart; CompositionEnd = compositionEnd; Scope = scope; RecentEvents = recentEvents;
    }
    /// <summary>Gets the most recently created host session generation.</summary>
    public long Generation { get; }
    /// <summary>Gets whether the host currently permits text-service ownership.</summary>
    public bool IsActive { get; }
    /// <summary>Gets whether a current canonical focused editor supplies a client.</summary>
    public bool HasClient { get; }
    /// <summary>Gets whether an optional native input method is attached.</summary>
    public bool HasNativeMethod { get; }
    /// <summary>Gets the observed document revision.</summary>
    public long Revision { get; }
    /// <summary>Gets the absolute selection anchor, or -1 without a client.</summary>
    public int SelectionStart { get; }
    /// <summary>Gets the absolute selection caret, or -1 without a client.</summary>
    public int SelectionEnd { get; }
    /// <summary>Gets the absolute composition start, or -1 when absent.</summary>
    public int CompositionStart { get; }
    /// <summary>Gets the absolute composition end, or -1 when absent.</summary>
    public int CompositionEnd { get; }
    /// <summary>Gets the current keyboard scope.</summary>
    public TextInputScope Scope { get; }
    /// <summary>Gets at most 64 event kinds without user text, values or control names.</summary>
    public IReadOnlyList<string> RecentEvents { get; }
}
