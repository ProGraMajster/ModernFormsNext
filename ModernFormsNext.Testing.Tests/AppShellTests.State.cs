using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class AppShellTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RouteOnlyStateRebuildsAFreshShellAndPreservesRetainedUserHistory(bool neutral)
    {
        PlatformApplicationStateData saved;
        using (var original = new ShellFixture(neutral))
        {
            original.Shell.Routes.Register("home", _ => NewPage("home"));
            original.Shell.Routes.Register("documents/{id}", _ => NewPage("document"));
            Complete(original.Shell.GoToAsync("/home", animated: false));
            Complete(original.Shell.GoToAsync("/documents/42?mode=edit", animated: false));
            saved = original.Shell.CaptureState("main");
            Assert.Equal(AppShell.SavedStateVersion, saved.Version);
            Assert.Equal(3, saved.Values.Count);
            Assert.DoesNotContain("TextBox", string.Join(' ', saved.Values.Values));
            Assert.False(Complete(original.Shell.RestoreStateAsync(saved, "main")));
            Assert.Equal(2, original.Shell.NavigationStack.Count);
        }
        using var restored = new ShellFixture(neutral);
        restored.Shell.Routes.Register("home", _ => NewPage("new home"));
        restored.Shell.Routes.Register("documents/{id}", context => NewPage(context.Parameters["id"]));
        Assert.True(Complete(restored.Shell.RestoreStateAsync(saved, "main")));
        Assert.Equal(2, restored.Shell.NavigationStack.Count);
        Assert.Equal("/documents/42?mode=edit", restored.Shell.CurrentRoute!.Route);
        Assert.Equal("42", restored.Shell.CurrentPage!.Title);
        var removed = Complete(restored.Shell.GoBackAsync(animated: false));
        Assert.Equal("/home", restored.Shell.CurrentRoute!.Route);
        removed!.Dispose();
    }

    [Fact]
    public void EverySnapshotRouteIsValidatedBeforeTheFirstFactoryRuns()
    {
        using var f = new ShellFixture(false);
        int calls = 0;
        f.Shell.Routes.Register("home", _ => { calls++; return NewPage("home"); });
        var data = new PlatformApplicationStateData(1, new Dictionary<string, string>
        { ["mfn.shell.main.count"] = "2", ["mfn.shell.main.0"] = "/home", ["mfn.shell.main.1"] = "/unknown" });
        Assert.False(Complete(f.Shell.RestoreStateAsync(data, "main")));
        Assert.Equal(0, calls);
        Assert.Null(f.Shell.CurrentPage);
        var malformed = new PlatformApplicationStateData(1, new Dictionary<string, string>
        { ["mfn.shell.main.count"] = "1", ["mfn.shell.main.0"] = "/home?bad=%FF" });
        Assert.False(Complete(f.Shell.RestoreStateAsync(malformed, "main")));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void StateSchemaNamespaceAndCanonicalCountDoNotExecuteUnknownData()
    {
        using var f = new ShellFixture(false);
        f.Shell.Routes.Register("home", _ => throw new InvalidOperationException("Do not invoke."));
        foreach (var value in new[] { "01", "-1", "33", "9999999999999999999", " " })
            Assert.False(Complete(f.Shell.RestoreStateAsync(new(1, new Dictionary<string, string> { ["mfn.shell.main.count"] = value }), "main")));
        Assert.False(Complete(f.Shell.RestoreStateAsync(new(2, new Dictionary<string, string> { ["mfn.shell.main.count"] = "0" }), "main")));
        Assert.False(Complete(f.Shell.RestoreStateAsync(new(1, new Dictionary<string, string> { ["mfn.shell.other.count"] = "0" }), "main")));
        Assert.False(Complete(f.Shell.RestoreStateAsync(new(1, new Dictionary<string, string> { ["mfn.shell.main.count"] = "0", ["mfn.shell.main.extra"] = "payload" }), "main")));
        Assert.True(Complete(f.Shell.RestoreStateAsync(new(1, new Dictionary<string, string> { ["mfn.shell.main.count"] = "0", ["application.other"] = "preserved" }), "main")));
        Assert.Throws<ArgumentException>(() => f.Shell.CaptureState("bad.key"));
        Assert.Throws<ArgumentException>(() => f.Shell.CaptureState(new string('k', 65)));
        Assert.Null(f.Shell.CurrentPage);
    }

    [Fact]
    public void RestoreStopsBeforeAnOlderHistoryEntryCanOverwriteCallbackNavigation()
    {
        using var f = new ShellFixture(false);
        f.Shell.Routes.Register("home", _ => NewPage("home"));
        f.Shell.Routes.Register("old", _ => NewPage("old"));
        f.Shell.Routes.Register("winner", _ => NewPage("winner"));
        var data = new PlatformApplicationStateData(1, new Dictionary<string, string>
        { ["mfn.shell.main.count"] = "2", ["mfn.shell.main.0"] = "/home", ["mfn.shell.main.1"] = "/old" });
        bool redirected = false;
        f.Shell.CurrentRouteChanged += (_, _) =>
        {
            if (!redirected) { redirected = true; f.Shell.GoToAsync("/winner", animated: false); }
        };
        Assert.False(Complete(f.Shell.RestoreStateAsync(data, "main")));
        Assert.Equal("/winner", f.Shell.CurrentRoute!.Route);
        Assert.Equal(2, f.Shell.NavigationStack.Count);
        Assert.DoesNotContain(f.Shell.NavigationStack, page => page.Title == "old");
    }

    [Fact]
    public void FailedRestoreFactoryLeavesTheAlreadyCommittedPrefixOwned()
    {
        using var f = new ShellFixture(false);
        f.Shell.Routes.Register("home", _ => NewPage("home"));
        f.Shell.Routes.Register("fault", _ => throw new InvalidOperationException("factory"));
        var data = new PlatformApplicationStateData(1, new Dictionary<string, string>
        { ["mfn.shell.main.count"] = "2", ["mfn.shell.main.0"] = "/home", ["mfn.shell.main.1"] = "/fault" });
        Assert.Throws<InvalidOperationException>(() => Complete(f.Shell.RestoreStateAsync(data, "main")));
        Assert.Equal("/home", f.Shell.CurrentRoute!.Route);
        Assert.Single(f.Shell.NavigationStack);
        Assert.Equal(PageLifecycleState.Appeared, f.Shell.CurrentPage!.LifecycleState);
    }

    [Fact]
    public void SnapshotDepthAndPreCanceledRestoreRemainBoundedWithoutResettingHistory()
    {
        using var f = new ShellFixture(false);
        f.Shell.Routes.Register("page/{id}", context => NewPage(context.Parameters["id"]));
        var empty = f.Shell.CaptureState("main");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.True(f.Shell.RestoreStateAsync(empty, "main", cancellationToken: cancellation.Token).IsCanceled);
        for (int index = 0; index <= AppShell.MaximumSavedRouteCount; index++)
            Assert.True(Complete(f.Shell.GoToAsync("/page/" + index, animated: false)));
        Assert.Throws<InvalidOperationException>(() => f.Shell.CaptureState("main"));
        Assert.Equal(AppShell.MaximumSavedRouteCount + 1, f.Shell.NavigationStack.Count);
        Assert.False(Complete(f.Shell.RestoreStateAsync(empty, "main")));
        Assert.Equal("/page/32", f.Shell.CurrentRoute!.Route);
    }
}
