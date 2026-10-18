using System;
using System.Linq;
using ModernFormsNext;

namespace ControlGallery.Panels;

/// <summary>Demonstrates canonical search suggestions, bounded history and nullable culture-aware time.</summary>
public sealed class SearchTimePanel : BasePanel
{
    private readonly SearchBar search;
    private readonly TimePicker time;
    private readonly Label status;
    private SearchSuggestionRequestEventArgs? pending;
    private bool deferred;

    /// <summary>Creates an interactive search/time page without networking, persistence or background jobs.</summary>
    public SearchTimePanel()
    {
        var tools = Controls.Add(new Panel { Dock = DockStyle.Top, Height = 310 });
        tools.Controls.Add(new Label { Bounds = new(8, 8, 850, 28), Text = "Type Entry; use Down / Up / Enter, Escape, Search and Clear." });
        search = tools.Controls.Add(new SearchBar { Bounds = new(8, 42, 580, 36), Placeholder = "Search entries", AccessibleName = "Gallery search" });
        var mode = tools.Controls.Add(new Button { Bounds = new(8, 90, 180, 36), Text = "Local / deferred results" });
        var publish = tools.Controls.Add(new Button { Bounds = new(196, 90, 180, 36), Text = "Publish pending results" });
        var fail = tools.Controls.Add(new Button { Bounds = new(384, 90, 180, 36), Text = "Fail pending request" });
        var history = tools.Controls.Add(new Button { Bounds = new(8, 138, 180, 36), Text = "Clear recent history" });
        var dismiss = tools.Controls.Add(new Button { Bounds = new(196, 138, 180, 36), Text = "Dismiss suggestions" });
        tools.Controls.Add(new Label { Bounds = new(8, 186, 850, 28), Text = "Time: checkbox clears/enables; arrows, wheel and buttons step by 30 seconds." });
        time = tools.Controls.Add(new TimePicker { Bounds = new(8, 222, 320, 36), Time = new TimeOnly(14, 30, 15).Add(TimeSpan.FromTicks(1234567)), Format = "HH:mm:ss.fffffff", StepSeconds = 30, AccessibleName = "Gallery time" });
        var culture = tools.Controls.Add(new Button { Bounds = new(336, 222, 180, 36), Text = "12 / 24 hour format" });
        var clear = tools.Controls.Add(new Button { Bounds = new(524, 222, 120, 36), Text = "Clear time" });
        tools.Controls.Add(new Label { Bounds = new(8, 270, 900, 28), Text = "Deferred mode is application-owned: old requests cannot replace a newer query." });
        status = Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 140, Multiline = true });
        search.SearchCommand = new DelegateCommand(value => ShowStatus("Submitted: " + value));
        search.SuggestionsRequested += (_, request) =>
        {
            pending = request;
            if (!deferred) request.TrySetSuggestions(Results(request.Query));
        };
        mode.Click += (_, _) => { deferred = !deferred; search.RefreshSuggestions(); ShowStatus(deferred ? "Deferred: type, then publish" : "Local suggestions"); };
        publish.Click += (_, _) => ShowStatus(pending?.TrySetSuggestions(Results(pending.Query)) == true ? "Published current results" : "Old request rejected");
        fail.Click += (_, _) => ShowStatus(pending?.TrySetError("Example application error") == true ? "Error published" : "Old request rejected");
        history.Click += (_, _) => { search.ClearHistory(); ShowStatus("History cleared"); };
        dismiss.Click += (_, _) => search.CloseSuggestions();
        culture.Click += (_, _) =>
        {
            bool twelve = time.CultureName != "en-US";
            time.CultureName = twelve ? "en-US" : "pl-PL";
            time.Format = twelve ? "hh:mm:ss tt" : "HH:mm:ss.fffffff";
            ShowStatus("Presentation changed; stored ticks retained");
        };
        clear.Click += (_, _) => time.Time = null;
        time.TimeChanged += (_, _) => ShowStatus("Time changed");
        ShowStatus("Ready");
    }

    private static string[] Results(string query) => Enumerable.Range(0, 100)
        .Select(index => $"Entry {index}").Where(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();

    private void ShowStatus(string action) => status.Text = $"{action}\nTime: {time.Time?.ToString("HH:mm:ss.fffffff") ?? "empty"}\nRecent: {string.Join(", ", search.History)}\nHistory is memory-only; no query is sent or saved.";

    /// <summary>Retires the borrowed pending request and dismisses the real popup when the page is unloaded.</summary>
    public override void UnloadPanel() { pending = null; search.CloseSuggestions(); }
}
