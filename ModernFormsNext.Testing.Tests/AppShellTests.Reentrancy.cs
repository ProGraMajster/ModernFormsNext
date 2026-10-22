using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class AppShellTests
{
    [Fact]
    public void BackCommandObserverReentryHasTheSameBoundAsRouteAndRestoreCallbacks()
    {
        using var f = new ShellFixture(false);
        f.Shell.Routes.Register("home", _ => NewPage("home"));
        int callbacks = 0;
        EventHandler observer = (_, _) =>
        {
            // Stop independently at40 so the original bug is a finite failure, never a process
            // stack overflow. Production must reject the documented32nested calls first.
            if (++callbacks < 40) _ = f.Shell.GoBackAsync(animated: false);
        };
        f.Shell.NavigateCommand.CanExecuteChanged += observer;
        var failure = Record.Exception(() => { _ = f.Shell.GoBackAsync(animated: false); });
        Assert.True(failure is InvalidOperationException && failure.Message.Contains("32", StringComparison.Ordinal),
            $"Expected the32-call guard; callbacks={callbacks}, failure={failure?.GetType().Name ?? "none"}.");
        Assert.InRange(callbacks, 1, 32);
        f.Shell.NavigateCommand.CanExecuteChanged -= observer;
        Assert.True(Complete(f.Shell.GoToAsync("/home", animated: false)));
        Assert.Equal(0, f.Shell.PendingRequestCount);
    }

    [Fact]
    public void CompletionObserverNavigationRetiresTheOlderTaskResult()
    {
        using var f = new ShellFixture(false);
        var old = NewPage("old"); var winner = NewPage("winner");
        f.Shell.Routes.Register("old", _ => old);
        f.Shell.Routes.Register("winner", _ => winner);
        bool redirected = false;
        Task<bool>? newer = null;
        f.Shell.NavigateCommand.CanExecuteChanged += (_, _) =>
        {
            if (!redirected && !f.Shell.IsNavigating && f.Shell.CurrentRoute?.Route == "/old")
            {
                redirected = true;
                newer = f.Shell.GoToAsync("/winner", animated: false);
            }
        };
        Assert.False(Complete(f.Shell.GoToAsync("/old", animated: false)));
        Assert.True(Complete(newer!));
        Assert.Same(winner, f.Shell.CurrentPage);
        Assert.Equal("/winner", f.Shell.CurrentRoute!.Route);
        Assert.Equal(0, f.Shell.PendingRequestCount);
    }

    [Fact]
    public void BackCannotPopANewerVisitStartedByCancellationCallback()
    {
        using var f = new ShellFixture(false);
        var source = new TaskCompletionSource<Page>();
        var home = NewPage("home"); var detail = NewPage("detail");
        var winner = NewPage("winner"); var borrowed = NewPage("late");
        f.Shell.Routes.Register("home", _ => home);
        f.Shell.Routes.Register("detail", _ => detail);
        f.Shell.Routes.Register("winner", _ => winner);
        f.Shell.Routes.Register("slow", (_, token) =>
        {
            token.Register(() => f.Shell.GoToAsync("/winner", animated: false));
            return source.Task;
        });
        Complete(f.Shell.GoToAsync("/home", animated: false));
        Complete(f.Shell.GoToAsync("/detail", animated: false));
        var pending = f.Shell.GoToAsync("/slow", animated: false);
        Assert.Null(Complete(f.Shell.GoBackAsync(animated: false)));
        Assert.Same(winner, f.Shell.CurrentPage);
        Assert.Equal("/winner", f.Shell.CurrentRoute!.Route);
        f.CompleteFactory(source, borrowed);
        Assert.False(Complete(pending));
        Assert.Null(borrowed.Parent);
        borrowed.Dispose();
    }

    [Fact]
    public void NavigationStartedByCancellationCallbackSupersedesTheOlderOuterRequest()
    {
        using var f = new ShellFixture(false);
        var source = new TaskCompletionSource<Page>();
        var borrowed = NewPage("late"); var winner = NewPage("winner"); var outer = NewPage("outer");
        Task<bool>? newer = null;
        f.Shell.Routes.Register("slow", (_, token) =>
        {
            token.Register(() => newer = f.Shell.GoToAsync("/winner", animated: false));
            return source.Task;
        });
        f.Shell.Routes.Register("winner", _ => winner);
        f.Shell.Routes.Register("outer", _ => outer);
        var older = f.Shell.GoToAsync("/slow", animated: false);
        var attempt = f.Shell.GoToAsync("/outer", animated: false);
        f.CompleteFactory(source, borrowed);
        Assert.False(Complete(attempt));
        Assert.False(Complete(older));
        Assert.True(Complete(newer!));
        Assert.Same(winner, f.Shell.CurrentPage);
        Assert.Equal("/winner", f.Shell.CurrentRoute!.Route);
        Assert.Null(outer.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, outer.LifecycleState);
        Assert.Null(borrowed.Parent);
        borrowed.Dispose(); outer.Dispose();
    }
}
