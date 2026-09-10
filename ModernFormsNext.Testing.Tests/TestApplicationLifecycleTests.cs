using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class TestApplicationLifecycleTests
{
    [Fact]
    public void ExistingRegisteredFakeProvidesBothContractsWithTheSameSenderAndSnapshot()
    {
        using var host = ModernFormsTestHost.Create();
        var legacy = PlatformServiceRegistry.GetRequiredService<IPlatformApplicationLifecycle>();
        var rich = Assert.IsAssignableFrom<IPlatformApplicationLifecycleController>(legacy);
        Assert.Same(host.Services.Lifecycle, rich);
        List<string> order = [];
        legacy.StateChanged += (sender, _) => { Assert.Same(rich, sender); order.Add("legacy"); };
        rich.LifecycleChanged += (sender, args) =>
        {
            Assert.Same(rich, sender);
            Assert.Same(args.Current, rich.Snapshot);
            order.Add("rich");
        };

        host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background);

        Assert.Equal(new[] { "legacy", "rich" }, order);
    }

    [Fact]
    public void RichBackgroundAndRecreationPauseTheRealSchedulerWithoutTerminatingTheApplication()
    {
        using var host = ModernFormsTestHost.Create();
        List<float> values = [];
        AnimationHandle animation = AnimationScheduler.Default.Start(new object(), "lifecycle", values.Add,
            new AnimationOptions { Duration = TimeSpan.FromSeconds(1), Easing = Easings.Linear });
        host.Clock.Advance(TimeSpan.FromMilliseconds(250));
        host.Services.Lifecycle.Publish(new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Suspended, PlatformApplicationLifecycleState.NoHost, hostGeneration: 1));
        host.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(.25f, Assert.Single(values));
        bool restored = false;
        host.Services.Lifecycle.StateRestoring += (_, _) => restored = true;
        host.Services.Lifecycle.RestoreState(new PlatformApplicationStateData(1), PlatformApplicationStateReason.Recreation);
        host.Services.Lifecycle.Publish(new PlatformApplicationLifecycleSnapshot(
            PlatformApplicationPhase.Running, PlatformApplicationLifecycleState.Foreground, true, 1, 2));
        host.Clock.Advance(TimeSpan.FromMilliseconds(250));

        Assert.True(restored);
        Assert.Equal(new[] { .25f, .5f }, values);
        Assert.Equal(AnimationState.Running, animation.State);
        Assert.Equal(PlatformApplicationPhase.Running, host.Services.Lifecycle.Snapshot.Phase);
    }

    [Fact]
    public void RichActivationAndStateHandoffsAreIsolatedAcrossHosts()
    {
        PlatformApplicationStateSavingEventArgs? retained = null;
        TestApplicationLifecycle previous;
        using (var host = ModernFormsTestHost.Create())
        {
            previous = host.Services.Lifecycle;
            previous.Activate(new PlatformApplicationActivation(PlatformActivationKind.Files,
                files: ["content://document/42"]));
            previous.StateSaving += (_, args) =>
            {
                retained = args;
                args.Data = new PlatformApplicationStateData(1, new Dictionary<string, string> { ["document"] = "42" });
            };
            Assert.Equal("42", previous.RequestSaveState(PlatformApplicationStateReason.Recreation)!.Values["document"]);
        }

        Assert.Throws<ObjectDisposedException>(() => previous.Activate(new PlatformApplicationActivation(PlatformActivationKind.Launch)));
        Assert.NotNull(retained);
        Assert.Throws<InvalidOperationException>(() => retained.Data = new PlatformApplicationStateData(2));
        using var next = ModernFormsTestHost.Create();
        Assert.Null(next.Services.Lifecycle.LastActivation);
        Assert.Null(next.Services.Lifecycle.RequestSaveState(PlatformApplicationStateReason.Recreation));
        Assert.Equal(1, next.Services.Lifecycle.Snapshot.HostGeneration);
    }

    [Fact]
    public void LegacyObserverFailureStillReachesAnimationPauseAndRichSubscribers()
    {
        using var host = ModernFormsTestHost.Create();
        bool rich = false;
        host.Services.Lifecycle.StateChanged += (_, _) => throw new InvalidOperationException("observer");
        host.Services.Lifecycle.LifecycleChanged += (_, _) => rich = true;
        AnimationHandle animation = AnimationScheduler.Default.Start(new object(), "pause", _ => { },
            new AnimationOptions { Duration = TimeSpan.FromSeconds(1) });

        Assert.Throws<InvalidOperationException>(() => host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background));

        Assert.True(rich);
        Assert.Equal(AnimationState.Paused, animation.State);
        Assert.Equal(PlatformApplicationLifecycleState.Background, host.Services.Lifecycle.State);
    }
}
