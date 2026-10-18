using System.Collections;
using System.ComponentModel;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class SearchBar
{
    private readonly List<string> history = new();
    private int historyCapacity = 32;
    private bool historyEnabled = true;

    /// <summary>Gets or sets whether admitted searches enter memory-only history; disabling clears retained queries.</summary>
    /// <remarks>UI thread only. Refreshes current suggestions after HistoryChanged, preserving newer callback publications. No history is written to disk or sent to a service. An application may explicitly persist a detached History snapshot.</remarks>
    [DefaultValue(true)]
    public bool IsHistoryEnabled
    {
        get => historyEnabled;
        set
        {
            VerifyAccess();
            if (historyEnabled == value) return;
            historyEnabled = value;
            if (!value) history.Clear();
            PublishHistorySettingsChange();
        }
    }

    /// <summary>Gets or sets the maximum retained query count, from zero through 1000; the default is 32.</summary>
    /// <remarks>UI thread only. Shrinking removes oldest entries before HistoryChanged; zero retains none. Current suggestions refresh after callbacks unless newer work was published.</remarks>
    [DefaultValue(32)]
    public int HistoryCapacity
    {
        get => historyCapacity;
        set
        {
            VerifyAccess();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1000);
            if (historyCapacity == value) return;
            historyCapacity = value;
            if (history.Count > value) history.RemoveRange(value, history.Count - value);
            PublishHistorySettingsChange();
        }
    }

    /// <summary>Gets a detached most-recent-first query snapshot, never the mutable backing list.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> History { get { VerifyAccess(); return history.ToArray(); } }

    /// <summary>Occurs after history contents or retention settings are committed on the UI thread.</summary>
    /// <remarks>History records admitted SearchRequested operations before command dispatch, not successful asynchronous completion.</remarks>
    public event EventHandler? HistoryChanged;

    /// <summary>Clears retained queries without performing I/O or changing editor text.</summary>
    /// <remarks>Retires the old suggestion snapshot after HistoryChanged unless callbacks published newer work. A search already being admitted retains its ordinary command dispatch.</remarks>
    public void ClearHistory()
    {
        VerifyAccess();
        if (history.Count == 0) return;
        history.Clear();
        PublishHistorySettingsChange();
    }

    private void PublishHistorySettingsChange()
    {
        var lifetime = new AccessibilityControlLifetime(this);
        long work = workVersion, source = suggestions.ItemsGeneration;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        if (retiring || IsDisposed || !lifetime.IsCurrent || work != workVersion || source != suggestions.ItemsGeneration) return;
        // Clearing retention must also release any previously published history array.
        // During admission the popup/request is already retired; clear its borrowed snapshot
        // without invalidating that search's command guard. Otherwise use normal fresh delivery.
        if (submitting) suggestions.ItemsSource = null;
        else BeginSuggestionRequest();
    }

    private void RecordHistory(string query)
    {
        if (!historyEnabled || historyCapacity == 0) return;
        int existing = history.FindIndex(value => string.Equals(value, query, StringComparison.OrdinalIgnoreCase));
        if (existing == 0 && history[0] == query) return;
        if (existing >= 0) history.RemoveAt(existing);
        history.Insert(0, query);
        if (history.Count > historyCapacity) history.RemoveRange(historyCapacity, history.Count - historyCapacity);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private IList GetHistorySuggestions(string query)
        => historyEnabled ? history.Where(value => value.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)).ToArray() : Array.Empty<string>();
}
