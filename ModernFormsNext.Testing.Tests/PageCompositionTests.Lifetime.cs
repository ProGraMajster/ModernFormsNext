using ModernFormsNext.Accessibility;
using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class PageCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlyoutNewerAppearanceClosePreservesCurrentEditorAndRetiresOldWork(bool neutral)
    {
        using var f = new FlyoutFixture(neutral);
        f.DetailEditor.Select();
        f.Menu.Appearing += (_, _) => Assert.True(f.Page.TrySetFlyoutOpen(false));
        var request = f.Page.SetFlyoutOpenAsync(true);
        Assert.False(CompleteComposition(request));
        Assert.False(f.Page.IsFlyoutOpen);
        Assert.Same(f.DetailEditor, f.Focused);
        Assert.True(f.Detail.Enabled);
        Assert.Equal(PageLifecycleState.Attached, f.Menu.LifecycleState);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlyoutNewerBackgroundFocusCannotBeRetiredByOlderOpeningPass(bool neutral)
    {
        using var f = new FlyoutFixture(neutral);
        f.DetailEditor.Select();
        f.Menu.Appearing += (_, _) => f.DetailEditor.Select();
        Assert.False(CompleteComposition(f.Page.SetFlyoutOpenAsync(true)));
        Assert.False(f.Page.IsFlyoutOpen);
        Assert.Same(f.DetailEditor, f.Focused);
        Assert.True(f.Detail.Enabled);
    }

    [Fact]
    public void FlyoutNestedNavigationGetsNearestBackBeforeOverlayDismissal()
    {
        using var f = new FlyoutFixture(false);
        var root = NewCompositionPage("nested root");
        var nested = new NavigationPage(root) { TransitionDuration = TimeSpan.Zero };
        var previousContent = f.Menu.Content!;
        f.Menu.Content = new PageHost { Page = nested, Dock = DockStyle.Fill };
        previousContent.Dispose();
        f.DetailEditor.Select();
        Assert.True(f.Page.TrySetFlyoutOpen(true));
        var next = NewCompositionPage("nested detail");
        Assert.True(CompleteComposition(nested.PushAsync(next, animated: false)));
        f.Key(Keys.BrowserBack);
        Assert.True(f.Page.IsFlyoutOpen);
        Assert.Same(root, nested.CurrentPage);
        Assert.Null(next.Parent);
        next.Dispose();
        f.Key(Keys.BrowserBack);
        Assert.False(f.Page.IsFlyoutOpen);
        Assert.Same(f.DetailEditor, f.Focused);
    }

    [Fact]
    public void FlyoutAncestorRetirementCancelsOldEffectWithoutLosingOwnedSlots()
    {
        using var f = new FlyoutFixture(false);
        using var foreign = new Panel();
        f.DetailEditor.Select();
        var request = f.Page.SetFlyoutOpenAsync(true);
        Assert.False(request.IsCompleted);
        foreign.Controls.Add(f.Presenter);
        Assert.False(CompleteComposition(request));
        f.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Same(f.Menu, f.Page.Flyout);
        Assert.Same(f.Detail, f.Page.Detail);
        Assert.Equal(PageLifecycleState.Attached, f.Menu.LifecycleState);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        f.Root.Controls.Add(f.Presenter);
        Assert.Equal(PageLifecycleState.Appeared, f.Menu.LifecycleState);
    }

    [Fact]
    public void FlyoutSlotReplacementTransfersOldPageAndRejectsDuplicateOrForeignOwnership()
    {
        using var f = new FlyoutFixture(false);
        using var foreign = new Panel();
        using var next = NewCompositionPage("replacement");
        f.DetailEditor.Select();
        bool cancel = true;
        f.DetailEditor.Validating += (_, e) => e.Cancel = cancel;
        Assert.False(f.Page.TrySetDetail(next));
        Assert.Null(next.Parent);
        Assert.Throws<ArgumentException>(() => f.Page.TrySetFlyout(f.Detail));
        cancel = false;
        Assert.True(f.Page.TrySetDetail(next));
        Assert.Null(f.Detail.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, f.Detail.LifecycleState);
        foreign.Controls.Add(f.Detail);
        Assert.Throws<InvalidOperationException>(() => f.Page.TrySetFlyout(f.Detail));
        f.Page.Dispose();
        Assert.NotEqual(PageLifecycleState.Disposed, f.Detail.LifecycleState);
        Assert.Equal(PageLifecycleState.Disposed, next.LifecycleState);
    }

    [Fact]
    public void FlyoutDisposalKeepsSiblingTransferredByAnEarlierCallbackAlive()
    {
        using var f = new FlyoutFixture(false);
        using var foreign = new Panel();
        f.Page.TrySetFlyoutOpen(true);
        f.Detail.Disappearing += (_, _) => foreign.Controls.Add(f.Menu);
        f.Page.Dispose();
        Assert.Equal(PageLifecycleState.Disposed, f.Detail.LifecycleState);
        Assert.NotEqual(PageLifecycleState.Disposed, f.Menu.LifecycleState);
        Assert.Same(foreign, f.Menu.Parent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlyoutOverlayTabWrapUsesActualFocusTraversal(bool neutral)
    {
        using var f = new FlyoutFixture(neutral);
        var previousContent = f.Menu.Content!;
        var body = new Panel();
        var first = body.Controls.Add(new Button { Text = "first", Bounds = new(0, 0, 100, 32), TabIndex = 0 });
        var second = body.Controls.Add(new Button { Text = "second", Bounds = new(0, 40, 100, 32), TabIndex = 1 });
        f.Menu.Content = body;
        previousContent.Dispose();
        f.DetailEditor.Select();
        f.Page.TrySetFlyoutOpen(true);
        first.Select();
        f.Key(Keys.Tab);
        Assert.Same(second, f.Focused);
        f.Key(Keys.Tab);
        Assert.Same(first, f.Focused);
        f.Key(Keys.Shift | Keys.Tab);
        Assert.Same(second, f.Focused);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlyoutZeroDurationAndReducedMotionUseProductionPolicy(bool reduced)
    {
        using var f = new FlyoutFixture(false);
        if (reduced) AnimationScheduler.Default.Policy.ReducedMotion = true;
        else f.Page.TransitionDuration = TimeSpan.Zero;
        Assert.True(CompleteComposition(f.Page.SetFlyoutOpenAsync(true)));
        Assert.Equal(PageLifecycleState.Appeared, f.Menu.LifecycleState);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void FlyoutNewerModeFromAnimationCompletionDoesNotPublishAnOlderSplitPass()
    {
        using var f = new FlyoutFixture(false);
        f.Presenter.Width = 400;
        f.Page.SplitBreakpoint = 500;
        f.Page.DisplayMode = FlyoutDisplayMode.Auto;
        var observed = new List<FlyoutDisplayMode>();
        f.Page.EffectiveDisplayModeChanged += (_, _) => observed.Add(f.Page.EffectiveDisplayMode);
        var opening = f.Page.SetFlyoutOpenAsync(true);
        Assert.False(opening.IsCompleted);
        bool completedAppearance = false;
        f.Menu.Appeared += (_, _) => { completedAppearance = true; f.Page.DisplayMode = FlyoutDisplayMode.Overlay; };
        f.Presenter.Width = 600;
        Assert.False(CompleteComposition(opening));
        Assert.True(f.Page.EffectiveDisplayMode == FlyoutDisplayMode.Overlay,
            $"Appeared={completedAppearance}; authored={f.Page.DisplayMode}; effective={f.Page.EffectiveDisplayMode}; lifecycle={f.Menu.LifecycleState}; observed={string.Join(',', observed)}");
        Assert.DoesNotContain(FlyoutDisplayMode.Split, observed);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void DetachedCompositionConstructorsAndOpenIntentDoNotStartNativePresentation()
    {
        using var host = ModernFormsTestHost.Create();
        using var flyout = new FlyoutPage { DisplayMode = FlyoutDisplayMode.Overlay };
        var menu = NewCompositionPage("menu");
        var detail = NewCompositionPage("detail");
        flyout.Flyout = menu; flyout.Detail = detail;
        int appeared = 0;
        menu.Appearing += (_, _) => appeared++;
        Assert.True(CompleteComposition(flyout.SetFlyoutOpenAsync(true)));
        Assert.True(flyout.IsFlyoutOpen);
        Assert.Equal(0, appeared);
        Assert.Equal(PageLifecycleState.Attached, menu.LifecycleState);
        Assert.Equal(PageLifecycleState.Attached, detail.LifecycleState);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }
}
