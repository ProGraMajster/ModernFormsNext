using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ApplicationLifetimeTests
{
    [Fact]
    public void ClosingFromShownExitsWithoutEnteringAnIdleLoop()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var exits = 0;
        Application.OnExit += (_, _) => exits++;
        form.Shown += (_, _) => form.Close();
        Application.Run(form);
        Assert.Equal(1, exits);
        Assert.Empty(Application.OpenForms);
        Assert.False(form.IsActive);
        Assert.False(form.Visible);
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
        Assert.Throws<InvalidOperationException>(() => Application.Run(form));
    }

    [Fact]
    public void AlreadyTerminatedProviderCannotStartOrShowAnApplication()
    {
        using var host = ModernFormsTestHost.Create();
        host.Services.Lifecycle.Publish(new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Exited, PlatformApplicationLifecycleState.NoHost, hostGeneration: 1));
        using var form = new Form();
        var shown = false;
        form.Shown += (_, _) => shown = true;
        Assert.Throws<InvalidOperationException>(() => Application.Run(form));
        Assert.False(shown);
        Assert.Empty(Application.OpenForms);
    }

    [Fact]
    public void ExitIsReentrantIdempotentAndContinuesAfterFailingObservers()
    {
        using var host = ModernFormsTestHost.Create();
        var calls = new List<int>();
        Application.OnExit += (_, _) => { calls.Add(1); Application.Exit(); throw new InvalidOperationException("exit observer"); };
        Application.OnExit += (_, _) => calls.Add(2);
        var error = Assert.Throws<InvalidOperationException>(Application.Exit);
        Assert.Equal("exit observer", error.Message);
        Application.Exit();
        Assert.Equal([1, 2], calls);
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
    }

    [Fact]
    public void ExitRequestedFromStartingOrdersSaveAndCleanupAfterLifecycleNotification()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var order = new List<string>();
        Application.Lifecycle.LifecycleChanged += (_, args) =>
        {
            order.Add(args.Current.Phase.ToString());
            if (args.Current.Phase == PlatformApplicationPhase.Starting)
                Application.Exit();
        };
        Application.Lifecycle.StateSaving += (_, _) => order.Add("save");
        Application.OnExit += (_, _) => order.Add("cleanup");
        form.Shown += (_, _) => order.Add("shown");
        Application.Run(form);
        Assert.Equal(["Starting", "Exiting", "save", "cleanup", "Exited"], order);
        host.Dispatcher.Drain();
        host.Dispatcher.ThrowUnhandledExceptions();
        Assert.Equal(5, order.Count);
    }

    [Fact]
    public void ExitRequestedFromBackgroundCallbackCompletesOnNextUiDrain()
    {
        using var host = ModernFormsTestHost.Create();
        Application.Lifecycle.LifecycleChanged += (_, args) =>
        {
            if (args.Current.State == PlatformApplicationLifecycleState.Background)
                Application.Exit();
        };
        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background);
        host.Dispatcher.Drain();
        host.Dispatcher.ThrowUnhandledExceptions();
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
    }

    [Fact]
    public void StartupFailureStillRaisesExitAndPreservesBothErrors()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Shown += (_, _) => throw new InvalidOperationException("shown");
        Application.OnExit += (_, _) => throw new ArgumentException("exit");
        var error = Assert.Throws<AggregateException>(() => Application.Run(form));
        Assert.Contains(error.InnerExceptions, e => e.Message == "shown");
        Assert.Contains(error.InnerExceptions, e => e.Message == "exit");
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
    }

    [Theory]
    [InlineData(ApplicationLifetimeMode.MainWindowClosed)]
    [InlineData(ApplicationLifetimeMode.LastWindowClosed)]
    [InlineData(ApplicationLifetimeMode.Explicit)]
    public void MultipleFormsUseSelectedLifetimePolicy(ApplicationLifetimeMode mode)
    {
        using var host = ModernFormsTestHost.Create();
        using var main = new Form();
        using var secondary = new Form();
        var steps = new List<string>();
        main.Shown += (_, _) => secondary.Show();
        host.Dispatcher.Post(() => { steps.Add("main"); main.Close(); });
        host.Dispatcher.Post(() => { steps.Add("secondary"); secondary.Close(); });
        host.Dispatcher.Post(() => { steps.Add("explicit"); Application.Exit(); });
        Application.Run(main, mode);
        var expected = mode switch
        {
            ApplicationLifetimeMode.MainWindowClosed => new[] { "main" },
            ApplicationLifetimeMode.LastWindowClosed => ["main", "secondary"],
            _ => ["main", "secondary", "explicit"]
        };
        Assert.Equal(expected, steps);
        // Any deliberately pending callbacks are still owned by this host and safe to drain.
        host.Dispatcher.Drain();
        host.Dispatcher.ThrowUnhandledExceptions();
    }

    [Fact]
    public void FailingClosedObserverCannotPreventLifetimeExitOrOtherObservers()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var notified = false;
        form.Closed += (_, _) => throw new InvalidOperationException("closed");
        form.Closed += (_, _) => notified = true;
        host.Dispatcher.Post(form.Close);
        Application.Run(form);
        Assert.True(notified);
        Assert.Empty(Application.OpenForms);
        Assert.Equal("closed", Assert.Single(host.Dispatcher.UnhandledExceptions).Message);
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
    }

    [Fact]
    public void ClosingDuringActivationDoesNotReinsertClosedFormOrRaiseShown()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var shown = false;
        form.Activated += (_, _) => form.Close();
        form.Shown += (_, _) => shown = true;
        Application.Run(form);
        Assert.False(shown);
        Assert.False(form.Visible);
        Assert.Empty(Application.OpenForms);
    }

    [Fact]
    public void CanceledCloseKeepsApplicationRunningUntilExplicitExit()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        form.Closing += (_, args) => args.Cancel = true;
        var afterClose = false;
        host.Dispatcher.Post(form.Close);
        host.Dispatcher.Post(() => { afterClose = true; Application.Exit(); });
        Application.Run(form);
        Assert.True(afterClose);
        Assert.Contains(form, Application.OpenForms);
    }

    [Fact]
    public void CustomLifetimeRootSubscriptionIsDetachedAfterRun()
    {
        using var host = ModernFormsTestHost.Create();
        var root = new LifetimeRoot();
        host.Dispatcher.Post(root.Close);
        Application.Run(root);
        Assert.Equal(0, root.SubscriptionCount);
    }

    [Fact]
    public void WindowActivationAndInsetsRemainSeparateFromApplicationActivity()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var window = host.Show(form);
        var original = host.Services.Lifecycle.Snapshot;
        window.SetActive(false);
        Assert.False(form.IsActive);
        Assert.Equal(original, host.Services.Lifecycle.Snapshot);
        window.SetActive(true);
        Assert.True(form.IsActive);
        var changes = 0;
        form.InsetsChanged += (_, args) => { changes++; Assert.Equal(args.Insets, form.Insets); };
        var insets = new WindowInsets(new Thickness(5, 6, 7, 8), new Thickness(0, 0, 0, 90));
        window.SetInsets(insets);
        window.SetInsets(insets);
        Assert.Equal(1, changes);
        Assert.Equal(insets, form.Insets);
        window.Close();
        Assert.False(form.IsActive);
        Assert.Throws<ObjectDisposedException>(() => window.SetInsets(default));
    }

    [Fact]
    public void DisposingHostInsideRunDoesNotPoisonNextApplicationRuntime()
    {
        var host = ModernFormsTestHost.Create();
        var root = new LifetimeRoot();
        host.Dispatcher.Post(host.Dispose);
        Application.Run(root, ApplicationLifetimeMode.Explicit);
        using var next = ModernFormsTestHost.Create();
        var nextRoot = new LifetimeRoot();
        next.Dispatcher.Post(nextRoot.Close);
        Application.Run(nextRoot);
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
    }

    private sealed class LifetimeRoot : ICloseable
    {
        private EventHandler? closed;
        public int SubscriptionCount { get; private set; }
        public event EventHandler? Closed
        {
            add { closed += value; SubscriptionCount++; }
            remove { closed -= value; SubscriptionCount--; }
        }
        public void Close() => closed?.Invoke(this, EventArgs.Empty);
    }
}
