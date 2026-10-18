using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class SearchBar
{
    private PopupWindow? suggestionPopup;
    private SearchSuggestionRequestEventArgs? suggestionRequest;
    private AccessibilityControlLifetime? suggestionLifetime;
    private long workVersion, lastDispatchedRequest, commitVersion;
    private long suggestionLayoutVersion;
    private bool suggestionDispatchQueued, dispatchingSuggestions, pendingSuggestions;
    private bool publishedPopupOpen;
    private int suggestionRowHeight = 32, maximumVisibleSuggestions = 6;
    private PendingCommit? pendingCommit;
    // Set only by the known-framework Designer preview before authored values are applied.
    // Previewing text must not enqueue requests or execute application providers/delegates.
    internal bool SuppressSuggestionRequests { get; set; }

    /// <summary>Gets whether the actual suggestion popup is visible.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AreSuggestionsOpen => suggestionPopup?.Visible == true;

    /// <summary>Gets whether the current request awaits application/provider results; no task state is inferred.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsLoadingSuggestions => pendingSuggestions;

    /// <summary>Gets or sets the highlighted canonical occurrence, or -1 for none.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HighlightedSuggestionIndex
    {
        get { VerifyAccess(); return suggestions.SelectedIndex; }
        set { VerifyAccess(); suggestions.SelectedIndex = value; }
    }

    /// <summary>Gets or sets the positive suggestion cell height in logical pixels; the default is 32.</summary>
    [DefaultValue(32)]
    public int SuggestionRowHeight
    {
        get => suggestionRowHeight;
        set { VerifyAccess(); ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); suggestionRowHeight = value; UpdateSuggestionLayout(); }
    }

    /// <summary>Gets or sets the visible row limit, from one through 20; realization remains shared and bounded.</summary>
    [DefaultValue(6)]
    public int MaximumVisibleSuggestions
    {
        get => maximumVisibleSuggestions;
        set
        {
            VerifyAccess();
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 20);
            maximumVisibleSuggestions = value;
            UpdateSuggestionLayout();
        }
    }

    /// <summary>Offers the immutable query and revocable UI-thread result publication.</summary>
    /// <remarks>Raised once per coalesced request after composition callbacks. A synchronous publication takes precedence over SuggestionProvider; otherwise an installed provider runs, or bounded history is used. Application-owned asynchronous handlers return and publish later through this request.</remarks>
    public event EventHandler<SearchSuggestionRequestEventArgs>? SuggestionsRequested;
    /// <summary>Occurs after the actual popup becomes visible. Reentrant newer requests are preserved.</summary>
    public event EventHandler? SuggestionsOpened;
    /// <summary>Occurs after the actual popup hides; outstanding request results are retired on dismissal.</summary>
    public event EventHandler? SuggestionsClosed;

    /// <summary>Requests a fresh current-query suggestion pass through the existing dispatcher.</summary>
    /// <remarks>Recursive refresh from a provider is ignored; a new query still schedules newer work. Composition is allowed to finish first. No timer, background job or networking is created.</remarks>
    public void RefreshSuggestions()
    {
        VerifyAccess();
        if (dispatchingSuggestions) return;
        BeginSuggestionRequest();
    }

    /// <summary>Dismisses suggestions and rejects outstanding results without moving keyboard focus.</summary>
    public void CloseSuggestions()
    {
        VerifyAccess();
        InvalidateSuggestionRequest();
        HideSuggestionPopup();
    }

    /// <summary>Admits an existing suggestion's canonical Invoke action.</summary>
    /// <param name="index">Current occurrence index.</param>
    /// <returns>Whether the item accepted invocation. Its posted commit may be retired by a newer query or lifecycle change.</returns>
    /// <remarks>The commit runs on the next dispatcher turn, after the original pointer/accessibility route completes.</remarks>
    public bool ChooseSuggestion(int index)
    {
        VerifyAccess();
        return suggestions.InvokeLogicalItem(index);
    }

    private long InvalidateSuggestionRequest()
    {
        suggestionRequest = null;
        suggestionLifetime = null;
        pendingSuggestions = false;
        pendingCommit = null;
        lastDispatchedRequest = ++workVersion;
        return workVersion;
    }

    private void HandleEditorTextChanged(object? sender, EventArgs e)
    {
        if (retiring || IsDisposed) return;
        long version = InvalidateSuggestionRequest();
        var lifetime = new AccessibilityControlLifetime(this);
        HideSuggestionPopup();
        if (!Current()) return;
        suggestions.ItemsSource = null;
        if (!Current()) return;
        UpdateButtons();
        if (!Current()) return;
        commandSource?.Parameter = EffectiveSearchParameter;
        if (!Current()) return;
        base.OnTextChanged(e);
        if (Current()) StartSuggestionRequest(version);
        bool Current() => !retiring && !IsDisposed && lifetime.IsCurrent && version == workVersion;
    }

    private void BeginSuggestionRequest()
    {
        long version = InvalidateSuggestionRequest();
        var lifetime = new AccessibilityControlLifetime(this);
        HideSuggestionPopup();
        if (retiring || IsDisposed || !lifetime.IsCurrent || version != workVersion) return;
        suggestions.ItemsSource = null;
        if (!retiring && !IsDisposed && lifetime.IsCurrent && version == workVersion) StartSuggestionRequest(version);
    }

    private void StartSuggestionRequest(long version)
    {
        suggestionRequest = new(this, version, Text);
        suggestionLifetime = new(this);
        lastDispatchedRequest = version - 1;
        QueueSuggestionDispatch();
    }

    private SearchSuggestionRequestEventArgs EnsureSuggestionRequest()
    {
        if (suggestionRequest is { } current && IsCurrentSuggestionRequest(current)) return current;
        long version = InvalidateSuggestionRequest();
        suggestionRequest = new(this, version, Text);
        suggestionLifetime = new(this);
        return suggestionRequest;
    }

    internal bool IsCurrentSuggestionRequest(SearchSuggestionRequestEventArgs request)
    {
        if (retiring || IsDisposed || Disposing) return false;
        VerifyAccess();
        return ReferenceEquals(request, suggestionRequest) && request.Id == workVersion &&
            suggestionLifetime?.IsCurrent == true && request.Query == Text && !HasNewerActivePopup;
    }

    private bool HasNewerActivePopup => Application.ActivePopupWindow is { Visible: true } active &&
        !ReferenceEquals(active, suggestionPopup);

    private void QueueSuggestionDispatch()
    {
        if (SuppressSuggestionRequests || suggestionDispatchQueued || retiring || IsDisposed || suggestionRequest is null ||
            !Visible || FindWindow() is null) return;
        suggestionDispatchQueued = true;
        var weak = new WeakReference<SearchBar>(this);
        Application.RunOnUIThread(() =>
        {
            if (!weak.TryGetTarget(out var owner) || owner.retiring || owner.IsDisposed) return;
            owner.suggestionDispatchQueued = false;
            owner.DispatchSuggestions();
        });
    }

    private void DispatchSuggestions()
    {
        var request = suggestionRequest;
        if (request is null || !IsCurrentSuggestionRequest(request) || lastDispatchedRequest == request.Id ||
            !Visible || !Enabled || editor.document.HasComposition) return;
        lastDispatchedRequest = request.Id;
        pendingSuggestions = true;
        suggestionStatus.Text = "Loading suggestions…";
        UpdateSuggestionLayout();
        ShowSuggestionPopup();
        if (!IsCurrentSuggestionRequest(request)) return;
        dispatchingSuggestions = true;
        try
        {
            SuggestionsRequested?.Invoke(this, request);
            if (!IsCurrentSuggestionRequest(request) || !pendingSuggestions) return;
            var provider = suggestionProvider;
            if (provider is not null) request.TrySetSuggestions(provider(request.Query));
            else if (SuggestionsRequested is null) request.TrySetSuggestions(GetHistorySuggestions(request.Query));
        }
        catch (Exception failure)
        {
            // A synchronous provider failure still belongs to the dispatcher diagnostic path.
            // Settle only its outstanding request; never replace results or a query published
            // reentrantly by application callbacks, and never display private exception text.
            try
            {
                if (pendingSuggestions && IsCurrentSuggestionRequest(request))
                    ApplySuggestions(request, null, "Suggestions unavailable");
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("Suggestion delivery and failure presentation failed.", failure, cleanupFailure);
            }
            throw;
        }
        finally { dispatchingSuggestions = false; }
    }

    internal bool ApplySuggestions(SearchSuggestionRequestEventArgs request, IList? source, string? error)
    {
        if (!IsCurrentSuggestionRequest(request)) return false;
        pendingSuggestions = false;
        lastDispatchedRequest = request.Id;
        suggestions.ItemsSource = source;
        if (!IsCurrentSuggestionRequest(request)) return false;
        suggestionStatus.Text = error ?? "No suggestions";
        UpdateSuggestionLayout();
        if (IsCurrentSuggestionRequest(request)) ShowSuggestionPopup();
        return IsCurrentSuggestionRequest(request);
    }

    private void ShowSuggestionPopup()
    {
        if (retiring || IsDisposed || !Enabled || !Visible || !editor.Focused || editor.document.HasComposition ||
            HasNewerActivePopup || FindForm() is not { InputBindingsClosed: false } form) return;
        var lifetime = new AccessibilityControlLifetime(this);
        long version = workVersion;
        if (suggestionPopup is { } old && (old.InputBindingsClosed || !ReferenceEquals(old.ParentForm, form))) RetireSuggestionPopup();
        if (!lifetime.IsCurrent || version != workVersion || retiring) return;
        if (suggestionPopup is null)
        {
            var created = new PopupWindow(form) { RetainParentTextInput = true };
            suggestionPopup = created;
            created.Controls.Add(suggestions);
            created.Controls.Add(suggestionStatus);
            created.VisibleChanged += SuggestionPopupVisibilityChanged;
            created.Closed += SuggestionPopupVisibilityChanged;
        }
        UpdateSuggestionLayout();
        var popup = suggestionPopup;
        if (popup is not null && lifetime.IsCurrent && version == workVersion && !popup.Visible)
            popup.Show(this, 0, Height);
    }

    private void UpdateSuggestionLayout()
    {
        if (retiring || IsDisposed || suggestions is null) return;
        long version = ++suggestionLayoutVersion, work = workVersion;
        var lifetime = new AccessibilityControlLifetime(this);
        int width = Math.Max(1, Width);
        int rows = Math.Clamp(suggestions.ItemCount, 1, maximumVisibleSuggestions);
        int height = (int)Math.Min(int.MaxValue / 2L, (long)rows * suggestionRowHeight);
        suggestions.ItemSize = new(Math.Max(1, suggestions.LogicalScrollViewport.Width), suggestionRowHeight);
        if (!Current()) return;
        suggestions.Bounds = new(0, 0, width, height);
        if (!Current()) return;
        suggestions.Visible = suggestions.ItemCount != 0;
        if (!Current()) return;
        suggestionStatus.Visible = suggestions.ItemCount == 0;
        if (!Current()) return;
        if (suggestionPopup is { } popup && !popup.InputBindingsClosed && popup.Size != new Size(width, height))
        {
            popup.Size = new(width, height);
            if (popup.Visible && !HasNewerActivePopup) popup.Show(this, 0, Height);
        }
        bool Current() => !retiring && !IsDisposed && lifetime.IsCurrent &&
            version == suggestionLayoutVersion && work == workVersion;
    }

    private void SuggestionPopupVisibilityChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, suggestionPopup)) return;
        bool open = suggestionPopup!.Visible && !suggestionPopup.InputBindingsClosed;
        if (open == publishedPopupOpen) return;
        publishedPopupOpen = open;
        if (open) SuggestionsOpened?.Invoke(this, EventArgs.Empty);
        else
        {
            if (suggestionRequest is not null) InvalidateSuggestionRequest();
            SuggestionsClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HideSuggestionPopup()
    {
        if (suggestionPopup is { Visible: true, InputBindingsClosed: false } popup) popup.Hide();
    }

    private void RetireSuggestionPopup()
    {
        var popup = suggestionPopup;
        if (popup is null) return;
        suggestionPopup = null;
        bool wasOpen = publishedPopupOpen;
        publishedPopupOpen = false;
        popup.VisibleChanged -= SuggestionPopupVisibilityChanged;
        popup.Closed -= SuggestionPopupVisibilityChanged;
        List<Exception>? errors = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (errors ??= []).Add(error); } }
        // The list/status belong to this control, not a retired window adapter. Detach before
        // destruction; a callback may create another popup using those same canonical controls.
        Attempt(() => popup.Controls.Remove(suggestions));
        Attempt(() => popup.Controls.Remove(suggestionStatus));
        if (!popup.InputBindingsClosed) Attempt(popup.Close);
        if (!popup.adapter.IsDisposed) Attempt(popup.adapter.Dispose);
        Attempt(popup.Dispose);
        if (wasOpen) Attempt(() => SuggestionsClosed?.Invoke(this, EventArgs.Empty));
        if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException("Search suggestion popup cleanup failed.", errors);
    }

    private void SuggestionInvoked(object? sender, CollectionViewItemEventArgs e)
    {
        if (retiring || IsDisposed || !Enabled || !Visible || HasNewerActivePopup) return;
        long id = ++commitVersion;
        pendingCommit = new(id, workVersion, suggestions.ItemsGeneration, e.Index, e.Item, new(this));
        var weak = new WeakReference<SearchBar>(this);
        // Keep the admitted item's original route valid until Invoke/pointer-up returns.
        Application.RunOnUIThread(() => { if (weak.TryGetTarget(out var owner)) owner.CommitSuggestion(id); });
    }

    private void CommitSuggestion(long id)
    {
        if (retiring || IsDisposed || pendingCommit is not { } commit || commit.Id != id) return;
        pendingCommit = null;
        if (!Current()) return;
        string text = suggestionTextSelector?.Invoke(commit.Item) ?? Convert.ToString(commit.Item, CultureInfo.CurrentCulture) ?? string.Empty;
        if (!Current()) return;
        var args = new SearchSuggestionEventArgs(commit.Item, commit.Index, text);
        SuggestionChosen?.Invoke(this, args);
        if (args.Cancel || !Current()) return;
        bool changes = Text != text;
        editor.Text = text;
        long expected = commit.Work + (changes ? 1 : 0);
        if (retiring || IsDisposed || !commit.Lifetime.IsCurrent || workVersion != expected || Text != text) return;
        CloseSuggestions();
        if (submitOnSuggestion) SubmitSearch();
        bool Current() => !retiring && !IsDisposed && Enabled && Visible && commit.Work == workVersion &&
            !HasNewerActivePopup && commit.Source == suggestions.ItemsGeneration &&
            commit.Index == suggestions.SelectedIndex && commit.Lifetime.IsCurrent;
    }

    private sealed record PendingCommit(long Id, long Work, long Source, int Index, object? Item, AccessibilityControlLifetime Lifetime);

    private sealed class SuggestionList(SearchBar owner) : CollectionView
    {
        protected override void OnLayout(LayoutEventArgs e)
        {
            // The vertical scrollbar is part of the canonical viewport. Fit that viewport,
            // not the outer popup width, to avoid manufacturing horizontal overflow.
            var size = new Size(Math.Max(1, LogicalScrollViewport.Width), owner?.suggestionRowHeight ?? 32);
            if (ItemSize != size) ItemSize = size;
            base.OnLayout(e);
        }
    }
}
