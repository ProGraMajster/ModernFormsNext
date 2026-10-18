using System.Collections;
using System.ComponentModel;

namespace ModernFormsNext;

/// <summary>Offers a revocable suggestion request without owning application asynchronous work.</summary>
/// <remarks>
/// Query is an immutable snapshot that may be read by background work. IsCurrent and result
/// publication require the UI thread; post through Application.RunOnUIThread. The request holds
/// only a weak control reference. Query/provider changes, dismissal and host retirement reject
/// old results. Sources and values remain application-owned and are never disposed by the request.
/// </remarks>
public sealed class SearchSuggestionRequestEventArgs : EventArgs
{
    private readonly WeakReference<SearchBar> owner;
    internal long Id { get; }
    internal SearchSuggestionRequestEventArgs(SearchBar owner, long id, string query)
    {
        this.owner = new(owner);
        Id = id;
        Query = query;
    }

    /// <summary>Gets the immutable query that caused this request.</summary>
    public string Query { get; }

    /// <summary>Gets whether this request still belongs to the visible current query; UI thread only.</summary>
    public bool IsCurrent => owner.TryGetTarget(out var current) && current.IsCurrentSuggestionRequest(this);

    /// <summary>Publishes borrowed indexed results on the UI thread, or rejects retired work.</summary>
    /// <param name="source">Current-query results, or null for no suggestions.</param>
    /// <returns>Whether this request remained current through source/layout/application callbacks.</returns>
    public bool TrySetSuggestions(IList? source)
        => owner.TryGetTarget(out var current) && current.ApplySuggestions(this, source, null);

    /// <summary>Publishes an application-provided error caption without retaining an exception.</summary>
    /// <param name="message">The caption to display; it must not contain sensitive diagnostic data.</param>
    /// <returns>Whether this request remained current through publication.</returns>
    public bool TrySetError(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return owner.TryGetTarget(out var current) && current.ApplySuggestions(this, null, message);
    }
}

/// <summary>Describes an admitted search before history publication and guarded command execution.</summary>
public sealed class SearchRequestedEventArgs : CancelEventArgs
{
    internal SearchRequestedEventArgs(string query) => Query = query;
    /// <summary>Gets the submitted text snapshot. Cancel prevents history and command execution.</summary>
    public string Query { get; }
}

/// <summary>Describes a proposed suggestion commit before editor text is replaced.</summary>
public sealed class SearchSuggestionEventArgs : CancelEventArgs
{
    internal SearchSuggestionEventArgs(object? suggestion, int index, string text)
    { Suggestion = suggestion; Index = index; Text = text; }
    /// <summary>Gets the borrowed source value, including a possible null value.</summary>
    public object? Suggestion { get; }
    /// <summary>Gets its occurrence index at admission; do not retain this as item identity.</summary>
    public int Index { get; }
    /// <summary>Gets the proposed editor text after the runtime text selector.</summary>
    public string Text { get; }
}
