using System.Drawing;
using System.Numerics;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class ThemeManagerApplyAndTransitionTests
{
    [Fact]
    public void ImmediateApplyCommitsAtomicallyAndRaisesDocumentedEventOrder()
    {
        using var harness = new ThemeManagerTestHarness();
        var order = new List<string>();
        object backgroundKey = ThemeTokens.Colors.Background.ResourceKey;
        harness.Manager.ThemeChanging += (_, _) => order.Add("changing");
        harness.Resources.ResourceChanged += (_, args) =>
        {
            if (Equals(args.Key, backgroundKey))
                order.Add("resources");
        };
        harness.Manager.ThemeChanged += (_, _) => order.Add("changed");

        ThemeApplyResult result = harness.Manager.Apply(Theme("apply.immediate", Color.Red), Immediate());

        Assert.True(result.Success);
        Assert.Null(result.Transition);
        Assert.Equal(["changing", "resources", "changed"], order);
        Assert.Equal("apply.immediate", harness.Manager.ActiveTheme!.Id);
        Assert.Equal(Color.Red.ToArgb(), ((Color)harness.Resources[backgroundKey]!).ToArgb());
        Assert.Equal(1, harness.Manager.GetDiagnostics().SuccessfulSwitches);
    }

    [Fact]
    public void ThemeChangingCanCancelBeforeAnyStateMutation()
    {
        using var harness = new ThemeManagerTestHarness();
        Assert.True(harness.Manager.Apply(Theme("cancel.before", Color.Red), Immediate()).Success);
        harness.Manager.ThemeChanging += (_, args) => args.Cancel = true;

        ThemeApplyResult result = harness.Manager.Apply(Theme("cancel.after", Color.Blue), Immediate());

        Assert.Equal(ThemeApplyStatus.Canceled, result.Status);
        Assert.Equal("cancel.before", harness.Manager.ActiveSnapshot!.Id);
        Assert.Equal(Color.Red.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
    }

    [Fact]
    public void ObserverFailureRollsBackSnapshotResourcesAndLegacyProjection()
    {
        using var harness = new ThemeManagerTestHarness();
        Assert.True(harness.Manager.Apply(Theme("rollback.old", Color.Red), Immediate()).Success);
        Dictionary<string, object> oldLegacy = harness.LegacyStore.GetSnapshot();
        bool failedEvent = false;
        harness.Manager.ThemeApplyFailed += (_, _) => failedEvent = true;
        harness.Resources.ResourceChanged += (_, args) =>
        {
            if (args.NewValue is Color color && color.ToArgb() == Color.Blue.ToArgb())
                throw new InvalidOperationException("Rejected by test observer.");
        };

        ThemeApplyResult result = harness.Manager.Apply(Theme("rollback.new", Color.Blue), Immediate());

        Assert.Equal(ThemeApplyStatus.Failed, result.Status);
        Assert.Equal("rollback.old", harness.Manager.ActiveSnapshot!.Id);
        Assert.Equal(Color.Red.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
        Assert.Equal(oldLegacy, harness.LegacyStore.GetSnapshot());
        Assert.True(failedEvent);
        Assert.NotNull(harness.Manager.GetDiagnostics().LastFailure);
    }

    [Fact]
    public void LegacyProjectionFailureRollsBackAlreadyReplacedResourceSnapshot()
    {
        using var harness = new ThemeManagerTestHarness();
        Assert.True(harness.Manager.Apply(Theme("rollback.legacy.old", Color.Red), Immediate()).Success);
        Dictionary<string, object> oldLegacy = harness.LegacyStore.GetSnapshot();
        harness.LegacyStore.NextReplaceException = new InvalidOperationException("Injected legacy failure.");

        ThemeApplyResult result = harness.Manager.Apply(Theme("rollback.legacy.new", Color.Blue), Immediate());

        Assert.Equal(ThemeApplyStatus.Failed, result.Status);
        Assert.Equal("rollback.legacy.old", harness.Manager.ActiveSnapshot!.Id);
        Assert.Equal(Color.Red.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
        Assert.Equal(oldLegacy, harness.LegacyStore.GetSnapshot());
    }

    [Fact]
    public void EveryDeferredResourceNotificationSeesTheCompleteNewSnapshot()
    {
        using var harness = new ThemeManagerTestHarness();
        var theme = Theme("atomic.snapshot", Color.Red);
        theme.Colors[ThemeTokens.Colors.Primary.Name] = Color.Blue;
        bool observedCompleteSnapshot = false;
        harness.Resources.ResourceChanged += (_, _) =>
        {
            observedCompleteSnapshot = harness.Resources.ContainsKey(ThemeTokens.Colors.Background.ResourceKey) &&
                harness.Resources.ContainsKey(ThemeTokens.Colors.Primary.ResourceKey);
        };

        ThemeApplyResult result = harness.Manager.Apply(theme, Immediate());

        Assert.True(result.Success);
        Assert.True(observedCompleteSnapshot);
    }

    [Fact]
    public async Task BackgroundRequestCommitsAndRaisesEventsOnDispatcherThread()
    {
        var dispatcher = new QueuedThemeDispatcher();
        using var harness = new ThemeManagerTestHarness(dispatcher: dispatcher);
        int changingThread = 0;
        int changedThread = 0;
        harness.Manager.ThemeChanging += (_, _) => changingThread = Environment.CurrentManagedThreadId;
        harness.Manager.ThemeChanged += (_, _) => changedThread = Environment.CurrentManagedThreadId;

        Task<ThemeApplyResult> pending = Task.Run(
            () => harness.Manager.ApplyAsync(Theme("background.apply", Color.Red), Immediate()));
        await dispatcher.Enqueued;
        int dispatcherThread = Environment.CurrentManagedThreadId;
        dispatcher.Drain();
        ThemeApplyResult result = await pending;

        Assert.True(result.Success);
        Assert.Equal(dispatcherThread, changingThread);
        Assert.Equal(dispatcherThread, changedThread);
        Assert.Equal(1, dispatcher.TotalInvocations);
    }

    [Fact]
    public async Task CancellationBeforeQueuedCommitLeavesStateUntouched()
    {
        var dispatcher = new QueuedThemeDispatcher();
        using var harness = new ThemeManagerTestHarness(dispatcher: dispatcher);
        using var cancellation = new CancellationTokenSource();
        Task<ThemeApplyResult> pending = Task.Run(
            () => harness.Manager.ApplyAsync(Theme("queued.cancel", Color.Red), Immediate(), cancellation.Token));
        await dispatcher.Enqueued;

        cancellation.Cancel();
        dispatcher.Drain();
        ThemeApplyResult result = await pending;

        Assert.Equal(ThemeApplyStatus.Canceled, result.Status);
        Assert.Null(harness.Manager.ActiveSnapshot);
        Assert.Empty(harness.Resources);
    }

    [Fact]
    public async Task ColorCustomNumberAndSolidBrushAnimateWithOneSharedSchedulerEntry()
    {
        using var harness = new ThemeManagerTestHarness();
        ThemeDefinition start = Theme("animation.start", Color.Black);
        start.Resources["Progress"] = ThemeResourceValue.FromNumber(0d);
        start.Brushes["Card"] = new SolidColorBrush(Color.Red) { Opacity = 0.2f };
        ThemeDefinition target = Theme("animation.target", Color.White);
        target.Resources["Progress"] = ThemeResourceValue.FromNumber(10d);
        target.Brushes["Card"] = new SolidColorBrush(Color.Blue) { Opacity = 0.8f };
        Assert.True(harness.Manager.Apply(start, Immediate()).Success);

        ThemeApplyResult result = harness.Manager.Apply(target, Animated());
        Assert.NotNull(result.Transition);
        Assert.Equal(1, harness.SchedulerHarness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        var working = Assert.IsType<SolidColorBrush>(harness.Resources[BrushKey("Card")]);
        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Color middle = ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey);
        Assert.InRange(middle.R, 127, 128);
        Assert.Equal(5d, (double)harness.Resources[ResourceKey("Progress")]!, 6);
        Assert.Same(working, harness.Resources[BrushKey("Card")]);
        Assert.NotEqual(Color.Red.ToArgb(), working.PaintColor.ToArgb());
        Assert.NotEqual(Color.Blue.ToArgb(), working.PaintColor.ToArgb());
        Assert.Equal(0.5f, working.Opacity, 3);

        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(ThemeTransitionStatus.Completed, await result.Transition!.Completion);
        Assert.Equal(Color.White.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
        Assert.Equal(0, harness.SchedulerHarness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.SchedulerHarness.TickSource.IsRunning);
    }

    [Theory]
    [InlineData("linear")]
    [InlineData("radial")]
    [InlineData("sweep")]
    public async Task CompatibleGradientsAnimateStopsGeometryOpacityAndTransform(string kind)
    {
        using var harness = new ThemeManagerTestHarness();
        ThemeDefinition start = Theme("gradient.start." + kind, Color.Black);
        ThemeDefinition target = Theme("gradient.target." + kind, Color.Black);
        start.Brushes["Gradient"] = Gradient(kind, Color.Red, Color.Blue, 0f, 1f, Matrix3x2.Identity, 0.2f);
        target.Brushes["Gradient"] = Gradient(kind, Color.Blue, Color.Green, 0.2f, 0.8f, Matrix3x2.CreateTranslation(10f, 20f), 0.8f);
        ConfigureTargetGeometry(target.Brushes["Gradient"]);
        harness.Manager.Apply(start, Immediate());

        ThemeApplyResult result = harness.Manager.Apply(target, Animated());
        var working = Assert.IsAssignableFrom<GradientBrush>(harness.Resources[BrushKey("Gradient")]);
        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Same(working, harness.Resources[BrushKey("Gradient")]);
        Assert.Equal(0.5f, working.Opacity, 3);
        Assert.Equal(5f, working.Transform.M31, 3);
        Assert.Equal(10f, working.Transform.M32, 3);
        Assert.Equal(0.1f, working.GradientStops[0].Offset, 3);
        Assert.NotEqual(Color.Red.ToArgb(), working.GradientStops[0].PaintColor.ToArgb());
        Assert.NotEqual(Color.Blue.ToArgb(), working.GradientStops[0].PaintColor.ToArgb());
        AssertGeometryAtHalf(working);

        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(ThemeTransitionStatus.Completed, await result.Transition!.Completion);
    }

    [Fact]
    public void LayoutTokensAndIncompatibleBrushesSwitchImmediately()
    {
        using var harness = new ThemeManagerTestHarness();
        ThemeDefinition start = Theme("incompatible.start", Color.Black);
        start.Spacing["LayoutGap"] = 4d;
        start.Brushes["Card"] = new SolidColorBrush(Color.Red);
        ThemeDefinition target = Theme("incompatible.target", Color.Black);
        target.Spacing["LayoutGap"] = 20d;
        target.Brushes["Card"] = Gradient("linear", Color.Blue, Color.Green, 0f, 1f, Matrix3x2.Identity, 1f);
        harness.Manager.Apply(start, Immediate());

        ThemeApplyResult result = harness.Manager.Apply(target, Animated());

        Assert.Null(result.Transition);
        Assert.Equal(20d, (double)harness.Resources[SpacingKey("LayoutGap")]!);
        Assert.IsType<LinearGradientBrush>(harness.Resources[BrushKey("Card")]);
    }

    [Fact]
    public void DifferentGradientStopCountsSwitchThatBrushImmediately()
    {
        using var harness = new ThemeManagerTestHarness();
        ThemeDefinition start = Theme("stops.start", Color.Black);
        ThemeDefinition target = Theme("stops.target", Color.Black);
        var two = Gradient("linear", Color.Red, Color.Blue, 0f, 1f, Matrix3x2.Identity, 1f);
        var three = Gradient("linear", Color.Blue, Color.Green, 0f, 1f, Matrix3x2.Identity, 1f);
        three.GradientStops.Add(new GradientStop(Color.White, 1f));
        start.Brushes["Card"] = two;
        target.Brushes["Card"] = three;
        harness.Manager.Apply(start, Immediate());

        ThemeApplyResult result = harness.Manager.Apply(target, Animated());

        Assert.Null(result.Transition);
        Assert.Equal(3, Assert.IsType<LinearGradientBrush>(harness.Resources[BrushKey("Card")]).GradientStops.Count);
    }

    [Fact]
    public async Task RapidSwitchReplacesPriorTransitionAndOldHandleCannotCancelNewOne()
    {
        using var harness = new ThemeManagerTestHarness();
        harness.Manager.Apply(Theme("rapid.red", Color.Red), Immediate());
        ThemeApplyResult blue = harness.Manager.Apply(Theme("rapid.blue", Color.Blue), Animated());
        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));

        ThemeApplyResult green = harness.Manager.Apply(Theme("rapid.green", Color.Green), Animated());
        Assert.Equal(ThemeTransitionStatus.Canceled, await blue.Transition!.Completion);
        blue.Transition.Cancel();

        Assert.Equal(ThemeTransitionStatus.Running, green.Transition!.State);
        Assert.Equal(1, harness.SchedulerHarness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(ThemeTransitionStatus.Completed, await green.Transition.Completion);
        Assert.Equal(Color.Green.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
    }

    [Fact]
    public async Task IgnoreNewReplacementModeKeepsExistingThemeTransition()
    {
        using var harness = new ThemeManagerTestHarness();
        harness.Manager.Apply(Theme("ignore.start", Color.Red), Immediate());
        ThemeApplyResult existing = harness.Manager.Apply(Theme("ignore.blue", Color.Blue), Animated());
        ThemeApplyOptions ignoredOptions = Animated();
        ignoredOptions.Transition.ReplacementMode = AnimationReplacementMode.IgnoreNew;

        ThemeApplyResult ignored = harness.Manager.Apply(Theme("ignore.green", Color.Green), ignoredOptions);

        Assert.Equal(ThemeApplyStatus.Canceled, ignored.Status);
        Assert.Equal("ignore.blue", harness.Manager.ActiveSnapshot!.Id);
        Assert.Equal(ThemeTransitionStatus.Running, existing.Transition!.State);
        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(ThemeTransitionStatus.Completed, await existing.Transition.Completion);
        Assert.Equal(Color.Blue.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
    }

    [Fact]
    public async Task ExplicitCancellationSnapsToTargetAndLeavesNoOrphanedWork()
    {
        using var harness = new ThemeManagerTestHarness();
        harness.Manager.Apply(Theme("cancel.red", Color.Red), Immediate());
        ThemeApplyResult target = harness.Manager.Apply(Theme("cancel.blue", Color.Blue), Animated());
        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));

        target.Transition!.Cancel();

        Assert.Equal(ThemeTransitionStatus.Canceled, await target.Transition.Completion);
        Assert.Equal(Color.Blue.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
        Assert.Equal(0, harness.SchedulerHarness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.SchedulerHarness.TickSource.IsRunning);
    }

    [Fact]
    public void ReducedMotionAndDesignerModeCommitWithoutStartingTransition()
    {
        using var reduced = new ThemeManagerTestHarness(reducedMotion: true);
        reduced.Manager.Apply(Theme("reduced.start", Color.Red), Immediate());
        ThemeApplyResult reducedResult = reduced.Manager.Apply(Theme("reduced.target", Color.Blue), Animated());

        using var designer = new ThemeManagerTestHarness(designMode: true);
        designer.Manager.Apply(Theme("designer.start", Color.Red), Immediate());
        ThemeApplyResult designerResult = designer.Manager.Apply(Theme("designer.target", Color.Blue), Animated());

        Assert.Null(reducedResult.Transition);
        Assert.Null(designerResult.Transition);
        Assert.False(reduced.SchedulerHarness.TickSource.IsRunning);
        Assert.False(designer.SchedulerHarness.TickSource.IsRunning);
    }

    [Fact]
    public async Task SchedulerAnimationPolicyCanCompleteActiveTransitionImmediately()
    {
        using var harness = new ThemeManagerTestHarness();
        harness.Manager.Apply(Theme("policy.start", Color.Red), Immediate());
        var order = new List<string>();
        harness.Manager.ThemeChanged += (_, _) => order.Add("changed");
        harness.Manager.ThemeTransitionCompleted += (_, _) => order.Add("completed");
        ThemeApplyResult result = harness.Manager.Apply(Theme("policy.target", Color.Blue), Animated());

        harness.SchedulerHarness.Policy.AnimationsEnabled = false;

        Assert.Equal(ThemeTransitionStatus.Completed, await result.Transition!.Completion);
        Assert.Equal(Color.Blue.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
        Assert.Equal(0, harness.SchedulerHarness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(["changed", "completed"], order);
    }

    [Fact]
    public async Task TransitionObserverFaultLeavesCommittedTargetAndNoSchedulerWork()
    {
        using var harness = new ThemeManagerTestHarness();
        harness.Manager.Apply(Theme("fault.start", Color.Red), Immediate());
        harness.Resources.ResourceChanged += (_, args) =>
        {
            if (args.NewValue is Color color && color.ToArgb() != Color.Red.ToArgb())
                throw new InvalidOperationException("Transition observer failure.");
        };
        ThemeApplyResult result = harness.Manager.Apply(Theme("fault.target", Color.Blue), Animated());

        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(ThemeTransitionStatus.Failed, await result.Transition!.Completion);
        Assert.Equal("fault.target", harness.Manager.ActiveSnapshot!.Id);
        Assert.Equal(Color.Blue.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
        Assert.Equal(0, harness.SchedulerHarness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.SchedulerHarness.TickSource.IsRunning);
    }

    [Fact]
    public async Task PlatformLifecyclePauseResumeHasNoTimeJump()
    {
        var lifecycle = new TestPlatformApplicationLifecycle(PlatformApplicationLifecycleState.Foreground);
        using var harness = new ThemeManagerTestHarness(lifecycle: lifecycle);
        harness.Manager.Apply(Theme("lifecycle.start", Color.Black), Immediate());
        ThemeApplyResult result = harness.Manager.Apply(Theme("lifecycle.target", Color.White), Animated());
        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(40));
        Color beforePause = ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey);

        lifecycle.SetState(PlatformApplicationLifecycleState.Background);
        harness.SchedulerHarness.Clock.Advance(TimeSpan.FromSeconds(30));
        lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);
        harness.SchedulerHarness.TickSource.Fire();

        Assert.Equal(beforePause.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(60));
        Assert.Equal(ThemeTransitionStatus.Completed, await result.Transition!.Completion);
        Assert.Equal(Color.White.ToArgb(), ResourceColor(harness, ThemeTokens.Colors.Background.ResourceKey).ToArgb());
    }

    [Fact]
    public async Task NoActiveHostDoesNotCrashAndResumesWhenForegroundAppears()
    {
        var lifecycle = new TestPlatformApplicationLifecycle(PlatformApplicationLifecycleState.NoHost);
        using var harness = new ThemeManagerTestHarness(lifecycle: lifecycle);
        harness.Manager.Apply(Theme("nohost.start", Color.Black), Immediate());

        ThemeApplyResult result = harness.Manager.Apply(Theme("nohost.target", Color.White), Animated());

        Assert.NotNull(result.Transition);
        Assert.False(harness.SchedulerHarness.TickSource.IsRunning);
        lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);
        harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(ThemeTransitionStatus.Completed, await result.Transition!.Completion);
    }

    [Fact]
    public void DiagnosticsTrackActiveThemeValidationAndCounters()
    {
        using var harness = new ThemeManagerTestHarness();
        harness.Manager.Apply(Theme("diagnostics.valid", Color.Red), Immediate());
        var invalid = Theme("diagnostics.invalid", Color.Blue);
        invalid.BaseTheme = "missing.base";
        harness.Manager.Apply(invalid, Immediate());

        ThemeManagerDiagnostics diagnostics = harness.Manager.GetDiagnostics();

        Assert.Equal("diagnostics.valid", diagnostics.ActiveThemeId);
        Assert.Equal(1, diagnostics.SuccessfulSwitches);
        Assert.Equal(1, diagnostics.FailedSwitches);
        Assert.Contains(diagnostics.ValidationDiagnostics, item => item.Code == "THEME_BASE_MISSING");
        Assert.NotNull(diagnostics.LastFailure);
    }

    [Fact]
    public void UnsupportedMutableValueFailsSafelyAndStillRaisesFailureEvent()
    {
        using var harness = new ThemeManagerTestHarness();
        var theme = Theme("apply.unsupported", Color.Red);
        theme.Brushes["Unsupported"] = new UnsupportedBrush();
        ThemeApplyFailedEventArgs? failure = null;
        harness.Manager.ThemeApplyFailed += (_, args) => failure = args;

        ThemeApplyResult result = harness.Manager.Apply(theme, Immediate());

        Assert.Equal(ThemeApplyStatus.Failed, result.Status);
        Assert.Contains(result.Diagnostics, item => item.Code == "THEME_VALUE_UNSUPPORTED");
        Assert.NotNull(failure);
        Assert.Equal(theme.Id, failure.Theme.Id);
        Assert.Empty(harness.Resources);
        Assert.Null(harness.Manager.ActiveSnapshot);
    }

    private static ThemeDefinition Theme(string id, Color background)
    {
        var theme = new ThemeDefinition(id, id) { Variant = ThemeVariant.Custom };
        theme.Colors[ThemeTokens.Colors.Background.Name] = background;
        return theme;
    }

    private static ThemeApplyOptions Immediate()
        => new() { Transition = new ThemeTransitionOptions { Enabled = false } };

    private static ThemeApplyOptions Animated()
        => new()
        {
            Transition = new ThemeTransitionOptions
            {
                Enabled = true,
                Duration = TimeSpan.FromMilliseconds(100),
                Easing = ThemeEasing.Linear
            }
        };

    private static Color ResourceColor(ThemeManagerTestHarness harness, object key)
        => (Color)harness.Resources[key]!;

    private static string BrushKey(string name) => ThemeResourceKeys.Create(ThemeTokenCategory.Brush, name);
    private static string ResourceKey(string name) => ThemeResourceKeys.Create(ThemeTokenCategory.Resource, name);
    private static string SpacingKey(string name) => ThemeResourceKeys.Create(ThemeTokenCategory.Spacing, name);

    private sealed class UnsupportedBrush : ModernFormsNext.Drawing.Brush
    {
    }

    private static GradientBrush Gradient(
        string kind,
        Color first,
        Color second,
        float firstOffset,
        float secondOffset,
        Matrix3x2 transform,
        float opacity)
    {
        GradientBrush brush = kind switch
        {
            "linear" => new LinearGradientBrush
            {
                Start = new PointF(0f, 0f),
                End = new PointF(1f, 1f)
            },
            "radial" => new RadialGradientBrush
            {
                CenterPoint = new PointF(0.5f, 0.5f),
                GradientOrigin = new PointF(0.4f, 0.4f),
                Radius = 0.5f
            },
            "sweep" => new SweepGradientBrush
            {
                CenterPoint = new PointF(0.5f, 0.5f),
                StartAngle = 0f,
                EndAngle = 360f
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        brush.Opacity = opacity;
        brush.Transform = transform;
        brush.GradientStops.Add(new GradientStop(first, firstOffset));
        brush.GradientStops.Add(new GradientStop(second, secondOffset));
        return brush;
    }

    private static void ConfigureTargetGeometry(ModernFormsNext.Drawing.Brush brush)
    {
        switch (brush)
        {
            case LinearGradientBrush linear:
                linear.Start = new PointF(0.2f, 0.4f);
                linear.End = new PointF(0.8f, 0.6f);
                break;
            case RadialGradientBrush radial:
                radial.CenterPoint = new PointF(0.7f, 0.3f);
                radial.GradientOrigin = new PointF(0.6f, 0.2f);
                radial.Radius = 0.9f;
                break;
            case SweepGradientBrush sweep:
                sweep.CenterPoint = new PointF(0.7f, 0.3f);
                sweep.StartAngle = 90f;
                sweep.EndAngle = 180f;
                break;
        }
    }

    private static void AssertGeometryAtHalf(GradientBrush brush)
    {
        switch (brush)
        {
            case LinearGradientBrush linear:
                Assert.Equal(new PointF(0.1f, 0.2f), linear.Start);
                Assert.Equal(new PointF(0.9f, 0.8f), linear.End);
                break;
            case RadialGradientBrush radial:
                Assert.Equal(new PointF(0.6f, 0.4f), radial.CenterPoint);
                Assert.Equal(new PointF(0.5f, 0.3f), radial.GradientOrigin);
                Assert.Equal(0.7f, radial.Radius, 3);
                break;
            case SweepGradientBrush sweep:
                Assert.Equal(new PointF(0.6f, 0.4f), sweep.CenterPoint);
                Assert.Equal(45f, sweep.StartAngle, 3);
                Assert.Equal(270f, sweep.EndAngle, 3);
                break;
        }
    }
}
