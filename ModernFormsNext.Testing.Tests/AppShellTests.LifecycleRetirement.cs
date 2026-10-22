using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class AppShellTests
{
    [Fact]
    public void StartupActivationSurvivesItsFirstMount()
    {
        using var f = new ShellFixture(false, mount: false);
        f.Shell.TransitionDuration = TimeSpan.Zero;
        f.Shell.Routes.Register("startup", _ => NewPage("startup"));
        using var binding = f.Shell.AttachLifecycle(Application.Lifecycle, new Uri("example://app/"), "main");
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Uri, uri: new Uri("example://app/startup")));
        f.Mount();
        f.Host.Dispatcher.Drain();
        Assert.True(Complete(binding.LastOperation!));
        Assert.Equal("/startup", f.Shell.CurrentRoute!.Route);
        Assert.True(f.Shell.IsPresented);
    }

    [Fact]
    public void HidingThePresentedShellRetiresQueuedActivationWithoutCallingItsFactory()
    {
        using var f = new ShellFixture(false);
        int calls = 0;
        f.Shell.Routes.Register("queued", _ => { calls++; return NewPage("queued"); });
        using var binding = f.Shell.AttachLifecycle(Application.Lifecycle, new Uri("example://app/"), "main");
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Uri, uri: new Uri("example://app/queued")));
        f.Root.Visible = false;
        f.Host.Dispatcher.Drain();
        Assert.False(Complete(binding.LastOperation!));
        Assert.Equal(0, calls);
        Assert.Null(f.Shell.CurrentPage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuspensionOrBindingDisposalRetiresInFlightFactoryWithoutTakingItsLatePage(bool dispose)
    {
        using var f = new ShellFixture(false);
        var source = new TaskCompletionSource<Page>();
        var late = NewPage("late");
        CancellationToken token = default;
        f.Shell.Routes.Register("late", (_, cancellation) => { token = cancellation; return source.Task; });
        using var binding = f.Shell.AttachLifecycle(Application.Lifecycle, new Uri("example://app/"), "main");
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Uri, uri: new Uri("example://app/late")));
        f.Host.Dispatcher.Drain();
        Assert.False(binding.LastOperation!.IsCompleted);
        if (dispose) binding.Dispose();
        else
        {
            var current = f.Host.Services.Lifecycle.Snapshot;
            f.Host.Services.Lifecycle.Publish(new(PlatformApplicationPhase.Suspended,
                PlatformApplicationLifecycleState.Background, false, current.HostCount, current.HostGeneration));
        }
        Assert.True(token.IsCancellationRequested);
        f.CompleteFactory(source, late);
        DrainUntil(f, binding.LastOperation!);
        Assert.True(binding.LastOperation!.IsCanceled);
        Assert.Null(f.Shell.CurrentPage);
        Assert.Null(late.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, late.LifecycleState);
        Assert.Equal(0, f.Shell.PendingRequestCount);
        Assert.Empty(f.Host.Dispatcher.UnhandledExceptions);
        late.Dispose();
    }

    [Fact]
    public void ReadOnlyNotificationProviderSupportsRoutesAndIgnoresStaleSequences()
    {
        using var f = new ShellFixture(false);
        var provider = new ReadOnlyLifecycle(f.Host.Services.Lifecycle);
        using var services = OverrideShellLifecycle(provider);
        f.Shell.TransitionDuration = TimeSpan.Zero;
        f.Shell.Routes.Register("page/{id}", context => NewPage(context.Parameters["id"]));
        var facade = Application.Lifecycle;
        using var binding = f.Shell.AttachLifecycle(facade, new Uri("example://app/"), "main");
        provider.Activate("example://app/page/10", 10);
        var accepted = binding.LastOperation!;
        provider.Activate("example://app/page/9", 9);
        provider.Activate("example://app/page/duplicate", 10);
        Assert.Same(accepted, binding.LastOperation);
        Assert.Throws<NotSupportedException>(() => facade.DeliverActivation(new(PlatformActivationKind.Launch)));
        f.Host.Dispatcher.Drain();
        Assert.True(Complete(accepted));
        Assert.Equal("/page/10", f.Shell.CurrentRoute!.Route);
        Assert.Same(facade, Application.Lifecycle);
    }

    [Fact]
    public void ProviderReplacementRejectsLateFactoryWithoutReplacingApplicationServices()
    {
        using var f = new ShellFixture(false);
        var original = new ReadOnlyLifecycle(f.Host.Services.Lifecycle);
        using var services = OverrideShellLifecycle(original);
        var source = new TaskCompletionSource<Page>();
        var late = NewPage("late");
        f.Shell.Routes.Register("late", (_, _) => source.Task);
        using var binding = f.Shell.AttachLifecycle(Application.Lifecycle, new Uri("example://app/"), "main");
        original.Activate("example://app/late", 1);
        f.Host.Dispatcher.Drain();
        var replacement = new ReadOnlyLifecycle(f.Host.Services.Lifecycle);
        // Revoke the shared scope holder in already-captured ExecutionContexts too. A merely
        // nested AsyncLocal override changes only the test caller's context, not the old request.
        services.Dispose();
        using var replaced = OverrideShellLifecycle(replacement);
        var facade = Application.Lifecycle;
        f.CompleteFactory(source, late);
        DrainUntil(f, binding.LastOperation!);
        Assert.False(Complete(binding.LastOperation!));
        Assert.Null(f.Shell.CurrentPage);
        Assert.Null(late.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, late.LifecycleState);
        Assert.Throws<NotSupportedException>(() => facade.DeliverActivation(new(PlatformActivationKind.Launch)));
        late.Dispose();
    }

    [Theory]
    [InlineData("example://elsewhere/base/page", false)]
    [InlineData("example://app/baseball/page", false)]
    [InlineData("example://app/base/page", true)]
    public void ExplicitUriBaseUsesAuthorityAndWholePathBoundaries(string uri, bool accepted)
    {
        using var f = new ShellFixture(false);
        f.Shell.TransitionDuration = TimeSpan.Zero;
        f.Shell.Routes.Register("page", _ => NewPage("page"));
        using var binding = f.Shell.AttachLifecycle(Application.Lifecycle, new Uri("example://app/base/"), "main");
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Uri, uri: new Uri(uri)));
        f.Host.Dispatcher.Drain();
        if (accepted) Assert.True(Complete(binding.LastOperation!));
        else { Assert.Null(binding.LastOperation); Assert.Null(f.Shell.CurrentPage); }
    }

    [Fact]
    public void FailureObserverExceptionIsAggregatedIntoTheObservedOperation()
    {
        using var f = new ShellFixture(false);
        using var binding = f.Shell.AttachLifecycle(Application.Lifecycle, new Uri("example://app/"), "main");
        binding.OperationFailed += (_, _) => throw new InvalidOperationException("observer");
        Application.Lifecycle.DeliverActivation(new(PlatformActivationKind.Uri, uri: new Uri("example://app/unknown")));
        f.Host.Dispatcher.Drain();
        var failure = Assert.Throws<AggregateException>(() => Complete(binding.LastOperation!));
        Assert.Collection(failure.InnerExceptions, error => Assert.IsType<KeyNotFoundException>(error),
            error => Assert.Equal("observer", error.Message));
        Assert.Empty(f.Host.Dispatcher.UnhandledExceptions);
    }

    private static void DrainUntil(ShellFixture f, Task task)
    {
        // Completion crosses the production scheduler and a public Task boundary. Pump those
        // posted continuations without advancing animation time or running another attempt.
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(10))
        { f.Host.Dispatcher.Drain(); Thread.Yield(); }
        Assert.True(task.IsCompleted, "The controlled factory continuation did not settle.");
    }

    private static IDisposable OverrideShellLifecycle(IPlatformApplicationLifecycle provider)
    {
        var push = typeof(WindowKit.Backend.PlatformServiceRegistry).GetMethod("PushServiceForTesting",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(push);
        return Assert.IsAssignableFrom<IDisposable>(push.MakeGenericMethod(typeof(IPlatformApplicationLifecycle)).Invoke(null, [provider]));
    }

    // A legitimate application override can receive rich native notifications without exposing
    // controller ingress. The shell must borrow it rather than installing a writable provider.
    private sealed class ReadOnlyLifecycle(IPlatformApplicationLifecycleNotifications inner) : IPlatformApplicationLifecycleNotifications
    {
        public PlatformApplicationLifecycleState State => inner.State;
        public PlatformApplicationLifecycleSnapshot Snapshot => inner.Snapshot;
        public PlatformApplicationActivation? LastActivation { get; private set; }
        public event EventHandler<PlatformApplicationLifecycleChangedEventArgs>? StateChanged
        { add => inner.StateChanged += value; remove => inner.StateChanged -= value; }
        public event EventHandler<PlatformApplicationLifecycleEventArgs>? LifecycleChanged
        { add => inner.LifecycleChanged += value; remove => inner.LifecycleChanged -= value; }
        public event EventHandler<PlatformApplicationStateSavingEventArgs>? StateSaving
        { add => inner.StateSaving += value; remove => inner.StateSaving -= value; }
        public event EventHandler<PlatformApplicationStateRestoringEventArgs>? StateRestoring
        { add => inner.StateRestoring += value; remove => inner.StateRestoring -= value; }
        public event EventHandler<PlatformApplicationActivationEventArgs>? ActivationReceived;
        internal void Activate(string uri, long sequence)
        {
            LastActivation = new(PlatformActivationKind.Uri, uri: new Uri(uri));
            ActivationReceived?.Invoke(this, new(LastActivation, sequence));
        }
    }
}
