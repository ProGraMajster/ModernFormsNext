using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Input;
using ModernFormsNext.Accessibility;
using ModernFormsNext.DataBinding;
using ModernFormsNext.Virtualization;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext;

/// <summary>Combines the canonical text editor, bounded suggestions, recent history and a search command.</summary>
/// <remarks>
/// UI thread only. Providers, indexed sources, factories, commands and result values are borrowed.
/// Suggestions use one owned CollectionView and the existing nonactivating popup; the parent
/// editor keeps its IME connection. Asynchronous work, networking and persistence belong to the
/// application. Use the revocable SuggestionsRequested request to reject late results.
/// </remarks>
/// <example><code>
/// var search = new SearchBar { Placeholder = "Search entries" };
/// search.SuggestionProvider = query =&gt; entries.Where(x =&gt; x.StartsWith(query)).Take(20).ToArray();
/// search.SearchCommand = new DelegateCommand(parameter =&gt; OpenResults((string)parameter!));
/// </code></example>
[DefaultProperty(nameof(Text)), DefaultEvent(nameof(SearchRequested))]
public partial class SearchBar : Control, ICommandBindingTargetProvider
{
    private readonly SearchEditor editor;
    private readonly SearchButton searchButton;
    private readonly Button clearButton;
    private readonly CollectionView suggestions;
    private readonly Label suggestionStatus;
    private CommandSource? commandSource;
    private object? searchCommandParameter;
    private Func<string, IList?>? suggestionProvider;
    private Func<object?, string>? suggestionTextSelector;
    private bool commandAvailable = true, submitting, retiring;
    private string searchButtonText = "Search", clearButtonText = "Clear";
    private bool submitOnSuggestion = true;

