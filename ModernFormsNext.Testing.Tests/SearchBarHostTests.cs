using ModernFormsNext.Accessibility;
using ModernFormsNext.Virtualization;
using ModernFormsNext.WindowKit.Input;
using System.Windows.Input;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class SearchBarHostTests
{
    [Fact]
    public void PopupObserverFailureStopsLoadingAndKeepsOriginalDispatcherDiagnostic()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar { SuggestionProvider = _ => new[] { "recovered" } };
        host.Show(search, 440, 36);
        var failure = new InvalidOperationException("popup observer");
        EventHandler handler = (_, _) => throw failure;
        search.SuggestionsOpened += handler;
        search.Text = "query";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.Same(failure, Assert.Single(host.Dispatcher.UnhandledExceptions));
        Assert.False(search.IsLoadingSuggestions);
        Assert.Null(search.SuggestionsSource);
        search.SuggestionsOpened -= handler;
        search.RefreshSuggestions();
        host.Dispatcher.Drain();
        Assert.Equal("recovered", search.SuggestionsSource![0]);
    }

    [Fact]
    public void LeavingEditorDismissesPopupAndRejectsOutstandingResults()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var search = form.Controls.Add(new SearchBar { Bounds = new(0, 0, 320, 36) });
        var other = form.Controls.Add(new TextBox { Top = 70 });
        host.Show(form, 440, 160);
        SearchSuggestionRequestEventArgs? request = null;
        search.SuggestionsRequested += (_, e) => request = e;
        search.Text = "query";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.NotNull(request);
        Assert.True(search.AreSuggestionsOpen);
        other.Select();
        Assert.True(other.Focused);
        Assert.False(search.AreSuggestionsOpen);
        Assert.False(request.IsCurrent);
        Assert.False(request.TrySetSuggestions(new[] { "late" }));
    }

    [Fact]
    public void DisablingHistoryInsideAdmissionKeepsTheCurrentCommandAndNoRetainedQuery()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar();
        host.Show(search, 440, 36);
        int commands = 0;
        search.SearchCommand = new DelegateCommand(_ => commands++);
        search.Text = "private query";
        search.SearchRequested += (_, _) => search.IsHistoryEnabled = false;
        search.SubmitSearch();
        Assert.Equal(1, commands);
        Assert.Empty(search.History);
        Assert.Null(search.SuggestionsSource);
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("disable")]
    [InlineData("capacity")]
    public void RemovingHistoryAlsoRetiresItsPublishedSuggestionSnapshot(string change)
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar();
        host.Show(search, 440, 36);
        search.Text = "Entry history";
        search.SubmitSearch();
        search.Text = "Entry";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.Equal("Entry history", search.SuggestionsSource![0]);
        switch (change)
        {
            case "clear": search.ClearHistory(); break;
            case "disable": search.IsHistoryEnabled = false; break;
            case "capacity": search.HistoryCapacity = 0; break;
        }
        host.Dispatcher.Drain();
        Assert.Empty(search.History);
        Assert.True(search.SuggestionsSource is null or { Count: 0 });
    }

    [Fact]
    public void HistoryCallbackPublicationWinsOverOlderRetentionRefresh()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar();
        host.Show(search, 440, 36);
        search.Text = "Entry history";
        search.SubmitSearch();
        search.Text = "Entry";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        search.HistoryChanged += (_, _) => search.SuggestionsSource = new[] { "newer application result" };
        search.ClearHistory();
        host.Dispatcher.Drain();
        Assert.Equal("newer application result", search.SuggestionsSource![0]);
    }

    [Fact]
    public void FinalizerPathDoesNotDetachCommandsOrClosePopupAndManagedChildren()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new FinalizerSearch { SuggestionProvider = _ => new[] { "result" } };
        var window = host.Show(search, 440, 36);
        int calls = 0, disposed = 0;
        var command = new DelegateCommand(_ => calls++);
        search.SearchCommand = command;
        search.Text = "query";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        var popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        window.FocusedControl!.Disposed += (_, _) => disposed++;
        search.FinalizeForTest();
        Assert.False(popup.IsClosed);
        Assert.True(search.AreSuggestionsOpen);
        Assert.Same(command, search.SearchCommand);
        Assert.Equal(0, disposed);
        search.SubmitSearch();
        Assert.Equal(1, calls);
        search.Dispose();
        Assert.True(popup.IsClosed);
        Assert.Equal(1, disposed);
    }

    private sealed class FinalizerSearch : SearchBar
    {
        internal void FinalizeForTest() => Dispose(false);
    }

    [Fact]
    public void AsyncSearchUsesCanonicalBusyCompletionAndDoesNotCancelBorrowedWorkOnDisposal()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar();
        host.Show(search, 440, 36);
        var pending = new TaskCompletionSource();
        CancellationToken token = default;
        int calls = 0;
        object? parameter = null;
        var command = new AsyncCommand((value, cancellation) =>
        {
            calls++;
            parameter = value;
            token = cancellation;
            return pending.Task;
        });
        search.SearchCommand = command;
        search.Text = "async query";
        search.SubmitSearch();
        Assert.True(command.IsExecuting);
        Assert.False(search.CanSubmitSearch);
        search.SubmitSearch();
        Assert.Equal(1, calls);
        Assert.Equal("async query", parameter);
        search.Dispose();
        Assert.False(token.IsCancellationRequested);
        var worker = new Thread(() => pending.SetResult());
        worker.Start();
        worker.Join();
        host.Dispatcher.Drain();
        Assert.True(command.ExecutionTask!.IsCompletedSuccessfully);
        Assert.False(command.IsExecuting);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void FocusRedirectFromEditorIsRespectedAndDoesNotOpenSuggestions()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var search = form.Controls.Add(new SearchBar { SuggestionProvider = _ => new[] { "result" } });
        var other = form.Controls.Add(new TextBox { Top = 60 });
        var window = host.Show(form, 440, 140);
        // The canonical editor peer is the actual owned TextBox, not a proxy focus target.
        Assert.True(search.FocusEditor());
        var editor = Assert.IsAssignableFrom<TextBox>(window.FocusedControl);
        editor.GotFocus += (_, _) => other.Select();
        other.Select();
        Assert.False(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.Same(other, window.FocusedControl);
        Assert.False(search.AreSuggestionsOpen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProviderFailureStopsLoadingPreservesDiagnosticAndAllowsRecovery(bool eventFails)
    {
        using var host = ModernFormsTestHost.Create();
        var failure = new InvalidOperationException("provider diagnostic");
        var search = new SearchBar();
        host.Show(search, 440, 36);
        EventHandler<SearchSuggestionRequestEventArgs> handler = (_, _) => throw failure;
        if (eventFails) search.SuggestionsRequested += handler;
        else search.SuggestionProvider = _ => throw failure;
        search.Text = "query";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.Same(failure, Assert.Single(host.Dispatcher.UnhandledExceptions));
        Assert.False(search.IsLoadingSuggestions);
        Assert.Null(search.SuggestionsSource);
        search.SuggestionsRequested -= handler;
        search.SuggestionProvider = _ => new[] { "recovered" };
        host.Dispatcher.Drain();
        Assert.Equal("recovered", search.SuggestionsSource![0]);
        Assert.False(search.IsLoadingSuggestions);
    }

    [Fact]
    public void UnhostedConfigurationDoesNotQueueProviderWork()
    {
        using var host = ModernFormsTestHost.Create();
        host.Dispatcher.Drain();
        int before = host.Dispatcher.PendingWorkCount, calls = 0;
        using var search = new SearchBar { Text = "preview", SuggestionProvider = _ => { calls++; return null; } };
        search.RefreshSuggestions();
        Assert.Equal(before, host.Dispatcher.PendingWorkCount);
        host.Dispatcher.Drain();
        Assert.Equal(0, calls);
    }

    [Fact]
    public void RequestPublicationRequiresUiThreadAndRejectsAfterDisposal()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar();
        host.Show(search, 440, 36);
        SearchSuggestionRequestEventArgs? request = null;
        search.SuggestionsRequested += (_, e) => request = e;
        search.Text = "query";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.NotNull(request);
        Exception? failure = null;
        var worker = new Thread(() => failure = Record.Exception(() => request.TrySetSuggestions(new[] { "wrong thread" })));
        worker.Start();
        worker.Join();
        Assert.IsType<InvalidOperationException>(failure);
        Assert.True(request.IsCurrent);
        search.Dispose();
        Assert.False(request.IsCurrent);
        Assert.False(request.TrySetSuggestions(new[] { "late" }));
    }

    [Fact]
    public void CompositeExposesActualEditorAndActionPeers()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar();
        var window = host.Show(search, 440, 36);
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.IsAssignableFrom<TextBox>(window.FocusedControl);
        var root = search.AccessibilityObject;
        Assert.Equal(3, root.GetChildCount());
        Assert.Equal(AccessibleControlType.Edit, root.GetChild(0)!.ControlType);
        Assert.Equal(AccessibleControlType.Button, root.GetChild(1)!.ControlType);
        Assert.Equal(AccessibleControlType.Button, root.GetChild(2)!.ControlType);
        Assert.NotNull(search.FindForm()!.TextInputClient);
    }

    [Fact]
    public void SubmitOrdersAdmissionHistoryAndCanonicalCommandAndIgnoresRecursion()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar();
        host.Show(search, 440, 36);
        var order = new List<string>();
        object? parameter = null;
        search.SearchCommand = new DelegateCommand(value => { order.Add("command"); parameter = value; });
        search.SearchRequested += (_, e) => { order.Add("request:" + e.Query); search.SubmitSearch(); };
        search.HistoryChanged += (_, _) => order.Add("history");
        search.Text = "alpha";
        search.SubmitSearch();
        Assert.Equal(new[] { "request:alpha", "history", "command" }, order);
        Assert.Equal("alpha", parameter);
        Assert.Equal(new[] { "alpha" }, search.History);
        search.SearchRequested += (_, e) => e.Cancel = true;
        search.Text = "cancel";
        search.SubmitSearch();
        Assert.Equal(new[] { "alpha" }, search.History);
        Assert.Equal("alpha", parameter);
    }

    [Fact]
    public void KeyboardSuggestionCommitsAfterTheOriginalRouteAndSubmitsOnce()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar { SuggestionProvider = _ => new[] { "first", "second" } };
        var window = host.Show(search, 440, 36);
        search.Text = "query";
        object? submitted = null;
        int executions = 0;
        search.SearchCommand = new DelegateCommand(value => { submitted = value; executions++; });
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.True(search.AreSuggestionsOpen);
        window.Input.KeyDown(Keys.Down);
        window.Input.KeyUp(Keys.Down);
        Assert.Equal(0, search.HighlightedSuggestionIndex);
        window.Input.KeyDown(Keys.Enter);
        window.Input.KeyUp(Keys.Enter);
        Assert.Equal("query", search.Text);
        host.Dispatcher.Drain();
        Assert.Equal("first", search.Text);
        Assert.Equal("first", submitted);
        Assert.Equal(1, executions);
        Assert.False(search.AreSuggestionsOpen);
        Assert.IsAssignableFrom<TextBox>(window.FocusedControl);
    }

    [Fact]
    public void QueryRequestsRejectOldResultsAndDismissalRetiresCurrentResults()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar();
        host.Show(search, 440, 36);
        var requests = new List<SearchSuggestionRequestEventArgs>();
        search.SuggestionsRequested += (_, e) => requests.Add(e);
        search.Text = "old";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        var old = Assert.Single(requests);
        search.Text = "new";
        host.Dispatcher.Drain();
        var current = requests[^1];
        Assert.Equal(2, requests.Count);
        Assert.False(old.IsCurrent);
        Assert.False(old.TrySetSuggestions(new[] { "obsolete" }));
        Assert.True(current.TrySetSuggestions(new[] { "current" }));
        Assert.Equal("current", search.SuggestionsSource![0]);
        search.CloseSuggestions();
        Assert.False(current.IsCurrent);
        Assert.False(current.TrySetSuggestions(new[] { "late" }));
    }

    [Fact]
    public void HistoryIsBoundedDetachedDeduplicatedAndClearedWhenDisabled()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar { HistoryCapacity = 2 };
        host.Show(search, 440, 36);
        foreach (string query in new[] { "One", "two", "ONE", "three" })
        {
            search.Text = query;
            search.SubmitSearch();
        }
        Assert.Equal(new[] { "three", "ONE" }, search.History);
        var detached = Assert.IsType<string[]>(search.History);
        detached[0] = "changed externally";
        Assert.Equal("three", search.History[0]);
        search.IsHistoryEnabled = false;
        Assert.Empty(search.History);
        search.Text = "private";
        search.SubmitSearch();
        Assert.Empty(search.History);
    }

    [Fact]
    public void CompositionSuppressesProvidersAndPopupDoesNotRetireParentIme()
    {
        using var host = ModernFormsTestHost.Create();
        int providers = 0, searches = 0;
        var search = new SearchBar { SuggestionProvider = query => { providers++; return new[] { query + " result" }; } };
        var window = host.Show(search, 440, 36);
        search.SearchCommand = new DelegateCommand(_ => searches++);
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        providers = 0;
        var client = Assert.IsAssignableFrom<ITextInputClient>(search.FindForm()!.TextInputClient);
        Assert.True(client.SetComposingText("provisional"));
        host.Dispatcher.Drain();
        Assert.Equal(0, providers);
        Assert.True(client.GetState()!.HasComposition);
        Assert.Same(client, search.FindForm()!.TextInputClient);
        Assert.True(client.CommitText("final"));
        host.Dispatcher.Drain();
        Assert.Equal(1, providers);
        Assert.True(search.AreSuggestionsOpen);
        Assert.Same(client, search.FindForm()!.TextInputClient);
        Assert.True(client.PerformEditorAction(TextInputAction.Search));
        Assert.Equal(1, searches);
        Assert.False(search.AreSuggestionsOpen);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("command")]
    [InlineData("parameter")]
    [InlineData("hide")]
    [InlineData("reparent")]
    [InlineData("dispose")]
    public void AdmissionCallbackCannotExecuteObsoleteSearch(string change)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var first = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        var second = form.Controls.Add(new Panel());
        var search = first.Controls.Add(new SearchBar());
        host.Show(form, 440, 180);
        int executions = 0;
        search.SearchCommand = new DelegateCommand(_ => executions++);
        search.Text = "old";
        search.SearchRequested += (_, _) =>
        {
            switch (change)
            {
                case "text": search.Text = "new"; break;
                case "command": search.SearchCommand = new DelegateCommand(_ => executions++); break;
                case "parameter": search.SearchCommandParameter = new object(); break;
                case "hide": search.Visible = false; break;
                case "reparent": second.Controls.Add(search); break;
                case "dispose": search.Dispose(); break;
            }
        };
        search.SubmitSearch();
        Assert.Equal(0, executions);
    }

    [Fact]
    public void ProviderReplacementDuringOldProviderWinsOverItsReturnedResults()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar();
        host.Show(search, 440, 36);
        bool first = true;
        search.SuggestionProvider = _ =>
        {
            if (first)
            {
                first = false;
                search.SuggestionProvider = _ => new[] { "new provider" };
            }
            return new[] { "old provider" };
        };
        search.Text = "query";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.Equal("new provider", search.SuggestionsSource![0]);
    }

    [Fact]
    public void PostedSuggestionCannotReplaceANewerPopupOrItsEditor()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var search = form.Controls.Add(new SearchBar { SuggestionProvider = _ => new[] { "old selection" } });
        var window = host.Show(form, 440, 180);
        search.Text = "query";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.True(search.ChooseSuggestion(0));
        using var newer = new PopupWindow(form) { Size = new System.Drawing.Size(180, 60) };
        newer.Controls.Add(new TextBox { Dock = DockStyle.Fill });
        newer.Show(0, 40);
        var popupHost = Assert.IsType<TestPopupHost>(window.ActivePopup);
        Assert.True(popupHost.Input.Focus(newer.Controls[0]));
        var client = newer.TextInputClient;
        host.Dispatcher.Drain();
        Assert.Equal("query", search.Text);
        Assert.Same(newer, window.ActivePopup!.Window);
        Assert.Same(client, newer.TextInputClient);
    }

    [Theory]
    [InlineData("query")]
    [InlineData("source")]
    [InlineData("focus")]
    [InlineData("hide")]
    [InlineData("dispose")]
    public void SuggestionCallbackCannotCommitOlderText(string change)
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar { SuggestionProvider = _ => new[] { "suggested", "second" } };
        host.Show(search, 440, 36);
        var other = search.FindForm()!.Controls.Add(new TextBox { Top = 60 });
        search.Text = "query";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        search.SuggestionChosen += (_, _) =>
        {
            switch (change)
            {
                case "query": search.Text = "new query"; break;
                case "source": search.SuggestionsSource = new[] { "new source" }; break;
                case "focus": other.Select(); break;
                case "hide": search.Visible = false; break;
                case "dispose": search.Dispose(); break;
            }
        };
        Assert.True(search.ChooseSuggestion(0));
        host.Dispatcher.Drain();
        if (change == "dispose") Assert.Throws<ObjectDisposedException>(() => search.FocusEditor());
        else Assert.Equal(change == "query" ? "new query" : "query", search.Text);
    }

    [Fact]
    public void PendingResultsCannotStealANewerPopup()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var search = form.Controls.Add(new SearchBar());
        var window = host.Show(form, 440, 180);
        SearchSuggestionRequestEventArgs? request = null;
        search.SuggestionsRequested += (_, e) => request = e;
        search.Text = "query";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        Assert.NotNull(request);
        using var newer = new PopupWindow(form) { Size = new System.Drawing.Size(180, 60) };
        newer.Show(0, 40);
        Assert.False(request.IsCurrent);
        Assert.False(request.TrySetSuggestions(new[] { "late" }));
        Assert.Same(newer, window.ActivePopup!.Window);
    }

    [Fact]
    public void LargeSuggestionSourceHasBoundedRealizationWithoutHorizontalOverflow()
    {
        using var host = ModernFormsTestHost.Create();
        var values = Enumerable.Range(0, 100_003).Select(index => $"Entry {index}").ToArray();
        var search = new SearchBar { SuggestionProvider = _ => values };
        var window = host.Show(search, 440, 36);
        search.Text = "Entry";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        var popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        popup.LayoutUntilStable();
        var list = Assert.IsAssignableFrom<CollectionView>(popup.Window.Controls[0]);
        Assert.Equal(100_003, list.ItemCount);
        Assert.InRange(list.GetVirtualizationSnapshot().RealizedContainerCount, 1, 16);
        // Hidden scrollbars retain their last numeric Maximum. The canonical scroll provider
        // reports the actual visible/scrollable axis and its unobscured logical viewport.
        var scroll = list.AccessibilityObject.ScrollInfo!.Value;
        Assert.False(scroll.Horizontal.IsScrollable);
        Assert.InRange(list.ItemSize.Width, 1, scroll.Horizontal.ViewportLength);
    }

    [Fact]
    public void TemplateCallbackChangingVisibleRowLimitCannotBeOverwrittenByOldLayout()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar();
        bool first = true;
        search.SuggestionItemTemplate = new MetricTemplate(() =>
        {
            if (!first) return;
            first = false;
            search.MaximumVisibleSuggestions = 2;
        });
        search.SuggestionProvider = _ => Enumerable.Range(0, 30).Select(index => $"Entry {index}").ToArray();
        var window = host.Show(search, 440, 36);
        search.Text = "Entry";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        var popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        popup.LayoutUntilStable();
        Assert.Equal(2, search.MaximumVisibleSuggestions);
        Assert.Equal(2 * search.SuggestionRowHeight, popup.Window.Size.Height);
    }

    private sealed class MetricTemplate(Action prepare) : IItemContainerFactory<Control>
    {
        public Control CreateContainer() => new Label();
        public void PrepareContainer(Control container, int index) { prepare(); container.Text = $"Entry {index}"; }
        public void ClearContainer(Control container, int previousIndex) { }
        public void DiscardContainer(Control container) => container.Dispose();
    }

    [Fact]
    public void ThrowingCommandDetachDoesNotSkipPopupAndOwnedEditorCleanup()
    {
        using var host = ModernFormsTestHost.Create();
        var search = new SearchBar { SuggestionProvider = _ => new[] { "result" } };
        var window = host.Show(search, 440, 36);
        var command = new ThrowingDetachCommand();
        search.SearchCommand = command;
        search.Text = "query";
        Assert.True(search.FocusEditor());
        host.Dispatcher.Drain();
        var popup = Assert.IsType<TestPopupHost>(window.ActivePopup);
        bool editorDisposed = false;
        Assert.IsAssignableFrom<TextBox>(window.FocusedControl).Disposed += (_, _) => editorDisposed = true;
        var failure = Assert.Throws<InvalidOperationException>(search.Dispose);
        Assert.Equal("detach", failure.Message);
        Assert.True(editorDisposed);
        Assert.True(popup.IsClosed);
        Assert.False(command.Disposed);
    }

    private sealed class ThrowingDetachCommand : ICommand, IDisposable
    {
        public bool Disposed;
        private EventHandler? handlers;
        public event EventHandler? CanExecuteChanged
        {
            add => handlers += value;
            remove { handlers -= value; throw new InvalidOperationException("detach"); }
        }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
        public void Dispose() => Disposed = true;
    }
}
