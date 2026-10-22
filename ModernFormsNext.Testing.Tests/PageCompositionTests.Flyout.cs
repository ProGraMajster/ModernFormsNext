using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class PageCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlyoutAdmissionPreservesEditedFocusUntilOpenOrCloseIsAccepted(bool neutral)
    {
        using var f = new FlyoutFixture(neutral);
        Assert.Equal(PageLifecycleState.Appeared, f.Detail.LifecycleState);
        Assert.Equal(PageLifecycleState.Attached, f.Menu.LifecycleState);
        bool cancelDetail = true, cancelMenu = true;
        f.DetailEditor.Validating += (_, e) => e.Cancel = cancelDetail;
        f.MenuEditor.Validating += (_, e) => e.Cancel = cancelMenu;
        f.DetailEditor.Select();
        Assert.False(f.Page.TrySetFlyoutOpen(true));
        Assert.False(f.Page.IsFlyoutOpen);
        Assert.Same(f.DetailEditor, f.Focused);
        cancelDetail = false;
        Assert.True(f.Page.TrySetFlyoutOpen(true));
        Assert.Same(f.MenuEditor, f.Focused);
        Assert.False(f.Detail.Enabled);
        Assert.Equal(PageLifecycleState.Appeared, f.Menu.LifecycleState);
        Assert.False(f.Page.TrySetFlyoutOpen(false));
        Assert.Same(f.MenuEditor, f.Focused);
        Assert.True(f.Page.IsFlyoutOpen);
        cancelMenu = false;
        Assert.True(f.Page.TrySetFlyoutOpen(false));
        Assert.False(f.Page.IsFlyoutOpen);
        Assert.Same(f.DetailEditor, f.Focused);
        Assert.True(f.Detail.Enabled);
        Assert.Equal(PageLifecycleState.Attached, f.Menu.LifecycleState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlyoutBackAndEscapeUseScopedCommandsAndConsumeValidationVeto(bool neutral)
    {
        using var f = new FlyoutFixture(neutral);
        f.DetailEditor.Select();
        Assert.True(f.Page.TrySetFlyoutOpen(true));
        bool cancel = true;
        f.MenuEditor.Validating += (_, e) => e.Cancel = cancel;
        f.Key(Keys.BrowserBack);
        Assert.True(f.Page.IsFlyoutOpen);
        Assert.Same(f.MenuEditor, f.Focused);
        cancel = false;
        f.Key(Keys.Escape);
        Assert.False(f.Page.IsFlyoutOpen);
        Assert.Same(f.DetailEditor, f.Focused);
    }

    [Fact]
    public void FlyoutBackdropUsesActualHitTestingCaptureAndCanonicalValidation()
    {
        using var f = new FlyoutFixture(false);
        f.DetailEditor.Select();
        f.Page.TrySetFlyoutOpen(true);
        var peer = Descendants(f.Page.AccessibilityObject).Single(item => item.Name == "Dismiss navigation pane");
        var control = Assert.IsAssignableFrom<Control.ControlAccessibleObject>(peer).Owner!;
        bool cancel = true;
        f.MenuEditor.Validating += (_, e) => e.Cancel = cancel;
        f.Window!.Input.Click(control);
        Assert.True(f.Page.IsFlyoutOpen);
        Assert.Same(f.MenuEditor, f.Focused);
        Assert.False(f.Root.Capture);
        cancel = false;
        f.Window.Input.Click(control);
        Assert.False(f.Page.IsFlyoutOpen);
        Assert.False(f.Root.Capture);
        Assert.Equal(AccessibleActions.None, peer.SupportedActions);
        Assert.False(peer.PerformAction(AccessibleActions.Invoke));
    }

    [Fact]
    public void FlyoutSharedAnimationDefersAppearanceAndCloseRetiresOlderCompletion()
    {
        using var f = new FlyoutFixture(false);
        f.Menu.Opacity = .6f;
        f.DetailEditor.Select();
        var opening = f.Page.SetFlyoutOpenAsync(true);
        Assert.True(f.Page.IsFlyoutOpen);
        Assert.False(opening.IsCompleted);
        Assert.Equal(PageLifecycleState.Appearing, f.Menu.LifecycleState);
        Assert.Equal(.6f, f.Menu.Opacity);
        f.Host.Clock.Advance(TimeSpan.FromMilliseconds(90));
        Assert.False(opening.IsCompleted);
        Assert.True(f.Page.TrySetFlyoutOpen(false));
        Assert.False(CompleteComposition(opening));
        Assert.False(f.Page.IsFlyoutOpen);
        f.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(PageLifecycleState.Attached, f.Menu.LifecycleState);
        Assert.Equal(.6f, f.Menu.Opacity);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void FlyoutCancellationBeforeCommitAndAfterCommitHaveDistinctOwnershipSemantics()
    {
        using var f = new FlyoutFixture(false);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.True(f.Page.SetFlyoutOpenAsync(true, cancellationToken: cancelled.Token).IsCanceled);
        Assert.False(f.Page.IsFlyoutOpen);
        using var visual = new CancellationTokenSource();
        var opening = f.Page.SetFlyoutOpenAsync(true, cancellationToken: visual.Token);
        visual.Cancel();
        Assert.True(CompleteComposition(opening));
        Assert.True(f.Page.IsFlyoutOpen);
        Assert.Equal(PageLifecycleState.Appeared, f.Menu.LifecycleState);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        f.Page.TrySetFlyoutOpen(false);
        f.DetailEditor.Select();
        using var precommit = new CancellationTokenSource();
        f.DetailEditor.Validating += (_, _) => precommit.Cancel();
        var rejected = f.Page.SetFlyoutOpenAsync(true, cancellationToken: precommit.Token);
        Assert.True(rejected.IsCanceled);
        Assert.False(f.Page.IsFlyoutOpen);
        Assert.Same(f.DetailEditor, f.Focused);
    }

    [Theory]
    [InlineData(1d, FlowDirection.LeftToRight)]
    [InlineData(1.5d, FlowDirection.LeftToRight)]
    [InlineData(2d, FlowDirection.LeftToRight)]
    [InlineData(1d, FlowDirection.RightToLeft)]
    [InlineData(1.5d, FlowDirection.RightToLeft)]
    [InlineData(2d, FlowDirection.RightToLeft)]
    public void FlyoutSplitGeometryUsesLogicalPaddingWidthAndLocalDirection(double scale, FlowDirection direction)
    {
        using var f = new FlyoutFixture(false, scale);
        f.Page.Padding = new Padding(10);
        f.Page.FlowDirection = direction;
        f.Page.DisplayMode = FlyoutDisplayMode.Split;
        f.Window!.LayoutUntilStable();
        var menuHost = f.Menu.Parent!.Parent!;
        var detailHost = f.Detail.Parent!.Parent!;
        Assert.Equal(new Rectangle(direction == FlowDirection.LeftToRight ? 10 : 390, 10, 200, 340), menuHost.Bounds);
        Assert.Equal(new Rectangle(direction == FlowDirection.LeftToRight ? 210 : 10, 10, 380, 340), detailHost.Bounds);
        Assert.Equal(PageLifecycleState.Appeared, f.Menu.LifecycleState);
        Assert.Equal(PageLifecycleState.Appeared, f.Detail.LifecycleState);
        Assert.False(f.Page.IsFlyoutOpen);
        Assert.Equal(FlyoutDisplayMode.Split, f.Page.EffectiveDisplayMode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResponsiveFlyoutPreservesFocusedSplitEditorAcrossBreakpoint(bool neutral)
    {
        using var f = new FlyoutFixture(neutral);
        f.Page.DisplayMode = FlyoutDisplayMode.Auto;
        f.Page.SplitBreakpoint = 500;
        Assert.Equal(FlyoutDisplayMode.Split, f.Page.EffectiveDisplayMode);
        f.MenuEditor.Select();
        int disappearing = 0;
        f.Menu.Disappearing += (_, _) => disappearing++;
        f.Presenter.Width = 400;
        Assert.Equal(FlyoutDisplayMode.Overlay, f.Page.EffectiveDisplayMode);
        Assert.True(f.Page.IsFlyoutOpen);
        Assert.Same(f.MenuEditor, f.Focused);
        Assert.Equal(0, disappearing);
        f.Presenter.Width = 600;
        Assert.Equal(FlyoutDisplayMode.Split, f.Page.EffectiveDisplayMode);
        Assert.Same(f.MenuEditor, f.Focused);
        Assert.True(f.Page.IsFlyoutOpen);
    }

    [Fact]
    public void FlyoutThrowingAppearanceSettlesCommittedStateWithoutLosingSlots()
    {
        using var f = new FlyoutFixture(false);
        EventHandler throwing = (_, _) => throw new InvalidOperationException("appearance observer");
        f.Menu.Appearing += throwing;
        var request = f.Page.SetFlyoutOpenAsync(true);
        Assert.Throws<InvalidOperationException>(() => CompleteComposition(request));
        f.Menu.Appearing -= throwing;
        Assert.True(f.Page.IsFlyoutOpen);
        Assert.Same(f.Menu, f.Page.Flyout);
        Assert.Same(f.Detail, f.Page.Detail);
        Assert.Equal(PageLifecycleState.Appeared, f.Menu.LifecycleState);
        Assert.True(f.Page.TrySetFlyoutOpen(false));
    }

    private static T CompleteComposition<T>(Task<T> task)
    {
        Assert.True(task.IsCompleted, "The immediate or clock-completed operation must already be terminal.");
        return task.GetAwaiter().GetResult();
    }
    private sealed class FlyoutFixture : IDisposable
    {
        private readonly bool neutral;
        private SkiaControlSurface? surface;
        internal ModernFormsTestHost Host { get; }
        internal FlyoutPage Page { get; }
        internal ContentPage Menu { get; } = NewCompositionPage("menu");
        internal ContentPage Detail { get; } = NewCompositionPage("detail");
        internal TextBox MenuEditor => (TextBox)Menu.Content!;
        internal TextBox DetailEditor => (TextBox)Detail.Content!;
        internal Panel Root { get; } = new() { Width = 600, Height = 400 };
        internal PageHost Presenter { get; }
        internal TestWindowHost? Window { get; }
        internal Control? Focused => surface?.SelectedControl ?? Window?.FocusedControl;
        internal FlyoutFixture(bool neutral, double scale = 1)
        {
            this.neutral = neutral;
            Host = ModernFormsTestHost.Create(new TestViewport(600, 400, scale));
            Page = new() { DisplayMode = FlyoutDisplayMode.Overlay, FlyoutWidth = 200, Flyout = Menu, Detail = Detail };
            Presenter = Root.Controls.Add(new PageHost { Page = Page, Bounds = new(0, 0, 600, 360) });
            Root.Controls.Add(new Button { Text = "outside", Bounds = new(0, 365, 100, 32) });
            if (neutral) { surface = new(Root); surface.Resize(600, 400); }
            else Window = Host.Show(Root);
        }
        internal void Key(Keys key)
        {
            if (neutral) { surface!.ProcessKeyDown(key); surface.ProcessKeyUp(key); }
            else Window!.Input.PressKey(key);
        }
        public void Dispose() { surface?.Dispose(); Host.Dispose(); Root.Dispose(); }
    }
}
