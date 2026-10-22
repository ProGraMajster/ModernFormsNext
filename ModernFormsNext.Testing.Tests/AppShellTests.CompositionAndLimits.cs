using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class AppShellTests
{
    [Fact]
    public void RegistrationCanBeRecoveredByExactPatternAfterACommandObserverThrows()
    {
        using var f = new ShellFixture(false);
        EventHandler observer = (_, _) => throw new InvalidOperationException("observer");
        f.Shell.NavigateCommand.CanExecuteChanged += observer;
        Assert.Throws<InvalidOperationException>(() => f.Shell.Routes.Register("page/{id}", _ => NewPage("page")));
        f.Shell.NavigateCommand.CanExecuteChanged -= observer;
        Assert.Equal(1, f.Shell.Routes.Count);
        Assert.False(f.Shell.Routes.Unregister("page/{otherName}"));
        Assert.True(f.Shell.Routes.Unregister("/page/{id}/"));
        Assert.Equal(0, f.Shell.Routes.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedNavigationAndFlyoutBackWinBeforeTheOuterShell(bool neutral)
    {
        using var f = new ShellFixture(neutral);
        var home = NewPage("home");
        var innerRoot = NewPage("inner root");
        var inner = new NavigationPage(innerRoot) { TransitionDuration = TimeSpan.Zero };
        var tabs = new TabbedPage(); tabs.Pages.Add(inner); tabs.Pages.Add(NewPage("settings"));
        var menu = NewPage("menu");
        var composition = new FlyoutPage { Detail = tabs, Flyout = menu,
            DisplayMode = FlyoutDisplayMode.Overlay, TransitionDuration = TimeSpan.Zero };
        f.Shell.Routes.Register("home", _ => home);
        f.Shell.Routes.Register("workspace", _ => composition);
        Complete(f.Shell.GoToAsync("/home", animated: false));
        home.Content!.Select();
        Complete(f.Shell.GoToAsync("/workspace", animated: false));
        var detail = NewPage("nested detail");
        Assert.True(Complete(inner.PushAsync(detail, animated: false)));
        detail.Content!.Select();
        f.Key(Keys.BrowserBack);
        Assert.Same(innerRoot, inner.CurrentPage);
        Assert.Equal("/workspace", f.Shell.CurrentRoute!.Route);
        Assert.Equal(2, f.Shell.NavigationStack.Count);
        Assert.Null(detail.Parent);
        detail.Dispose();
        Assert.True(composition.TrySetFlyoutOpen(true));
        menu.Content!.Select();
        f.Key(Keys.BrowserBack);
        Assert.False(composition.IsFlyoutOpen);
        Assert.Same(composition, f.Shell.CurrentPage);
        innerRoot.Content!.Select();
        f.Key(Keys.BrowserBack);
        Assert.Same(home, f.Shell.CurrentPage);
        Assert.Null(composition.Parent);
        Assert.Same(home.Content, f.Focused);
        composition.Dispose();
    }

    [Fact]
    public void UnfinishedFactoriesRemainBoundedAndReleaseTheirSlotsWhenTheyFinish()
    {
        using var f = new ShellFixture(false);
        var sources = new List<TaskCompletionSource<Page>>();
        f.Shell.Routes.Register("slow/{id}", (_, _) =>
        {
            var source = new TaskCompletionSource<Page>(); sources.Add(source); return source.Task;
        });
        var visits = Enumerable.Range(0, AppShell.MaximumPendingRequests)
            .Select(index => f.Shell.GoToAsync("/slow/" + index, animated: false)).ToArray();
        Assert.Equal(AppShell.MaximumPendingRequests, f.Shell.PendingRequestCount);
        Assert.Throws<InvalidOperationException>(() => { _ = f.Shell.GoToAsync("/slow/overflow"); });
        Assert.Equal(AppShell.MaximumPendingRequests, sources.Count);
        for (int index = 0; index < sources.Count; index++)
        {
            var page = NewPage("late " + index);
            f.CompleteFactory(sources[index], page);
            Assert.Equal(index == sources.Count - 1, Complete(visits[index]));
            if (index < sources.Count - 1) { Assert.Null(page.Parent); page.Dispose(); }
        }
        Assert.Equal(0, f.Shell.PendingRequestCount);
        Assert.Single(f.Shell.NavigationStack);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InvalidOrThrowingFactoriesFaultWithoutAdmittingPagesAndReleaseTheRequest(int kind)
    {
        using var f = new ShellFixture(false);
        f.Shell.Routes.Register("bad", (_, _) => kind switch
        {
            0 => null!,
            1 => Task.FromResult<Page>(null!),
            _ => throw new InvalidOperationException("factory")
        });
        Assert.Throws<InvalidOperationException>(() => Complete(f.Shell.GoToAsync("/bad", animated: false)));
        Assert.Null(f.Shell.CurrentPage);
        Assert.Equal(0, f.Shell.PendingRequestCount);
        Assert.False(f.Shell.IsNavigating);
    }

    [Fact]
    public void ReaffirmingTheCanonicalRouteCancelsOlderWorkWithoutReinvokingTheCurrentFactory()
    {
        using var f = new ShellFixture(false);
        int calls = 0;
        f.Shell.Routes.Register("home", _ => { calls++; return NewPage("home"); });
        var source = new TaskCompletionSource<Page>();
        var late = NewPage("late");
        f.Shell.Routes.Register("late", (_, _) => source.Task);
        Complete(f.Shell.GoToAsync("/home?b=2&a=1", animated: false));
        var pending = f.Shell.GoToAsync("/late", animated: false);
        Assert.True(Complete(f.Shell.GoToAsync("/home?a=1&b=2", animated: false)));
        Assert.Equal(1, calls);
        f.CompleteFactory(source, late);
        Assert.False(Complete(pending));
        Assert.Single(f.Shell.NavigationStack);
        Assert.Null(late.Parent);
        late.Dispose();
    }

    [Fact]
    public void IndependentShellsUseTheirOwnTablesAndDoNotReplaceApplicationBindings()
    {
        using var f = new ShellFixture(false);
        using var other = new AppShell();
        var applicationBindings = Application.InputBindings;
        int baseline = applicationBindings.Count;
        f.Shell.Routes.Register("home", _ => NewPage("first"));
        other.Routes.Register("home", _ => NewPage("second"));
        Complete(f.Shell.GoToAsync("/home", animated: false));
        Complete(other.GoToAsync("/home", animated: false));
        Assert.Equal("first", f.Shell.CurrentPage!.Title);
        Assert.Equal("second", other.CurrentPage!.Title);
        Assert.NotSame(f.Shell.CurrentPage, other.CurrentPage);
        Assert.Same(applicationBindings, Application.InputBindings);
        Assert.Equal(baseline, applicationBindings.Count);
    }
}
