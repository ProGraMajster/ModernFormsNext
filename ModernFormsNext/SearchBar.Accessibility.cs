using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class SearchBar
{
    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new SearchAccessibleObject(this);

    private sealed class SearchAccessibleObject(SearchBar owner) : ControlAccessibleObject(owner)
    {
        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (Owner is SearchBar { IsDisposed: false } current)
            {
                yield return current.editor.AccessibilityObject;
                yield return current.searchButton.AccessibilityObject;
                yield return current.clearButton.AccessibilityObject;
            }
            // Popup suggestions already belong to their real canonical window/root. Exposing
            // them again here would create conflicting parentage and duplicate semantic nodes.
            foreach (var child in base.GetAccessibilityChildren()) yield return child;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (retiring) return;
        retiring = true;
        InvalidateSuggestionRequest();
        var source = commandSource;
        commandSource = null;
        suggestionProvider = null;
        suggestionTextSelector = null;
        history.Clear();
        List<Exception>? errors = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (errors ??= []).Add(error); } }
        // Event accessors on borrowed commands are application code. A failed detach must
        // not abandon the independently owned popup, recycler or actual editor children.
        if (source is not null) Attempt(source.Dispose);
        Attempt(RetireSuggestionPopup);
        Attempt(suggestions.Dispose);
        Attempt(suggestionStatus.Dispose);
        SuggestionsRequested = null;
        SuggestionsOpened = null;
        SuggestionsClosed = null;
        SearchRequested = null;
        SuggestionChosen = null;
        HistoryChanged = null;
        Attempt(() => base.Dispose(true));
        if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException("Search control cleanup failed.", errors);
    }
}
