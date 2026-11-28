using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class AppShellTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShellVisitsUseCanonicalOwnershipFocusAndTransferredBack(bool neutral)
    {
        using var f = new ShellFixture(neutral);
        var home = NewPage("home"); var document = NewPage("document");
        f.Shell.Routes.Register("home", _ => home);
        f.Shell.Routes.Register("home/documents/{id}", context => { Assert.Equal("42", context.Parameters["id"]); return document; });
        Assert.True(Complete(f.Shell.GoToAsync("/home", animated: false)));
        home.Content!.Select();
        Assert.True(Complete(f.Shell.GoToAsync("documents/42?mode=edit", animated: false)));
        Assert.Same(document, f.Shell.CurrentPage);
        Assert.Equal("/home/documents/42?mode=edit", f.Shell.CurrentRoute!.Route);
        Assert.Equal(PageLifecycleState.Attached, home.LifecycleState);
        Assert.Equal(PageLifecycleState.Appeared, document.LifecycleState);
        Assert.Same(document.Content, f.Focused);
        Assert.Equal(2, f.Shell.NavigationStack.Count);
        var removed = Complete(f.Shell.GoBackAsync(animated: false));
        Assert.Same(document, removed);
        Assert.Null(document.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, document.LifecycleState);
        Assert.Same(home, f.Shell.CurrentPage);
        Assert.Equal("/home", f.Shell.CurrentRoute!.Route);
        Assert.Same(home.Content, f.Focused);
        Assert.Equal(0, f.Shell.PendingRequestCount);
        document.Dispose();
    }

    [Fact]
    public void VirtualizedCollectionRetainsSelectionAcrossNavigationAndReleasesOldSourceRows()
    {
        using var f = new ShellFixture(neutral: false);
        var window = f.Window!;
        var values = Enumerable.Range(0, 100_000).Select(index => $"Row {index}").ToArray();
        var view = new CollectionView { Dock = DockStyle.Fill, ItemSize = new(120, 24), ItemsSource = values };
        var collection = new ContentPage { Title = "Collection", Content = view };
        using var detail = NewPage("detail");
        f.Shell.Routes.Register("collection", _ => collection);
        f.Shell.Routes.Register("detail", _ => detail);
        Assert.True(Complete(f.Shell.GoToAsync("/collection", animated: false)));

        window.LayoutUntilStable();
        var firstRow = Assert.IsAssignableFrom<Control>(view.GetItemContainer(0));
        Assert.True(window.Input.Focus(firstRow));
        Assert.True(window.Input.PressKey(Keys.End));
        int last = values.Length - 1;
        var selectedRow = Assert.IsAssignableFrom<Control>(view.GetItemContainer(last));
        Assert.Equal(values[last], view.SelectedItem);
        Assert.Same(selectedRow, window.FocusedControl);

        view.ScrollToItem(0);
        Assert.Same(view, window.FocusedControl);
        view.ScrollToItem(last);
        Assert.Equal(last, view.SelectedIndex);
        Assert.Same(view.GetItemContainer(last), window.FocusedControl);
        Assert.InRange(view.GetVirtualizationSnapshot().RealizedContainerCount, 1, 32);
        Assert.True(view.GetVirtualizationSnapshot().ReusedContainerCount > 0);

        Assert.True(Complete(f.Shell.GoToAsync("/detail", animated: false)));
        Assert.Same(detail.Content, window.FocusedControl);
        Assert.Same(detail, Complete(f.Shell.GoBackAsync(animated: false)));
        Assert.Equal(last, view.SelectedIndex);
        Assert.Same(view.GetItemContainer(last), window.FocusedControl);

        Control[] oldRows = view.Controls.Cast<Control>().ToArray();
        Assert.NotEmpty(oldRows);
        var retired = new HashSet<Control>();
        foreach (var row in oldRows)
            row.Disposed += (_, _) => Assert.True(retired.Add(row));
        view.ItemsSource = new[] { "New source" };
        window.LayoutUntilStable();
        Assert.Equal(-1, view.SelectedIndex);
        Assert.True(retired.SetEquals(oldRows));
        Assert.True(window.FocusedControl is null || !oldRows.Contains(window.FocusedControl));
        var newRow = Assert.IsAssignableFrom<Control>(view.GetItemContainer(0));
        Assert.Equal("New source", newRow.Text);
        int viewDisposals = 0, rowDisposals = 0;
        view.Disposed += (_, _) => viewDisposals++;
        newRow.Disposed += (_, _) => rowDisposals++;
        window.Close();
        Assert.Equal(1, viewDisposals);
        Assert.Equal(1, rowDisposals);
        Assert.Empty(f.Host.Dispatcher.UnhandledExceptions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidationVetoLeavesFactoryResultBorrowedUntilLaterAdmission(bool neutral)
    {
        using var f = new ShellFixture(neutral);
        var home = NewPage("home"); var detail = NewPage("detail");
        f.Shell.Routes.Register("home", _ => home);
        f.Shell.Routes.Register("detail", _ => detail);
        Assert.True(Complete(f.Shell.GoToAsync("/home", animated: false)));
        home.Content!.Select();
        bool cancel = true;
        home.Content!.Validating += (_, args) => args.Cancel = cancel;
        Assert.False(Complete(f.Shell.GoToAsync("/detail", animated: false)));
        Assert.Same(home, f.Shell.CurrentPage);
        Assert.Same(home.Content, f.Focused);
        Assert.Null(detail.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, detail.LifecycleState);
        cancel = false;
        Assert.True(Complete(f.Shell.GoToAsync("/detail", animated: false)));
        Assert.Same(detail.Content, f.Focused);
    }

    [Fact]
    public void NewerVisitWinsOverLateFactoryWithoutDisposingItsBorrowedResult()
    {
        using var f = new ShellFixture(false);
        var slow = new TaskCompletionSource<Page>();
        var stale = NewPage("stale"); var current = NewPage("current");
        CancellationToken token = default;
        f.Shell.Routes.Register("slow", (_, cancellation) => { token = cancellation; return slow.Task; });
        f.Shell.Routes.Register("current", _ => current);
        var older = f.Shell.GoToAsync("/slow", animated: false);
        Assert.False(older.IsCompleted);
        Assert.True(Complete(f.Shell.GoToAsync("/current", animated: false)));
        Assert.True(token.IsCancellationRequested);
        f.CompleteFactory(slow, stale);
        Assert.False(Complete(older));
        Assert.Same(current, f.Shell.CurrentPage);
        Assert.Equal("/current", f.Shell.CurrentRoute!.Route);
        Assert.Null(stale.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, stale.LifecycleState);
        Assert.Single(f.Shell.NavigationStack);
        Assert.Equal(0, f.Shell.PendingRequestCount);
        stale.Dispose();
    }

    [Fact]
    public void FactoryReentryRetiresTheOlderUncommittedVisit()
    {
        using var f = new ShellFixture(false);
        var old = NewPage("old"); var current = NewPage("current");
        Task<bool>? newer = null;
        f.Shell.Routes.Register("old", _ => { newer = f.Shell.GoToAsync("/current", animated: false); return old; });
        f.Shell.Routes.Register("current", _ => current);
        Assert.False(Complete(f.Shell.GoToAsync("/old", animated: false)));
        Assert.True(Complete(newer!));
        Assert.Same(current, f.Shell.CurrentPage);
        Assert.Null(old.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, old.LifecycleState);
        Assert.Equal(0, f.Shell.PendingRequestCount);
        old.Dispose();
    }

    [Fact]
    public void UnregisteringAPendingFactoryPreservesItsCallerOwnedLatePage()
    {
        using var f = new ShellFixture(false);
        var source = new TaskCompletionSource<Page>();
        var page = NewPage("late");
        using var registration = f.Shell.Routes.Register("late", (_, _) => source.Task);
        var visit = f.Shell.GoToAsync("/late", animated: false);
        registration.Dispose();
        f.CompleteFactory(source, page);
        Assert.False(Complete(visit));
        Assert.Null(f.Shell.CurrentPage);
        Assert.Null(page.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, page.LifecycleState);
        page.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingRootBackUsesTheExistingInputRouteAndDoesNotCloseTheHost(bool neutral)
    {
        using var f = new ShellFixture(neutral);
        var home = NewPage("home"); var late = NewPage("late");
        var source = new TaskCompletionSource<Page>();
        CancellationToken token = default;
        f.Shell.Routes.Register("home", _ => home);
        f.Shell.Routes.Register("late", (_, cancellation) => { token = cancellation; return source.Task; });
        Complete(f.Shell.GoToAsync("/home", animated: false));
        home.Content!.Select();
        var visit = f.Shell.GoToAsync("/late", animated: false);
        f.Key(Keys.BrowserBack);
        Assert.True(token.IsCancellationRequested);
        Assert.Same(home, f.Shell.CurrentPage);
        Assert.Equal(PageLifecycleState.Appeared, home.LifecycleState);
        f.CompleteFactory(source, late);
        Assert.False(Complete(visit));
        Assert.Null(late.Parent);
        late.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeaderBackUsesShellPendingAdmissionAndUpdatesRouteTitle(bool neutral)
    {
        using var f = new ShellFixture(neutral);
        var home = NewPage("home");
        using var late = NewPage("late");
        var source = new TaskCompletionSource<Page>();
        CancellationToken cancellation = default;
        f.Shell.Routes.Register("home", _ => home);
        f.Shell.Routes.Register("late", (_, token) => { cancellation = token; return source.Task; });
        var header = f.Shell.NavigationBar;
        Assert.Same(f.Shell.BackCommand, header.BackButton.Command);
        Assert.False(header.BackButton.Visible);
        Assert.True(Complete(f.Shell.GoToAsync("/home", animated: false)));
        Assert.Equal("home", header.TitleLabel.Text);
        var visit = f.Shell.GoToAsync("/late", animated: false);
        Assert.True(header.BackButton.Visible);
        header.BackButton.Select();
        Assert.Same(header.BackButton, f.Focused);
        header.BackButton.PerformClick();
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Same(home.Content, f.Focused);
        Assert.False(header.BackButton.Visible);
        f.CompleteFactory(source, late);
        Assert.False(Complete(visit));
        Assert.Same(home, f.Shell.CurrentPage);
        Assert.Null(late.Parent);
        Assert.Equal("home", header.TitleLabel.Text);
    }

    [Fact]
    public void PreCanceledOrUnknownRequestsDoNotCancelAnExistingFactory()
    {
        using var f = new ShellFixture(false);
        var source = new TaskCompletionSource<Page>();
        var page = NewPage("late");
        CancellationToken pending = default;
        f.Shell.Routes.Register("late", (_, token) => { pending = token; return source.Task; });
        var visit = f.Shell.GoToAsync("/late", animated: false);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.True(f.Shell.GoToAsync("/unknown", cancellationToken: canceled.Token).IsCanceled);
        Assert.True(f.Shell.GoBackAsync(cancellationToken: canceled.Token).IsCanceled);
        Assert.Throws<KeyNotFoundException>(() => { _ = f.Shell.GoToAsync("/unknown"); });
        Assert.False(pending.IsCancellationRequested);
        f.CompleteFactory(source, page);
        Assert.True(Complete(visit));
        Assert.Same(page, f.Shell.CurrentPage);
    }

    [Fact]
    public void ThrowingRouteObserverKeepsCommittedStateAndOwnership()
    {
        using var f = new ShellFixture(false);
        var page = NewPage("committed");
        f.Shell.Routes.Register("page", _ => page);
        f.Shell.CurrentRouteChanged += (_, _) => throw new InvalidOperationException("observer");
        var visit = f.Shell.GoToAsync("/page", animated: false);
        Assert.Throws<InvalidOperationException>(() => Complete(visit));
        Assert.Same(page, f.Shell.CurrentPage);
        Assert.Equal("/page", f.Shell.CurrentRoute!.Route);
        Assert.NotNull(page.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, page.LifecycleState);
        Assert.Equal(0, f.Shell.PendingRequestCount);
    }

    [Fact]
    public void DisposalRetiresLateFactoryWithoutPostDisposalCommandNotifications()
    {
        using var f = new ShellFixture(false);
        var source = new TaskCompletionSource<Page>(); var page = NewPage("late");
        f.Shell.Routes.Register("late", (_, _) => source.Task);
        var visit = f.Shell.GoToAsync("/late", animated: false);
        int notices = 0;
        f.Shell.NavigateCommand.CanExecuteChanged += (_, _) => notices++;
        f.Shell.BackCommand.CanExecuteChanged += (_, _) => notices++;
        f.Shell.Dispose();
        int baseline = notices;
        f.CompleteFactory(source, page);
        Assert.False(Complete(visit));
        Assert.Equal(baseline, notices);
        Assert.Null(page.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, page.LifecycleState);
        Assert.Equal(0, f.Shell.PendingRequestCount);
        Assert.Throws<ObjectDisposedException>(() => f.Shell.Routes.Match("/late"));
        page.Dispose();
    }

    private static ContentPage NewPage(string title) => new() { Title = title, Content = new TextBox { Text = title, Dock = DockStyle.Fill } };
    private static T Complete<T>(Task<T> task) { Assert.True(task.IsCompleted, "An immediate/dispatcher-drained route must be terminal."); return task.GetAwaiter().GetResult(); }

    private sealed class ShellFixture : IDisposable
    {
        private readonly bool neutral;
        private SkiaControlSurface? surface;
        internal ModernFormsTestHost Host { get; } = ModernFormsTestHost.Create();
        internal AppShell Shell { get; } = new();
        internal Panel Root { get; } = new() { Width = 600, Height = 400 };
        internal TestWindowHost? Window { get; private set; }
        internal Control? Focused => surface?.SelectedControl ?? Window?.FocusedControl;
        internal ShellFixture(bool neutral, bool mount = true)
        {
            this.neutral = neutral;
            Root.Controls.Add(new PageHost { Page = Shell, Bounds = new(0, 0, 600, 350) });
            Root.Controls.Add(new Button { Text = "outside", Bounds = new(0, 360, 100, 32) });
            if (mount) Mount();
        }
        internal void Mount()
        {
            if (neutral) { surface = new(Root); surface.Resize(600, 400); }
            else Window = Host.Show(Root);
        }
        internal void CompleteFactory(TaskCompletionSource<Page> source, Page page)
        {
            var worker = new Thread(() => source.SetResult(page));
            worker.Start(); Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
            Host.Dispatcher.Drain();
        }
        internal void Key(Keys key)
        {
            if (surface is not null) { surface.ProcessKeyDown(key); surface.ProcessKeyUp(key); }
            else Window!.Input.PressKey(key);
        }
        public void Dispose() { surface?.Dispose(); Root.Dispose(); Host.Dispose(); }
    }
}
