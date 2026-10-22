using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class AppShellTests
{
    [Fact]
    public void ExplicitUriBindingCoalescesDataWithoutRegisteringTransportOrFactoriesDuringMatch()
    {
        using var f = new ShellFixture(false);
        f.Shell.TransitionDuration = TimeSpan.Zero;
        int calls = 0;
        f.Shell.Routes.Register("documents/{id}", context => { calls++; return NewPage(context.Parameters["id"]); });
        var lifecycle = Application.Lifecycle;
        using var binding = f.Shell.AttachLifecycle(lifecycle, new Uri("example://app/"), "main");
        lifecycle.DeliverActivation(new(PlatformActivationKind.Protocol, uri: new Uri("other://app/documents/ignored")));
        Assert.Null(binding.LastOperation);
        lifecycle.DeliverActivation(new(PlatformActivationKind.Protocol, uri: new Uri("example://app/documents/1")));
        var first = binding.LastOperation!;
        Assert.Null(f.Shell.CurrentPage);
        lifecycle.DeliverActivation(new(PlatformActivationKind.Uri, uri: new Uri("example://app/documents/2?mode=edit")));
        var latest = binding.LastOperation!;
        Assert.False(Complete(first));
        f.Host.Dispatcher.Drain();
        Assert.True(Complete(latest));
        Assert.Equal(1, calls);
        Assert.Equal("/documents/2?mode=edit", f.Shell.CurrentRoute!.Route);
        Assert.Equal("2", f.Shell.CurrentPage!.Title);
        Assert.Same(lifecycle, Application.Lifecycle);
    }

    [Fact]
    public void LaterApplicationNavigationWinsOverQueuedActivation()
    {
        using var f = new ShellFixture(false);
        f.Shell.TransitionDuration = TimeSpan.Zero;
        int activationCalls = 0;
        f.Shell.Routes.Register("activation", _ => { activationCalls++; return NewPage("activation"); });
        f.Shell.Routes.Register("manual", _ => NewPage("manual"));
        using var binding = f.Shell.AttachLifecycle(Application.Lifecycle, new Uri("example://app/"), "main");
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Protocol, uri: new Uri("example://app/activation")));
        Assert.True(Complete(f.Shell.GoToAsync("/manual", animated: false)));
        f.Host.Dispatcher.Drain();
        Assert.False(Complete(binding.LastOperation!));
        Assert.Equal(0, activationCalls);
        Assert.Equal("/manual", f.Shell.CurrentRoute!.Route);
    }

    [Fact]
    public void ExistingAndLaterApplicationStateAssignmentsRemainAuthoritative()
    {
        using var f = new ShellFixture(false);
        var lifecycle = Application.Lifecycle;
        var applicationData = new PlatformApplicationStateData(7, new Dictionary<string, string> { ["application"] = "keep" });
        EventHandler<PlatformApplicationStateSavingEventArgs> before = (_, args) => args.Data = applicationData;
        lifecycle.StateSaving += before;
        using var binding = f.Shell.AttachLifecycle(lifecycle, null, "main", includeState: true);
        Assert.Same(applicationData, lifecycle.SaveState(PlatformApplicationStateReason.Recreation));
        lifecycle.StateSaving -= before;
        var shellData = lifecycle.SaveState(PlatformApplicationStateReason.Recreation)!;
        Assert.Equal("0", shellData.Values["mfn.shell.main.count"]);
        lifecycle.StateSaving += before;
        Assert.Same(applicationData, lifecycle.SaveState(PlatformApplicationStateReason.Recreation));
        lifecycle.StateSaving -= before;
    }

    [Fact]
    public void AutomaticRestoreUsesTheCanonicalStateHandoffAndKeepsPopulatedUserState()
    {
        using var f = new ShellFixture(false);
        f.Shell.TransitionDuration = TimeSpan.Zero;
        f.Shell.Routes.Register("home", _ => NewPage("home"));
        f.Shell.Routes.Register("manual", _ => NewPage("manual"));
        using var binding = f.Shell.AttachLifecycle(Application.Lifecycle, null, "main", includeState: true);
        var data = new PlatformApplicationStateData(1, new Dictionary<string, string>
        { ["mfn.shell.main.count"] = "1", ["mfn.shell.main.0"] = "/home" });
        Application.Lifecycle.RestoreState(data, PlatformApplicationStateReason.Recreation);
        f.Host.Dispatcher.Drain();
        Assert.True(Complete(binding.LastOperation!));
        Assert.Equal("/home", f.Shell.CurrentRoute!.Route);
        Complete(f.Shell.GoToAsync("/manual", animated: false));
        Application.Lifecycle.RestoreState(data, PlatformApplicationStateReason.Recreation);
        f.Host.Dispatcher.Drain();
        Assert.False(Complete(binding.LastOperation!));
        Assert.Equal("/manual", f.Shell.CurrentRoute!.Route);
        Assert.Equal(2, f.Shell.NavigationStack.Count);
    }

    [Fact]
    public void BackgroundActivationWaitsForForegroundAndKeepsOnlyTheNewestData()
    {
        using var f = new ShellFixture(false);
        f.Shell.TransitionDuration = TimeSpan.Zero;
        f.Shell.Routes.Register("page/{id}", context => NewPage(context.Parameters["id"]));
        using var binding = f.Shell.AttachLifecycle(Application.Lifecycle, new Uri("example://app/"), "main");
        f.Host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background);
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Uri, uri: new Uri("example://app/page/1")));
        var older = binding.LastOperation!;
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Uri, uri: new Uri("example://app/page/2")));
        f.Host.Dispatcher.Drain();
        Assert.False(Complete(older));
        Assert.False(binding.LastOperation!.IsCompleted);
        Assert.Null(f.Shell.CurrentPage);
        f.Host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);
        f.Host.Dispatcher.Drain();
        Assert.True(Complete(binding.LastOperation!));
        Assert.Equal("/page/2", f.Shell.CurrentRoute!.Route);
    }

    [Fact]
    public void DisposedBindingRetiresQueuedDataButDoesNotDisposeBorrowedFacadeOrShell()
    {
        using var f = new ShellFixture(false);
        f.Shell.Routes.Register("page", _ => NewPage("page"));
        var lifecycle = Application.Lifecycle;
        var binding = f.Shell.AttachLifecycle(lifecycle, new Uri("example://app/"), "main");
        lifecycle.DeliverActivation(new(PlatformActivationKind.Uri, uri: new Uri("example://app/page")));
        var queued = binding.LastOperation!;
        binding.Dispose(); binding.Dispose();
        f.Host.Dispatcher.Drain();
        Assert.True(binding.IsDisposed);
        Assert.False(Complete(queued));
        Assert.Null(f.Shell.CurrentPage);
        Assert.Same(lifecycle, Application.Lifecycle);
        Assert.True(Complete(f.Shell.GoToAsync("/page", animated: false)));
        using var replacement = f.Shell.AttachLifecycle(lifecycle, null, "main", includeState: true);
    }

    [Fact]
    public void UnknownActivationRouteIsObservedThroughTaskAndFailureMetadata()
    {
        using var f = new ShellFixture(false);
        using var binding = f.Shell.AttachLifecycle(Application.Lifecycle, new Uri("example://app/"), "main");
        ShellLifecycleFailureEventArgs? failure = null;
        binding.OperationFailed += (_, args) => failure = args;
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Protocol, uri: new Uri("example://app/unknown")));
        f.Host.Dispatcher.Drain();
        Assert.Throws<KeyNotFoundException>(() => Complete(binding.LastOperation!));
        Assert.NotNull(failure);
        Assert.IsType<KeyNotFoundException>(failure.Exception);
        Assert.False(failure.IsStateRestoration);
        Assert.True(failure.Sequence > 0);
        Assert.Null(f.Shell.CurrentPage);
        Assert.Empty(f.Host.Dispatcher.UnhandledExceptions);
    }
}
