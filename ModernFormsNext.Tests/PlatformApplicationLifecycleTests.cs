using System.Runtime.ExceptionServices;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class PlatformApplicationLifecycleTests
{
    [Fact]
    public void SnapshotCommitsBeforeLegacyThenRichCallbacksAndDuplicatesAreSuppressed()
    {
        using var publisher = CreatePublisher();
        List<string> order = [];
        publisher.StateChanged += (sender, args) =>
        {
            Assert.Same(publisher, sender);
            Assert.Equal(args.CurrentState, publisher.State);
            Assert.True(publisher.IsPublishing);
            order.Add("legacy");
        };
        publisher.LifecycleChanged += (sender, args) =>
        {
            Assert.Same(publisher, sender);
            Assert.Same(args.Current, publisher.Snapshot);
            Assert.Equal(1, args.Sequence);
            order.Add("rich");
        };

        publisher.Publish(Running());
        publisher.Publish(Running());

        Assert.Equal(new[] { "legacy", "rich" }, order);
        Assert.False(publisher.IsPublishing);
    }

    [Fact]
    public void WindowDeactivationDoesNotInventBackgroundOrLegacyAnimationPause()
    {
        using var publisher = CreatePublisher(Running());
        int legacy = 0;
        publisher.StateChanged += (_, _) => legacy++;
        PlatformApplicationLifecycleEventArgs? transition = null;
        publisher.LifecycleChanged += (_, args) => transition = args;

        publisher.Publish(Running(active: false));

        Assert.Equal(0, legacy);
        Assert.Equal(PlatformApplicationLifecycleState.Foreground, publisher.State);
        Assert.False(publisher.Snapshot.IsActive);
        Assert.NotNull(transition);
    }

    [Fact]
    public void ReentrantTransitionWaitsUntilTheCurrentLegacyAndRichPairCompletes()
    {
        using var publisher = CreatePublisher(Running());
        List<string> order = [];
        publisher.StateChanged += (_, args) =>
        {
            order.Add($"legacy:{args.CurrentState}");
            if (args.CurrentState == PlatformApplicationLifecycleState.Background)
            {
                publisher.Publish(Running());
                Assert.Equal(PlatformApplicationLifecycleState.Background, publisher.State);
            }
        };
        publisher.LifecycleChanged += (_, args) => order.Add($"rich:{args.Current.State}:{args.Sequence}");

        publisher.Publish(Background());

        Assert.Equal(new[] { "legacy:Background", "rich:Background:1", "legacy:Foreground", "rich:Foreground:2" }, order);
        Assert.Equal(PlatformApplicationLifecycleState.Foreground, publisher.State);
    }

    [Fact]
    public void ThrowingLegacySubscriberDoesNotSuppressOtherSubscribersOrQueuedTransition()
    {
        using var publisher = CreatePublisher(Running());
        var expected = new InvalidOperationException("observer");
        List<string> order = [];
        publisher.StateChanged += (_, args) =>
        {
            if (args.CurrentState == PlatformApplicationLifecycleState.Background)
            {
                publisher.Publish(Running());
                throw expected;
            }
        };
        publisher.StateChanged += (_, args) => order.Add($"legacy:{args.CurrentState}");
        publisher.LifecycleChanged += (_, args) => order.Add($"rich:{args.Current.State}");

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => publisher.Publish(Background())));

        Assert.Equal(new[] { "legacy:Background", "rich:Background", "legacy:Foreground", "rich:Foreground" }, order);
        Assert.Equal(PlatformApplicationLifecycleState.Foreground, publisher.State);
    }

    [Fact]
    public void MultipleObserverFailuresAggregateOnlyAfterMandatoryDelivery()
    {
        using var publisher = CreatePublisher();
        bool delivered = false;
        publisher.StateChanged += (_, _) => throw new InvalidOperationException("legacy");
        publisher.LifecycleChanged += (_, _) => throw new ArgumentException("rich");
        publisher.LifecycleChanged += (_, _) => delivered = true;

        var failure = Assert.Throws<AggregateException>(() => publisher.Publish(Running()));

        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.True(delivered);
        Assert.Equal(Running(), publisher.Snapshot);
    }

    [Fact]
    public void OldGenerationAndPostExitNativeReportsCannotResurrectTheApplication()
    {
        using var publisher = CreatePublisher(Running(generation: 4));
        int transitions = 0;
        int activations = 0;
        int restores = 0;
        publisher.LifecycleChanged += (_, _) => transitions++;
        publisher.ActivationReceived += (_, _) => activations++;
        publisher.StateRestoring += (_, _) => restores++;
        publisher.Publish(Background(generation: 3));
        Assert.Equal(Running(generation: 4), publisher.Snapshot);

        publisher.Publish(new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Exited, PlatformApplicationLifecycleState.NoHost, hostGeneration: 4));
        publisher.Publish(Running(generation: 5));
        publisher.Activate(new PlatformApplicationActivation(PlatformActivationKind.Launch));
        publisher.RestoreState(new PlatformApplicationStateData(1), PlatformApplicationStateReason.Recreation);

        Assert.Equal(PlatformApplicationPhase.Exited, publisher.Snapshot.Phase);
        Assert.Equal(1, transitions);
        Assert.Equal(0, activations);
        Assert.Equal(0, restores);
        Assert.Null(publisher.RequestSaveState(PlatformApplicationStateReason.Exit));
    }

    [Fact]
    public void ExitingAllowsStateSaveThenExitedButRejectsNewInteraction()
    {
        using var publisher = CreatePublisher(Running());
        var data = new PlatformApplicationStateData(1, new Dictionary<string, string> { ["page"] = "settings" });
        publisher.StateSaving += (_, args) => args.Data = data;
        publisher.Publish(new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Exiting, PlatformApplicationLifecycleState.Foreground,
            hostCount: 1, hostGeneration: 1));

        publisher.Publish(Running(generation: 2));
        publisher.Activate(new PlatformApplicationActivation(PlatformActivationKind.Launch));

        Assert.Equal(PlatformApplicationPhase.Exiting, publisher.Snapshot.Phase);
        Assert.Null(publisher.LastActivation);
        Assert.Same(data, publisher.RequestSaveState(PlatformApplicationStateReason.Exit));
        publisher.Publish(new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Exited, PlatformApplicationLifecycleState.NoHost, hostGeneration: 1));
        Assert.Equal(PlatformApplicationPhase.Exited, publisher.Snapshot.Phase);
    }

    [Fact]
    public void RestoreAndActivationRemainExplicitOrderedRequestsOnTheUiThread()
    {
        using var publisher = CreatePublisher();
        int ownerThread = Environment.CurrentManagedThreadId;
        List<string> order = [];
        publisher.LifecycleChanged += (_, args) => order.Add($"state:{args.Current.Phase}:{args.Sequence}");
        publisher.StateRestoring += (_, args) =>
        {
            Assert.Equal(ownerThread, Environment.CurrentManagedThreadId);
            Assert.Equal("42", args.Data.Values["document"]);
            order.Add($"restore:{args.Sequence}");
        };
        publisher.ActivationReceived += (_, args) => order.Add($"activation:{args.Sequence}");
        publisher.Publish(new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Starting, PlatformApplicationLifecycleState.Unknown));
        publisher.RestoreState(new PlatformApplicationStateData(2,
            new Dictionary<string, string> { ["document"] = "42" }), PlatformApplicationStateReason.Recreation);
        var activation = new PlatformApplicationActivation(PlatformActivationKind.Uri, uri: new Uri("sample://document/42"));
        publisher.Activate(activation);
        publisher.Publish(Running());
        publisher.Activate(activation);

        Assert.Equal(new[] { "state:Starting:1", "restore:2", "activation:3", "state:Running:4", "activation:5" }, order);
        Assert.Same(activation, publisher.LastActivation);
    }

    [Fact]
    public void StateSaveIsSealedAfterCallbacksAndReentrantSaveDoesNotSuppressRichEvent()
    {
        using var publisher = CreatePublisher();
        PlatformApplicationStateSavingEventArgs? retained = null;
        publisher.StateSaving += (_, args) => { retained = args; args.Data = new PlatformApplicationStateData(1); };
        Assert.NotNull(publisher.RequestSaveState(PlatformApplicationStateReason.Suspend));
        Assert.NotNull(retained);
        Assert.Throws<InvalidOperationException>(() => retained.Data = new PlatformApplicationStateData(2));
        bool richDelivered = false;
        publisher.StateChanged += (_, _) => publisher.RequestSaveState(PlatformApplicationStateReason.Suspend);
        publisher.LifecycleChanged += (_, _) => richDelivered = true;

        Assert.Throws<InvalidOperationException>(() => publisher.Publish(Running()));
        Assert.True(richDelivered);
        Assert.Equal(Running(), publisher.Snapshot);
    }

    [Fact]
    public void SelfReplenishingNotificationsAreBoundedAndLeaveThePublisherRecoverable()
    {
        using var publisher = CreatePublisher(Running());
        int calls = 0;
        EventHandler<PlatformApplicationLifecycleEventArgs> repeat = (_, args) =>
        {
            calls++;
            publisher.Publish(args.Current.State == PlatformApplicationLifecycleState.Foreground ? Background() : Running());
        };
        publisher.LifecycleChanged += repeat;

        Assert.Throws<InvalidOperationException>(() => publisher.Publish(Background()));

        Assert.Equal(PlatformApplicationLifecyclePublisher.MaximumNotificationsPerDrain, calls);
        Assert.False(publisher.IsPublishing);
        publisher.LifecycleChanged -= repeat;
        publisher.Publish(Background());
        Assert.Equal(PlatformApplicationLifecycleState.Background, publisher.State);
    }

    [Fact]
    public void ForeignThreadMutationsFailBeforeChangingStateOrDeliveringCallbacks()
    {
        using var publisher = CreatePublisher(Running());
        int calls = 0;
        publisher.LifecycleChanged += (_, _) => calls++;
        Exception? failure = null;
        var thread = new Thread(() => failure = Record.Exception(() => publisher.Publish(Background())));
        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(Running(), publisher.Snapshot);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void DisposeDuringNotificationDropsRemainingCallbacksAndQueuedPayloads()
    {
        var publisher = CreatePublisher();
        bool laterCalled = false;
        publisher.StateChanged += (_, _) =>
        {
            publisher.Activate(new PlatformApplicationActivation(PlatformActivationKind.Launch));
            publisher.Dispose();
        };
        publisher.LifecycleChanged += (_, _) => laterCalled = true;
        publisher.ActivationReceived += (_, _) => laterCalled = true;

        publisher.Publish(Running());

        Assert.False(laterCalled);
        Assert.Throws<ObjectDisposedException>(() => publisher.Activate(new PlatformApplicationActivation(PlatformActivationKind.Launch)));
        publisher.Dispose();
    }

    [Fact]
    public void ActivationCopiesInputsPreservesExplicitKindsAndDoesNotExposeMutableCollections()
    {
        string[] args = ["", "sample://looks-like-uri", "--password=private"];
        string[] files = ["content://documents/42"];
        var activation = new PlatformApplicationActivation(PlatformActivationKind.Arguments, args, files);
        args[1] = "changed";
        files[0] = "changed";

        Assert.Equal(PlatformActivationKind.Arguments, activation.Kind);
        Assert.Null(activation.Uri);
        Assert.Equal("sample://looks-like-uri", activation.Arguments[1]);
        Assert.Equal("content://documents/42", Assert.Single(activation.Files));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)activation.Arguments)[0] = "changed");
        Assert.DoesNotContain("private", activation.ToString());
    }

    [Fact]
    public void ActivationRejectsOversizedOrMissingPayloadsWithoutUnboundedEnumeration()
    {
        Assert.Throws<ArgumentException>(() => new PlatformApplicationActivation(PlatformActivationKind.Arguments));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationActivation(PlatformActivationKind.Files));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationActivation(PlatformActivationKind.Uri));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationActivation(PlatformActivationKind.Uri, uri: new Uri("relative", UriKind.Relative)));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationActivation(PlatformActivationKind.Arguments,
            Enumerable.Repeat("x", int.MaxValue)));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationActivation(PlatformActivationKind.Arguments,
            [new string('x', PlatformApplicationActivation.MaximumItemLength + 1)]));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationActivation(PlatformActivationKind.Arguments,
            Enumerable.Repeat(new string('x', 4096), 17)));
    }

    [Fact]
    public void StateDataCopiesVersionedPrimitiveValuesAndEnforcesDocumentedBounds()
    {
        var values = new Dictionary<string, string> { ["count"] = "3", ["selection"] = "42" };
        var data = new PlatformApplicationStateData(2, values);
        values["count"] = "9";
        Assert.Equal(2, data.Version);
        Assert.Equal("3", data.Values["count"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)data.Values)["count"] = "9");
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformApplicationStateData(0));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationStateData(1,
            new Dictionary<string, string> { ["x"] = new string('x', 4097) }));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationStateData(1,
            Enumerable.Range(0, 65).ToDictionary(i => i.ToString(), _ => "x")));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationStateData(1,
            Enumerable.Range(0, 16).ToDictionary(i => i.ToString(), _ => new string('x', 4096))));
    }

    [Fact]
    public void SnapshotRejectsInvalidActiveHostAndTerminalCombinations()
    {
        Assert.Throws<ArgumentException>(() => new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Running, PlatformApplicationLifecycleState.Background, true, 1));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Running, PlatformApplicationLifecycleState.NoHost, hostCount: 1));
        Assert.Throws<ArgumentException>(() => new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Exited, PlatformApplicationLifecycleState.Foreground));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Running, PlatformApplicationLifecycleState.Unknown, hostGeneration: -1));
    }

    private static PlatformApplicationLifecyclePublisher CreatePublisher(PlatformApplicationLifecycleSnapshot? initial = null)
    {
        int ownerThread = Environment.CurrentManagedThreadId;
        return new PlatformApplicationLifecyclePublisher(() =>
        {
            if (Environment.CurrentManagedThreadId != ownerThread)
                throw new InvalidOperationException("Not the owning UI thread.");
        }, initial);
    }
    private static PlatformApplicationLifecycleSnapshot Running(bool active = true, long generation = 1)
        => new(PlatformApplicationPhase.Running, PlatformApplicationLifecycleState.Foreground, active, 1, generation);
    private static PlatformApplicationLifecycleSnapshot Background(long generation = 1)
        => new(PlatformApplicationPhase.Running, PlatformApplicationLifecycleState.Background, false, 1, generation);
}