    /// <summary>Creates an empty search editor with a 32-entry in-memory history and no provider.</summary>
    public SearchBar()
    {
        editor = Controls.AddImplicitControl(new SearchEditor(this)
        { TextInputOptions = new TextInputOptions { Action = TextInputAction.Search } });
        searchButton = Controls.AddImplicitControl(new SearchButton(this) { Text = searchButtonText, Enabled = false });
        clearButton = Controls.AddImplicitControl(new Button { Text = clearButtonText, Enabled = false });
        clearButton.Click += (_, _) => ClearText();
        suggestions = new SuggestionList(this) { CacheItemCount = 2, AccessibleName = "Search suggestions" };
        suggestions.ItemInvoked += SuggestionInvoked;
        suggestionStatus = new Label { Text = "No suggestions", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        editor.TextChanged += HandleEditorTextChanged;
        editor.GotFocus += (_, _) =>
        {
            if (retiring || IsDisposed) return;
            if (suggestions.ItemCount != 0) ShowSuggestionPopup();
            else RefreshSuggestions();
        };
        editor.LostFocus += (_, _) =>
        {
            // Real parent-focus departure dismisses the search session. Selecting a row
            // in the nonactivating popup uses its own scope and keeps this editor focused.
            if (!retiring && !IsDisposed && !Disposing) CloseSuggestions();
        };
        editor.TextCompositionChanged += (_, e) =>
        {
            if (e.Stage is TextCompositionStage.Committed or TextCompositionStage.Finished or TextCompositionStage.Canceled)
                QueueSuggestionDispatch();
        };
        TabStop = false;
    }

    /// <summary>Gets or sets the actual editor text. Changes retire old suggestion and search requests.</summary>
    /// <remarks>Uses the existing TextBox document/IME, binding and TextChanged event path. Suggestion delivery is posted after editing callbacks.</remarks>
    public override string Text
    {
        get => editor?.Text ?? string.Empty;
        set { VerifyAccess(); editor.Text = value; }
    }

    /// <summary>Gets or sets the ordinary editor placeholder.</summary>
    [DefaultValue("")]
    public string Placeholder { get => editor.Placeholder; set { VerifyAccess(); editor.Placeholder = value; } }

    /// <summary>Gets or sets the borrowed search command, receiving the query string unless an explicit parameter is configured.</summary>
    /// <remarks>CommandSource owns availability/routing and background requery. AsyncCommand owns execution/cancellation; this control never cancels or disposes it.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ICommand? SearchCommand
    {
        get => commandSource?.Command;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(SearchCommand, value)) return;
            long version = InvalidateSuggestionRequest();
            var source = commandSource ??= new CommandSource(this);
            source.Parameter = EffectiveSearchParameter;
            if (!IsDisposed && version == workVersion && ReferenceEquals(source, commandSource)) source.Command = value;
        }
    }

    /// <summary>Gets or sets a borrowed command parameter; null uses the current query string.</summary>
    /// <remarks>Runtime-only, UI thread only. Replacement retires older guarded dispatch through the canonical CommandSource.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SearchCommandParameter
    {
        get => searchCommandParameter;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(searchCommandParameter, value)) return;
            InvalidateSuggestionRequest();
            searchCommandParameter = value;
            (commandSource ??= new CommandSource(this)).Parameter = EffectiveSearchParameter;
        }
    }

    private object EffectiveSearchParameter => searchCommandParameter ?? Text;

    /// <summary>Gets or sets an explicit target for a borrowed RoutedCommand; null routes from this control.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? SearchCommandTarget
    {
        get => commandSource?.Target;
        set { VerifyAccess(); InvalidateSuggestionRequest(); (commandSource ??= new CommandSource(this)).Target = value; }
    }

    /// <summary>Gets or sets a synchronous UI-thread provider of already filtered indexed suggestions.</summary>
    /// <remarks>Runtime-only. Null uses bounded matching history unless SuggestionsRequested supplies results. Use that event for application-owned asynchronous providers; no network or task is started implicitly.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, IList?>? SuggestionProvider
    {
        get => suggestionProvider;
        set { VerifyAccess(); if (ReferenceEquals(suggestionProvider, value)) return; suggestionProvider = value; BeginSuggestionRequest(); }
    }

    /// <summary>Gets or sets borrowed results for the current query, without enumerating their values.</summary>
    /// <remarks>Runtime-only, UI thread only. A new query clears old results. Async producers should publish through their request token instead.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IList? SuggestionsSource
    {
        get { VerifyAccess(); return suggestions.ItemsSource; }
        set { VerifyAccess(); var request = EnsureSuggestionRequest(); ApplySuggestions(request, value, null); }
    }

    /// <summary>Gets or sets a borrowed runtime factory for suggestion display controls.</summary>
    /// <remarks>CollectionView owns generated controls and binding cleanup. Templates customize display; the parent text editor remains the text-input owner.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IItemContainerFactory<Control>? SuggestionItemTemplate
    {
        get => suggestions.ItemTemplate;
        set { VerifyAccess(); long version = InvalidateSuggestionRequest(); suggestions.ItemTemplate = value; if (version == workVersion && !IsDisposed) RefreshSuggestions(); }
    }

    /// <summary>Gets or sets the runtime conversion used when a suggestion commits to the editor.</summary>
    /// <remarks>Null uses culture-aware Convert.ToString. Display customization belongs to SuggestionItemTemplate. Reentrant conversion cannot commit obsolete text.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, string>? SuggestionTextSelector
    {
        get => suggestionTextSelector;
        set { VerifyAccess(); InvalidateSuggestionRequest(); suggestionTextSelector = value; }
    }

    /// <summary>Gets or sets whether a chosen suggestion also submits search; the default is true.</summary>
    [DefaultValue(true)]
    public bool SubmitOnSuggestion { get => submitOnSuggestion; set { VerifyAccess(); submitOnSuggestion = value; } }

    /// <summary>Gets or sets the search button caption.</summary>
    [DefaultValue("Search")]
    public string SearchButtonText
    {
        get => searchButtonText;
        set { VerifyAccess(); ArgumentNullException.ThrowIfNull(value); searchButtonText = value; searchButton.Text = value; }
    }

    /// <summary>Gets or sets the clear button caption.</summary>
    [DefaultValue("Clear")]
    public string ClearButtonText
    {
        get => clearButtonText;
        set { VerifyAccess(); ArgumentNullException.ThrowIfNull(value); clearButtonText = value; clearButton.Text = value; }
    }

    /// <summary>Gets the current search action availability snapshot; whitespace and provisional composition are not submitted.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanSubmitSearch => !retiring && !IsDisposed && !Disposing && Enabled && Visible && !submitting &&
        !editor.document.HasComposition && !string.IsNullOrWhiteSpace(Text) && commandAvailable &&
        SearchCommand is not AsyncCommand { IsExecuting: true };

    /// <summary>Occurs after button Click/availability and before history publication and guarded command execution.</summary>
    /// <remarks>Cancel prevents history and execution. State is committed before callbacks; recursive submission is ignored. Query/binding/host retirement prevents older execution.</remarks>
    public event EventHandler<SearchRequestedEventArgs>? SearchRequested;

    /// <summary>Occurs before a suggestion replaces editor text. Cancel keeps the existing text.</summary>
    /// <remarks>Text conversion has completed; handlers may redirect the query/source/focus or dispose. Newer requests win.</remarks>
    public event EventHandler<SearchSuggestionEventArgs>? SuggestionChosen;

    /// <summary>Requests search through the same guarded path as Enter, software Search and the actual button.</summary>
    public void SubmitSearch() => SubmitSearch(null);

    private void SubmitSearch(MouseEventArgs? click)
    {
        if (IsDisposed || Disposing) return;
        VerifyAccess();
        if (!CanSubmitSearch) return;
        var lifetime = new AccessibilityControlLifetime(this);
        long version = InvalidateSuggestionRequest();
        string query = Text;
        submitting = true;
        try
        {
            HideSuggestionPopup();
            if (!Current()) return;
            if (commandSource is not null && !commandSource.CanExecute()) return;
            if (!Current()) return;
            searchButton.DispatchClick(click ?? new(MouseButtons.Left, 1, 0, 0, Point.Empty));
            if (!Current()) return;
            var args = new SearchRequestedEventArgs(query);
            SearchRequested?.Invoke(this, args);
            if (args.Cancel || !Current()) return;
            RecordHistory(query);
            if (Current()) commandSource?.Execute();
        }
        finally { submitting = false; }
        bool Current() => lifetime.IsCurrent && version == workVersion && !retiring && Enabled && Visible && Text == query;
    }

    /// <summary>Clears editor text and preserves an application focus redirection made by callbacks.</summary>
    public void ClearText()
    {
        VerifyAccess();
        var lifetime = new AccessibilityControlLifetime(this);
        var scope = GetFocusScope();
        long focus = scope.Version;
        editor.Text = string.Empty;
        if (lifetime.IsCurrent && scope.Version == focus) editor.Select();
    }

    /// <summary>Focuses the actual text editor through the canonical validation/focus scope.</summary>
    /// <returns>Whether the editor owns focus after callbacks.</returns>
    public bool FocusEditor()
    {
        VerifyAccess();
        var lifetime = new AccessibilityControlLifetime(this);
        editor.Select();
        return lifetime.IsCurrent && editor.Focused;
    }

    ICommand? ICommandBindingTargetProvider.Command { get => SearchCommand; set => SearchCommand = value; }
    object? ICommandBindingTargetProvider.CommandParameter { get => EffectiveSearchParameter; set => SearchCommandParameter = value; }
    bool ICommandBindingTargetProvider.Enabled => Enabled;
    bool ICommandBindingTargetProvider.IsCommandSourceDisposed => retiring || IsDisposed || Disposing;
    bool ICommandBindingTargetProvider.IsCommandSourceActive => Visible && CommandRouting.IsSourceActive(this);
    void ICommandBindingTargetProvider.SetCommandEnabled(bool enabled)
    {
        commandAvailable = enabled;
        UpdateButtons();
    }
    internal override void RefreshRoutedCommandSource() => commandSource?.RefreshRouted();

    private void UpdateButtons()
    {
        if (retiring || IsDisposed || searchButton is null) return;
        searchButton.Enabled = commandAvailable && !string.IsNullOrWhiteSpace(Text);
        if (!IsDisposed) clearButton.Enabled = Text.Length != 0;
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(retiring || IsDisposed || Disposing, this);
        GetFocusScope().VerifyAccess();
    }

    private bool ProcessEditorKey(KeyEventArgs e)
    {
        if (retiring || !Enabled || editor.document.HasComposition || e.Control || e.Alt) return false;
        switch (e.KeyCode)
        {
            case Keys.Down:
                if (suggestions.ItemCount == 0) { RefreshSuggestions(); return true; }
                ShowSuggestionPopup();
                if (!IsDisposed) suggestions.SelectedIndex = Math.Min(suggestions.ItemCount - 1, suggestions.SelectedIndex + 1);
                return true;
            case Keys.Up when AreSuggestionsOpen:
                suggestions.SelectedIndex = Math.Max(-1, suggestions.SelectedIndex - 1);
                return true;
            case Keys.Enter:
                if (AreSuggestionsOpen && suggestions.SelectedIndex >= 0) suggestions.InvokeLogicalItem(suggestions.SelectedIndex);
                else SubmitSearch();
                return true;
            case Keys.Escape when AreSuggestionsOpen || pendingSuggestions:
                CloseSuggestions();
                return true;
            case Keys.Tab:
                CloseSuggestions();
                return false;
            default: return false;
        }
    }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(320, 32);
    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (editor is not null && !editor.IsDisposed)
        {
            var bounds = LogicalPaddedClientRectangle;
            int searchWidth = Math.Min(88, Math.Max(0, bounds.Width / 3));
            int clearWidth = Math.Min(64, Math.Max(0, (bounds.Width - searchWidth) / 2));
            editor.Bounds = new(bounds.X, bounds.Y, Math.Max(0, bounds.Width - searchWidth - clearWidth), bounds.Height);
            searchButton.Bounds = new(editor.Right, bounds.Y, searchWidth, bounds.Height);
            clearButton.Bounds = new(searchButton.Right, bounds.Y, clearWidth, bounds.Height);
        }
        base.OnLayout(e);
    }

    /// <inheritdoc/>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (suggestions is not null && !retiring && !IsDisposed) UpdateSuggestionLayout();
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(EventArgs e)
    {
        InvalidateSuggestionRequest();
        RetireSuggestionPopup();
        base.OnParentChanged(e);
        if (!IsDisposed) commandSource?.RefreshOwner();
    }
    /// <inheritdoc/>
    protected override void OnVisibleChanged(EventArgs e)
    {
        InvalidateSuggestionRequest();
        if (!Visible) HideSuggestionPopup();
        base.OnVisibleChanged(e);
        if (!IsDisposed) commandSource?.RefreshOwner();
    }
    /// <inheritdoc/>
    protected override void OnEnabledChanged(EventArgs e)
    {
        InvalidateSuggestionRequest();
        if (!Enabled) HideSuggestionPopup();
        base.OnEnabledChanged(e);
        if (!IsDisposed) commandSource?.RefreshOwner();
    }

    private sealed class SearchEditor(SearchBar owner) : TextBox
    {
        protected override bool ProcessTextBoxKeyDown(KeyEventArgs e)
            => owner.ProcessEditorKey(e) || base.ProcessTextBoxKeyDown(e);
    }
    private sealed class SearchButton(SearchBar owner) : Button
    {
        protected override void OnClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) owner.SubmitSearch(e);
            else base.OnClick(e);
        }
        internal void DispatchClick(MouseEventArgs e) => base.OnClick(e);
    }
}
