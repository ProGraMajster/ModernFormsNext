using System.Drawing;
using System.Reflection;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class ThemeVisualRefreshTests
{
    [Fact]
    public void NormalControlRefreshesImmediatelyAfterApply()
    {
        using var window = new TestWindow();
        using var context = new ThemeVisualTestContext(window);
        using var control = new ThemeProbeButton();
        window.Controls.Add(control);
        control.CreateControl();
        SKColor before = control.CurrentStyle.GetBackgroundColor();
        Reset(window, control);

        ThemeApplyResult result = context.ApplyDark(Immediate());

        Assert.True(result.Success);
        Assert.NotEqual(before, control.CurrentStyle.GetBackgroundColor());
        Assert.Equal(Theme.BackgroundColor, control.CurrentStyle.GetBackgroundColor());
        Assert.Equal(1, control.ThemeChanges);
        Assert.Equal(1, control.Invalidations);
        Assert.Equal(1, window.InvalidationCount);
    }

    [Fact]
    public void HoverStateIsPreservedAndUsesNewThemeStyle()
    {
        using var window = new TestWindow();
        using var context = new ThemeVisualTestContext(window);
        using var control = new ThemeProbeButton();
        window.Controls.Add(control);
        control.CreateControl();
        control.EnterHover();
        SKColor before = control.CurrentStyle.GetBackgroundColor();
        Reset(window, control);

        context.ApplyDark(Immediate());

        Assert.True(control.IsHovering);
        Assert.NotEqual(before, control.CurrentStyle.GetBackgroundColor());
        Assert.Equal(Theme.AccentColor, control.CurrentStyle.GetBackgroundColor());
        Assert.Equal(1, control.ThemeChanges);
        Assert.Equal(1, window.InvalidationCount);
    }

    [Fact]
    public void PressedFocusedAndDisabledControlsRefreshTheirActiveState()
    {
        using var window = new TestWindow();
        using var context = new ThemeVisualTestContext(window);
        using var pressed = new ThemeProbeSwitch();
        using var focused = new ThemeProbeButton();
        using var disabled = new ThemeProbeButton();
        window.Controls.Add(pressed);
        window.Controls.Add(focused);
        window.Controls.Add(disabled);
        pressed.CreateControl();
        focused.CreateControl();
        disabled.CreateControl();
        pressed.SetThumbPressedForThemeTest();
        focused.Select();
        disabled.Enabled = false;
        SKColor focusedBefore = focused.CurrentStyle.GetBackgroundColor();
        SKColor disabledBefore = disabled.CurrentStyle.GetBackgroundColor();
        Reset(window, pressed, focused, disabled);

        context.ApplyDark(Immediate());

        Assert.True(pressed.ThumbPressed);
        Assert.True(focused.Focused);
        Assert.False(disabled.Enabled);
        Assert.NotEqual(focusedBefore, focused.CurrentStyle.GetBackgroundColor());
        Assert.NotEqual(disabledBefore, disabled.CurrentStyle.GetBackgroundColor());
        Assert.All(new IThemeProbe[] { pressed, focused, disabled }, control =>
        {
            Assert.Equal(1, control.ThemeChanges);
            Assert.Equal(1, control.Invalidations);
        });
        Assert.Equal(1, window.InvalidationCount);
    }

    [Fact]
    public void ThemeResourceChangeInvalidatesWithoutMouseInput()
    {
        using var window = new TestWindow();
        using var context = new ThemeVisualTestContext(window);
        using var control = new ThemeResourceProbeControl();
        window.Controls.Add(control);
        control.CreateControl();
        control.SetResourceReference(
            nameof(ThemeResourceProbeControl.ThemeColor),
            ThemeTokens.Colors.Background.ResourceKey);
        Color before = control.ThemeColor;
        Reset(window, control);
        control.ResetSetters();

        context.ApplyDark(Immediate());

        Assert.NotEqual(before, control.ThemeColor);
        Assert.Equal(1, control.SetterCalls);
        Assert.True(control.Invalidations >= 1);
        Assert.Equal(1, window.InvalidationCount);
    }

    [Fact]
    public void NestedControlTreeRefreshesTogether()
    {
        using var window = new TestWindow();
        using var context = new ThemeVisualTestContext(window);
        using var parent = new ThemeProbeControl();
        using var child = new ThemeProbeControl();
        using var grandchild = new ThemeProbeControl();
        window.Controls.Add(parent);
        parent.Controls.Add(child);
        child.Controls.Add(grandchild);
        parent.CreateControl();
        Reset(window, parent, child, grandchild);

        context.ApplyDark(Immediate());

        Assert.All(new[] { parent, child, grandchild }, control =>
        {
            Assert.Equal(1, control.ThemeChanges);
            Assert.Equal(1, control.Invalidations);
        });
        Assert.Equal(1, window.InvalidationCount);
    }

    [Fact]
    public void MultipleOpenWindowsRefreshOnceEach()
    {
        using var firstWindow = new TestWindow();
        using var secondWindow = new TestWindow();
        using var context = new ThemeVisualTestContext(firstWindow, secondWindow);
        using var first = new ThemeProbeControl();
        using var second = new ThemeProbeControl();
        firstWindow.Controls.Add(first);
        secondWindow.Controls.Add(second);
        first.CreateControl();
        second.CreateControl();
        Reset(firstWindow, first);
        Reset(secondWindow, second);

        context.ApplyDark(Immediate());

        Assert.Equal(1, first.ThemeChanges);
        Assert.Equal(1, second.ThemeChanges);
        Assert.Equal(1, firstWindow.InvalidationCount);
        Assert.Equal(1, secondWindow.InvalidationCount);
    }

    [Fact]
    public async Task AnimatedTransitionInvalidatesSuccessiveTicks()
    {
        using var window = new TestWindow();
        using var context = new ThemeVisualTestContext(window);
        using var control = new ThemeProbeControl();
        window.Controls.Add(control);
        control.CreateControl();
        Reset(window, control);

        ThemeApplyResult result = context.ApplyDark(Animated());

        Assert.NotNull(result.Transition);
        Assert.Equal(1, window.InvalidationCount);
        Reset(window, control);
        context.Harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));
        Assert.Equal(1, window.InvalidationCount);
        Assert.Equal(0, control.ThemeChanges);
        Assert.Equal(1, control.Invalidations);
        Reset(window, control);
        context.Harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));
        Assert.Equal(1, window.InvalidationCount);
        Assert.Equal(0, control.ThemeChanges);
        Assert.Equal(1, control.Invalidations);

        context.Harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(ThemeTransitionStatus.Completed, await result.Transition!.Completion);
    }

    [Fact]
    public void ImmediateApplyCoalescesManyInvalidationsIntoOneWindowRefresh()
    {
        using var window = new TestWindow();
        using var context = new ThemeVisualTestContext(window);
        using var first = new ThemeResourceProbeControl();
        using var second = new ThemeResourceProbeControl();
        using var third = new ThemeResourceProbeControl();
        window.Controls.Add(first);
        window.Controls.Add(second);
        window.Controls.Add(third);
        foreach (ThemeResourceProbeControl control in new[] { first, second, third })
        {
            control.CreateControl();
            control.SetResourceReference(
                nameof(ThemeResourceProbeControl.ThemeColor),
                ThemeTokens.Colors.Background.ResourceKey);
            control.ResetObservations();
        }
        window.ResetInvalidations();

        context.ApplyDark(Immediate());

        Assert.All(new[] { first, second, third }, control => Assert.Equal(1, control.SetterCalls));
        Assert.Equal(1, window.InvalidationCount);
        Assert.False(Application.HasPendingVisualInvalidations);
    }

    [Fact]
    public void ReparentingDoesNotDuplicateSubscriptionsAndDisposedControlStopsUpdating()
    {
        using var window = new TestWindow();
        using var context = new ThemeVisualTestContext(window);
        using var firstParent = new ThemeProbeControl();
        using var secondParent = new ThemeProbeControl();
        var control = new ThemeResourceProbeControl();
        window.Controls.Add(firstParent);
        window.Controls.Add(secondParent);
        firstParent.Controls.Add(control);
        firstParent.CreateControl();
        secondParent.CreateControl();
        control.SetResourceReference(
            nameof(ThemeResourceProbeControl.ThemeColor),
            ThemeTokens.Colors.Background.ResourceKey);
        firstParent.Controls.Remove(control);
        secondParent.Controls.Add(control);
        control.ResetObservations();
        window.ResetInvalidations();

        context.ApplyDark(Immediate());

        Assert.Equal(1, control.SetterCalls);
        secondParent.Controls.Remove(control);
        control.Dispose();
        control.ResetObservations();
        window.ResetInvalidations();

        context.ApplyLight(Immediate());

        Assert.Equal(0, control.SetterCalls);
        Assert.Equal(0, control.Invalidations);
        Assert.Equal(1, window.InvalidationCount);
    }

    [Fact]
    public async Task CompletedTransitionLeavesSchedulerAndInvalidationQueueIdle()
    {
        using var window = new TestWindow();
        using var context = new ThemeVisualTestContext(window);
        using var control = new ThemeProbeControl();
        window.Controls.Add(control);
        control.CreateControl();

        ThemeApplyResult result = context.ApplyDark(Animated());
        context.Harness.SchedulerHarness.AdvanceAndTick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(ThemeTransitionStatus.Completed, await result.Transition!.Completion);
        Assert.Equal(0, context.Harness.SchedulerHarness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(context.Harness.SchedulerHarness.TickSource.IsRunning);
        Assert.False(Application.HasPendingVisualInvalidations);
        window.ResetInvalidations();
        context.Harness.SchedulerHarness.TickSource.Fire();
        Assert.Equal(0, window.InvalidationCount);
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

    private static void Reset(TestWindow window, params IThemeProbe[] controls)
    {
        window.ResetInvalidations();
        foreach (IThemeProbe control in controls)
            control.ResetObservations();
    }

    private sealed class ThemeVisualTestContext : IDisposable
    {
        private readonly Dictionary<string, object> originalTheme = Theme.GetValueSnapshot();
        private readonly Dictionary<object, object?> originalResources =
            Application.ThemeResourcesInternal.GetSnapshot();

        public ThemeVisualTestContext(params TestWindow[] windows)
        {
            var legacyStore = new WindowThemeLegacyStore(windows);
            Harness = new ThemeManagerTestHarness(
                resources: Application.ThemeResourcesInternal,
                legacyStore: legacyStore);
            ThemeApplyResult result = Harness.Manager.Apply(BuiltInThemes.Light, Immediate());
            Assert.True(result.Success);
            foreach (TestWindow window in windows)
                window.ResetInvalidations();
        }

        public ThemeManagerTestHarness Harness { get; }

        public ThemeApplyResult ApplyDark(ThemeApplyOptions options)
            => Harness.Manager.Apply(BuiltInThemes.Dark, options);

        public ThemeApplyResult ApplyLight(ThemeApplyOptions options)
            => Harness.Manager.Apply(BuiltInThemes.Light, options);

        public void Dispose()
        {
            Harness.Dispose();
            Theme.ReplaceValuesWithoutNotification(originalTheme);
            Theme.NotifyChanged();
            ResourceDictionaryChange[] changes =
                Application.ThemeResourcesInternal.ReplaceSnapshot(originalResources);
            Application.ThemeResourcesInternal.PublishChanges(changes);
        }
    }

    private sealed class WindowThemeLegacyStore(IEnumerable<WindowBase> windows) : IThemeLegacyStore
    {
        private readonly WindowBase[] windows = windows.ToArray();
        private Dictionary<string, object> values = Theme.GetValueSnapshot();

        public Dictionary<string, object> GetSnapshot() => new(values, StringComparer.Ordinal);

        public void Replace(IReadOnlyDictionary<string, object> replacement)
            => values = new Dictionary<string, object>(replacement, StringComparer.Ordinal);

        public void NotifyChanged()
        {
            Theme.ReplaceValuesWithoutNotification(values);
            Theme.NotifyChanged();
            Application.DoThemeChanged(windows);
        }
    }

    private interface IThemeProbe
    {
        int ThemeChanges { get; }
        int Invalidations { get; }
        void ResetObservations();
    }

    private class ThemeProbeControl : Control, IThemeProbe
    {
        public int ThemeChanges { get; private set; }
        public int Invalidations { get; private set; }

        public virtual void ResetObservations()
        {
            ThemeChanges = 0;
            Invalidations = 0;
        }

        protected internal override void OnThemeChanged(EventArgs e)
        {
            ThemeChanges++;
            base.OnThemeChanged(e);
        }

        protected override void OnInvalidated(EventArgs<Rectangle> e)
        {
            Invalidations++;
            base.OnInvalidated(e);
        }
    }

    private sealed class ThemeProbeButton : Button, IThemeProbe
    {
        public int ThemeChanges { get; private set; }
        public int Invalidations { get; private set; }

        public void EnterHover()
            => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));

        public void ResetObservations()
        {
            ThemeChanges = 0;
            Invalidations = 0;
        }

        protected internal override void OnThemeChanged(EventArgs e)
        {
            ThemeChanges++;
            base.OnThemeChanged(e);
        }

        protected override void OnInvalidated(EventArgs<Rectangle> e)
        {
            Invalidations++;
            base.OnInvalidated(e);
        }
    }

    private sealed class ThemeProbeSwitch : Switch, IThemeProbe
    {
        public int ThemeChanges { get; private set; }
        public int Invalidations { get; private set; }

        public void SetThumbPressedForThemeTest()
            // This test needs simultaneous active states on independent controls. Invoke the
            // Switch state handler directly instead of moving real focus/capture between them.
            => OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, Point.Empty));

        public void ResetObservations()
        {
            ThemeChanges = 0;
            Invalidations = 0;
        }

        protected internal override void OnThemeChanged(EventArgs e)
        {
            ThemeChanges++;
            base.OnThemeChanged(e);
        }

        protected override void OnInvalidated(EventArgs<Rectangle> e)
        {
            Invalidations++;
            base.OnInvalidated(e);
        }
    }

    private sealed class ThemeResourceProbeControl : ThemeProbeControl
    {
        private Color themeColor;

        public Color ThemeColor
        {
            get => themeColor;
            set
            {
                if (themeColor == value)
                    return;

                themeColor = value;
                SetterCalls++;
                Invalidate();
            }
        }

        public int SetterCalls { get; private set; }

        public void ResetSetters() => SetterCalls = 0;

        public override void ResetObservations()
        {
            base.ResetObservations();
            ResetSetters();
        }
    }

    private sealed class TestWindow : WindowBase
    {
        private readonly RecordingWindowProxy proxy;

        public TestWindow()
            : this(DispatchProxy.Create<IWindowBaseImpl, RecordingWindowProxy>())
        {
        }

        private TestWindow(IWindowBaseImpl implementation)
            : base(implementation)
        {
            proxy = (RecordingWindowProxy)implementation;
        }

        public int InvalidationCount => proxy.InvalidationCount;

        public void ResetInvalidations() => proxy.InvalidationCount = 0;
    }

    private class RecordingWindowProxy : DispatchProxy
    {
        public int InvalidationCount { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IWindowBaseImpl.Invalidate))
                InvalidationCount++;

            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
                return null;

            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
