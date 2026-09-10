using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Threading;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ApplicationLifecycleFacadeTests
{
    [Fact]
    public void PreBootstrapSubscriptionDoesNotCacheAnUninitializedDispatcher()
    {
        // This assembly serializes its tests and never initializes a native backend. Preserve
        // the borrowed dispatcher exactly while exercising the process's pre-bootstrap state;
        // reflection avoids exposing a production reset API solely for this regression.
        Assert.Null(WindowKit.Backend.PlatformServiceRegistry.GetService<IPlatformApplicationLifecycle>());
        var dispatcherField = typeof(Dispatcher).GetField("s_uiThread",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(dispatcherField);
        object? borrowedDispatcher = dispatcherField.GetValue(null);
        ApplicationLifecycle? lifecycle = null;
        EventHandler<PlatformApplicationActivationEventArgs> handler = (_, _) => { };
        try
        {
            dispatcherField.SetValue(null, null);
            lifecycle = Application.Lifecycle;
            lifecycle.ActivationReceived += handler;

            Assert.Null(dispatcherField.GetValue(null));
        }
        finally
        {
            try { if (lifecycle is not null) lifecycle.ActivationReceived -= handler; }
            finally { dispatcherField.SetValue(null, borrowedDispatcher); }
        }
    }

    [Fact]
    public void PublicFacadeForwardsCanonicalStateAndActivationOnTheUiThread()
    {
        using var host = ModernFormsTestHost.Create();
        var lifecycle = Application.Lifecycle;
        var notifications = new List<string>();
        int uiThread = Environment.CurrentManagedThreadId;
        lifecycle.LifecycleChanged += (_, args) =>
        {
            Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
            Assert.Equal(args.Current, lifecycle.Snapshot);
            notifications.Add(args.Current.Phase.ToString());
        };
        lifecycle.ActivationReceived += (_, args) =>
        {
            Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
            notifications.Add(args.Activation.Kind.ToString());
        };
        var provider = host.Services.Lifecycle;
        var current = provider.Snapshot;
        var suspended = new PlatformApplicationLifecycleSnapshot(PlatformApplicationPhase.Suspended,
            PlatformApplicationLifecycleState.Background, false, current.HostCount, current.HostGeneration);
        provider.Publish(suspended);
        provider.Publish(suspended);
        var activation = new PlatformApplicationActivation(PlatformActivationKind.Arguments, ["--open", "private-name"]);
        provider.Activate(activation);
        provider.Publish(current);

        Assert.Equal(new[] { "Suspended", "Arguments", "Running" }, notifications);
        Assert.Same(activation, lifecycle.LastActivation);
        Assert.Equal(current, lifecycle.Snapshot);
        Assert.Equal(2, lifecycle.GetDiagnostics().RecentTransitions.Count);
    }

    [Fact]
    public void ExplicitSaveRestoreHooksUseBoundedProviderDataWithoutSerializingControls()
    {
        using var host = ModernFormsTestHost.Create();
        var lifecycle = Application.Lifecycle;
        var order = new List<string>();
        var state = new PlatformApplicationStateData(1, new Dictionary<string, string> { ["page"] = "private-page" });
        lifecycle.StateSaving += (_, args) =>
        {
            order.Add("save:" + args.Reason);
            args.Data = state;
        };
        lifecycle.StateRestoring += (_, args) =>
        {
            order.Add("restore:" + args.Reason);
            Assert.Equal("private-page", args.Data.Values["page"]);
        };

        var saved = lifecycle.SaveState(PlatformApplicationStateReason.Recreation);
        Assert.NotNull(saved);
        lifecycle.RestoreState(saved, PlatformApplicationStateReason.Recreation);

        Assert.Equal(new[] { "save:Recreation", "restore:Recreation" }, order);
        Assert.DoesNotContain("private-page", System.Text.Json.JsonSerializer.Serialize(lifecycle.GetDiagnostics()));
    }

    [Fact]
    public void PublicDeliveryUsesTheSameProviderAndQueuesReentrantActivationAndRestoration()
    {
        using var host = ModernFormsTestHost.Create();
        var lifecycle = Application.Lifecycle;
        var first = new PlatformApplicationActivation(PlatformActivationKind.Launch);
        var second = new PlatformApplicationActivation(PlatformActivationKind.Protocol, uri: new Uri("example://document/42"));
        var state = new PlatformApplicationStateData(1, new Dictionary<string, string> { ["page"] = "home" });
        var order = new List<string>();
        lifecycle.ActivationReceived += (_, args) =>
        {
            order.Add(args.Activation.Kind.ToString());
            Assert.Same(args.Activation, host.Services.Lifecycle.LastActivation);
            if (ReferenceEquals(args.Activation, first))
            {
                lifecycle.DeliverActivation(second);
                lifecycle.RestoreState(state, PlatformApplicationStateReason.Recreation);
                Assert.Throws<InvalidOperationException>(() => lifecycle.SaveState(PlatformApplicationStateReason.Recreation));
                order.Add("first-finished");
            }
        };
        lifecycle.StateRestoring += (_, args) =>
        {
            Assert.Same(state, args.Data);
            order.Add("restored");
        };

        lifecycle.DeliverActivation(first);

        Assert.Equal(new[] { "Launch", "first-finished", "Protocol", "restored" }, order);
        Assert.Same(second, lifecycle.LastActivation);
    }

    [Fact]
    public void RetainedFacadeCannotDeliverIntoTheNextRuntime()
    {
        ApplicationLifecycle expired;
        using (var host = ModernFormsTestHost.Create()) expired = Application.Lifecycle;
        using var next = ModernFormsTestHost.Create();
        var calls = 0;
        Application.Lifecycle.ActivationReceived += (_, _) => calls++;
        Application.Lifecycle.StateRestoring += (_, _) => calls++;
        Application.Lifecycle.StateSaving += (_, _) => calls++;

        Assert.Throws<ObjectDisposedException>(() => expired.DeliverActivation(new(PlatformActivationKind.Launch)));
        Assert.Throws<ObjectDisposedException>(() => expired.SaveState(PlatformApplicationStateReason.Recreation));
        Assert.Throws<ObjectDisposedException>(() => expired.RestoreState(new(1), PlatformApplicationStateReason.Recreation));

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(PlatformApplicationPhase.Exiting)]
    [InlineData(PlatformApplicationPhase.Exited)]
    public void BackendTerminalNotificationEndsTheExistingApplicationRun(PlatformApplicationPhase phase)
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var order = new List<string>();
        Application.Lifecycle.LifecycleChanged += (_, args) =>
        {
            if (args.Current.Phase == phase) order.Add("terminal");
        };
        Application.OnExit += (_, _) => order.Add("exit");
        host.Dispatcher.Post(() =>
        {
            var current = host.Services.Lifecycle.Snapshot;
            host.Services.Lifecycle.Publish(new(phase, PlatformApplicationLifecycleState.NoHost, false, 0, current.HostGeneration));
            order.Add("published");
            Assert.DoesNotContain("exit", order);
        });

        Application.Run(form);

        Assert.Equal(new[] { "terminal", "published", "exit" }, order);
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void ThrowingTerminalObserverStillEndsRunAndReportsTheOriginalFailure()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var failure = new LifecycleObserverFailure();
        var exitCalls = 0;
        Application.Lifecycle.LifecycleChanged += (_, args) =>
        {
            if (args.Current.Phase == PlatformApplicationPhase.Exiting) throw failure;
        };
        Application.OnExit += (_, _) => exitCalls++;
        host.Dispatcher.Post(() =>
        {
            var current = host.Services.Lifecycle.Snapshot;
            host.Services.Lifecycle.Publish(new(PlatformApplicationPhase.Exiting, current.State,
                false, current.HostCount, current.HostGeneration));
        });

        Application.Run(form);

        Assert.Equal(1, exitCalls);
        Assert.Same(failure, Assert.Single(host.Dispatcher.UnhandledExceptions));
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
    }

    [Fact]
    public void TerminalObserverDisposingHostCannotExitTheRestoredApplication()
    {
        var borrowedInputs = Application.InputBindings;
        var borrowedExitCalls = 0;
        EventHandler borrowedExit = (_, _) => borrowedExitCalls++;
        Application.OnExit += borrowedExit;
        try
        {
            using var host = ModernFormsTestHost.Create();
            Application.Lifecycle.LifecycleChanged += (_, args) =>
            {
                if (args.Current.Phase == PlatformApplicationPhase.Exiting) host.Dispose();
            };
            var current = host.Services.Lifecycle.Snapshot;

            host.Services.Lifecycle.Publish(new(PlatformApplicationPhase.Exiting, current.State,
                false, current.HostCount, current.HostGeneration));

            Assert.Equal(0, borrowedExitCalls);
            Assert.Same(borrowedInputs, Application.InputBindings);
        }
        finally { Application.OnExit -= borrowedExit; }
    }

    [Fact]
    public void DiagnosticsBoundHistoryDetachSnapshotsAndExcludeActivationContents()
    {
        using var host = ModernFormsTestHost.Create();
        var lifecycle = Application.Lifecycle;
        var initial = lifecycle.GetDiagnostics();
        host.Services.Lifecycle.Activate(new PlatformApplicationActivation(PlatformActivationKind.Files,
            files: ["private-document.txt"]));
        for (int index = 0; index < 70; index++)
            host.Services.Lifecycle.SetState(index % 2 == 0
                ? PlatformApplicationLifecycleState.Background
                : PlatformApplicationLifecycleState.Foreground);

        var current = lifecycle.GetDiagnostics();

        Assert.Empty(initial.RecentTransitions);
        Assert.Null(initial.LastActivationKind);
        Assert.Equal(64, current.RecentTransitions.Count);
        Assert.True(current.RecentTransitions.Zip(current.RecentTransitions.Skip(1))
            .All(pair => pair.First.Sequence < pair.Second.Sequence));
        Assert.Equal(PlatformActivationKind.Files, current.LastActivationKind);
        Assert.DoesNotContain("private-document.txt", System.Text.Json.JsonSerializer.Serialize(current));
    }

    [Fact]
    public void WindowDiagnosticsRemainDistinctFromPlatformApplicationActivity()
    {
        using var host = ModernFormsTestHost.Create();
        var first = host.Show(new Form());
        var second = host.Show(new Form());
        var current = host.Services.Lifecycle.Snapshot;
        host.Services.Lifecycle.Publish(new(current.Phase, current.State, false, current.HostCount, current.HostGeneration));

        var diagnostics = Application.Lifecycle.GetDiagnostics();

        Assert.False(diagnostics.Snapshot.IsActive);
        Assert.Equal(2, diagnostics.OpenWindowCount);
        Assert.Equal(Application.OpenForms.Count(form => form.IsActive), diagnostics.ActiveWindowCount);
        second.Close();
        Assert.Equal(1, Application.Lifecycle.GetDiagnostics().OpenWindowCount);
        Assert.False(Application.Lifecycle.Snapshot.IsActive);
        first.Close();
        Assert.Equal(0, Application.Lifecycle.GetDiagnostics().OpenWindowCount);
    }

    [Fact]
    public void ExitAndHostRecreationDoNotReleaseBorrowedBindingsOrOnExitSubscriptions()
    {
        var baselineLifecycle = Application.Lifecycle;
        var baselineForms = Application.OpenForms;
        var baselineInputs = Application.InputBindings;
        var baselineCommands = Application.CommandBindings;
        var baselineInputCount = baselineInputs.Count;
        var baselineCommandCount = baselineCommands.Count;
        var command = new RoutedCommand("Borrowed");
        var input = new KeyBinding(new DelegateCommand(() => { }), new KeyGesture(Keys.F12));
        var binding = new CommandBinding(command, (_, _) => { });
        var borrowedExitCalls = 0;
        EventHandler borrowedExit = (_, _) => borrowedExitCalls++;
        baselineInputs.Add(input);
        baselineCommands.Add(binding);
        Application.OnExit += borrowedExit;
        try
        {
            ApplicationLifecycle expired;
            var hostExitCalls = 0;
            using (var host = ModernFormsTestHost.Create())
            {
                expired = Application.Lifecycle;
                Assert.NotSame(baselineForms, Application.OpenForms);
                Assert.Empty(Application.InputBindings);
                Assert.Empty(Application.CommandBindings);
                Application.OnExit += (_, _) => hostExitCalls++;
                Application.Exit();
                Assert.Equal(1, hostExitCalls);
                Assert.Equal(0, borrowedExitCalls);
            }

            Assert.Same(baselineLifecycle, Application.Lifecycle);
            Assert.Same(baselineForms, Application.OpenForms);
            Assert.Same(baselineInputs, Application.InputBindings);
            Assert.Same(baselineCommands, Application.CommandBindings);
            Assert.Equal(baselineInputCount + 1, baselineInputs.Count);
            Assert.Equal(baselineCommandCount + 1, baselineCommands.Count);
            Assert.Throws<ObjectDisposedException>(() => expired.GetDiagnostics());
            Assert.Throws<ObjectDisposedException>(() => { expired.ActivationReceived += (_, _) => { }; });
            using var next = ModernFormsTestHost.Create();
            Assert.Empty(Application.InputBindings);
            Assert.Empty(Application.CommandBindings);
            Assert.Empty(Application.Lifecycle.GetDiagnostics().RecentTransitions);
            Application.Exit();
            Assert.Equal(1, hostExitCalls);
            Assert.Equal(0, borrowedExitCalls);
        }
        finally
        {
            Application.OnExit -= borrowedExit;
            baselineInputs.Remove(input);
            baselineCommands.Remove(binding);
        }
    }

    [Fact]
    public void RuntimeScopeRestoresSynchronizationContextAfterActualApplicationRun()
    {
        var previous = SynchronizationContext.Current;
        using (var host = ModernFormsTestHost.Create())
        {
            var form = new Form();
            host.Dispatcher.Post(Application.Exit);
            Application.Run(form);
        }
        Assert.Same(previous, SynchronizationContext.Current);
    }

    [Fact]
    public void ControlledDispatcherUsesProductionNestedFramesAndCancellation()
    {
        using var host = ModernFormsTestHost.Create();
        var order = new List<string>();
        using var cancellation = new CancellationTokenSource();
        host.Dispatcher.Post(() =>
        {
            var nested = new DispatcherFrame();
            host.Dispatcher.Post(() => { order.Add("nested"); nested.Continue = false; });
            Dispatcher.UIThread.PushFrame(nested);
            order.Add("outer");
            cancellation.Cancel();
        });

        Dispatcher.UIThread.MainLoop(cancellation.Token);

        Assert.Equal(new[] { "nested", "outer" }, order);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void ControlledDispatcherRejectsQuiescentLoopWithoutWaitingOrAdvancingTime()
    {
        using var host = ModernFormsTestHost.Create();

        var failure = Assert.Throws<InvalidOperationException>(() => Dispatcher.UIThread.MainLoop(CancellationToken.None));

        Assert.Contains("quiescent before exit", failure.Message, StringComparison.Ordinal);
        Assert.Equal(TimeSpan.Zero, host.Clock.CurrentTime);
    }

    [Fact]
    public void BackgroundExitUsesTheRegisteredPlatformQueueWithoutDrainingWindowKit()
    {
        using var host = ModernFormsTestHost.Create();
        var platformDispatcher = new IndependentPlatformDispatcher();
        using var dispatcherScope = OverridePlatformDispatcher(platformDispatcher);
        var exitCalls = 0;
        Application.OnExit += (_, _) => exitCalls++;
        Exception? workerFailure = null;
        var worker = new Thread(() =>
        {
            try { Application.Exit(); }
            catch (Exception exception) { workerFailure = exception; }
        });
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)), "The background exit request must not wait for UI work.");

        Assert.Null(workerFailure);
        Assert.Equal(1, platformDispatcher.PendingCount);
        Assert.Equal(0, exitCalls);
        platformDispatcher.Drain();

        Assert.Equal(1, exitCalls);
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void ReentrantLifecycleExitUsesTheRegisteredPlatformQueueWithoutDrainingWindowKit()
    {
        using var host = ModernFormsTestHost.Create();
        var platformDispatcher = new IndependentPlatformDispatcher();
        using var dispatcherScope = OverridePlatformDispatcher(platformDispatcher);
        var order = new List<string>();
        Application.Lifecycle.LifecycleChanged += (_, args) =>
        {
            order.Add(args.Current.Phase.ToString());
            if (args.Current.Phase == PlatformApplicationPhase.Suspended)
            {
                Application.Exit();
                Assert.DoesNotContain("exit", order);
            }
        };
        Application.Lifecycle.StateSaving += (_, args) => order.Add("save:" + args.Reason);
        Application.OnExit += (_, _) => order.Add("exit");
        var current = host.Services.Lifecycle.Snapshot;
        host.Services.Lifecycle.Publish(new(PlatformApplicationPhase.Suspended,
            PlatformApplicationLifecycleState.Background, false, current.HostCount, current.HostGeneration));

        Assert.Equal(new[] { "Suspended" }, order);
        Assert.Equal(1, platformDispatcher.PendingCount);
        platformDispatcher.Drain();

        Assert.Equal(new[] { "Suspended", "Exiting", "save:Exit", "exit", "Exited" }, order);
        Assert.Empty(host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void BackgroundExitQueuedByAnExpiredScopeCannotExitTheNextRuntime()
    {
        var platformDispatcher = new IndependentPlatformDispatcher();
        using (var host = ModernFormsTestHost.Create())
        using (OverridePlatformDispatcher(platformDispatcher))
        {
            Exception? failure = null;
            var worker = new Thread(() =>
            {
                try { Application.Exit(); }
                catch (Exception exception) { failure = exception; }
            });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
            Assert.Null(failure);
            Assert.Equal(1, platformDispatcher.PendingCount);
        }

        using var next = ModernFormsTestHost.Create();
        var exitCalls = 0;
        Application.OnExit += (_, _) => exitCalls++;
        platformDispatcher.Drain();

        Assert.Equal(0, exitCalls);
        Assert.Equal(PlatformApplicationPhase.Running, Application.Lifecycle.Snapshot.Phase);
    }

    [Fact]
    public void ThrowingFacadeObserverDoesNotHideCommittedStateOrSkipOtherSubscribers()
    {
        using var host = ModernFormsTestHost.Create();
        var lifecycle = Application.Lifecycle;
        var observed = false;
        var failure = new LifecycleObserverFailure();
        lifecycle.LifecycleChanged += (_, _) => throw failure;
        lifecycle.LifecycleChanged += (_, args) =>
        {
            observed = true;
            Assert.Equal(PlatformApplicationLifecycleState.Background, args.Current.State);
        };

        Assert.Same(failure, Assert.Throws<LifecycleObserverFailure>(() =>
            host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background)));

        Assert.True(observed);
        Assert.Equal(PlatformApplicationLifecycleState.Background, lifecycle.Snapshot.State);
        Assert.Single(lifecycle.GetDiagnostics().RecentTransitions);
    }

    [Fact]
    public void ClosedObserverDisposingHostCannotExitRestoredBorrowedApplication()
    {
        var borrowedInputs = Application.InputBindings;
        var borrowedExitCalls = 0;
        EventHandler borrowedExit = (_, _) => borrowedExitCalls++;
        Application.OnExit += borrowedExit;
        try
        {
            using var host = ModernFormsTestHost.Create();
            using var form = new Form();
            form.Closed += (_, _) => host.Dispose();
            form.Shown += (_, _) => form.Close();

            Application.Run(form);

            Assert.Equal(0, borrowedExitCalls);
            Assert.Same(borrowedInputs, Application.InputBindings);
            using var next = ModernFormsTestHost.Create();
            Assert.Empty(Application.InputBindings);
        }
        finally { Application.OnExit -= borrowedExit; }
    }

    [Fact]
    public void ExitObserverDisposingHostStillReportsItsFailureAfterStateRestoration()
    {
        var borrowedInputs = Application.InputBindings;
        using var host = ModernFormsTestHost.Create();
        var expected = new LifecycleObserverFailure();
        Application.OnExit += (_, _) =>
        {
            host.Dispose();
            throw expected;
        };

        Assert.Same(expected, Assert.Throws<LifecycleObserverFailure>(Application.Exit));

        Assert.Same(borrowedInputs, Application.InputBindings);
        using var next = ModernFormsTestHost.Create();
        Assert.Empty(Application.InputBindings);
    }

    [Fact]
    public void FailedRunFinallyDoesNotOverwriteRuntimeRestoredInsideExitCallback()
    {
        var borrowedInputs = Application.InputBindings;
        var borrowedForms = Application.OpenForms;
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var expected = new LifecycleObserverFailure();
        form.Shown += (_, _) => throw expected;
        Application.OnExit += (_, _) => host.Dispose();

        Assert.Same(expected, Assert.Throws<LifecycleObserverFailure>(() => Application.Run(form)));

        Assert.Same(borrowedInputs, Application.InputBindings);
        Assert.Same(borrowedForms, Application.OpenForms);
        using var next = ModernFormsTestHost.Create();
        using var nextForm = new Form();
        nextForm.Shown += (_, _) => nextForm.Close();
        Application.Run(nextForm);
        Assert.Equal(PlatformApplicationPhase.Exited, Application.Lifecycle.Snapshot.Phase);
    }

    private static IDisposable OverridePlatformDispatcher(IPlatformDispatcher dispatcher)
    {
        // Use the existing revocable service seam without adding a public testing registry API.
        var push = typeof(WindowKit.Backend.PlatformServiceRegistry).GetMethod("PushServiceForTesting",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(push);
        return Assert.IsAssignableFrom<IDisposable>(push.MakeGenericMethod(typeof(IPlatformDispatcher))
            .Invoke(null, [dispatcher]));
    }

    private sealed class IndependentPlatformDispatcher : IPlatformDispatcher
    {
        private readonly int owner = Environment.CurrentManagedThreadId;
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> pending = new();
        internal int PendingCount => pending.Count;
        public bool CheckAccess() => Environment.CurrentManagedThreadId == owner;
        public void Post(Action action) => pending.Enqueue(action);
        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
            => InvokeAsync(() => { action(); return true; }, cancellationToken);
        public Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<T>(cancellationToken);
            if (!CheckAccess()) throw new InvalidOperationException("This test adapter supports owner-thread invocation only.");
            try { return Task.FromResult(function()); }
            catch (Exception exception) { return Task.FromException<T>(exception); }
        }
        internal void Drain()
        {
            Assert.True(CheckAccess());
            int remaining = 16;
            while (pending.TryDequeue(out var action))
            {
                Assert.True(remaining-- > 0, "Platform exit work must remain bounded.");
                action();
            }
        }
    }

    private sealed class LifecycleObserverFailure : Exception;
}
