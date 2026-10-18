using System.Drawing;
using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class PageNavigationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructorsAreInertAndMountHideShowFollowActualHost(bool neutral)
    {
        using var fixture = new Fixture(neutral, mount: false);
        var order = new List<string>();
        fixture.Root.Appearing += (_, _) => { Assert.True(fixture.Root.IsPresented); order.Add("appearing"); };
        fixture.Root.Appeared += (_, _) => order.Add("appeared");
        fixture.Root.Disappearing += (_, _) => { Assert.False(fixture.Root.IsPresented); order.Add("disappearing"); };
        fixture.Root.Disappeared += (_, _) => order.Add("disappeared");
        Assert.Equal(PageLifecycleState.Attached, fixture.Root.LifecycleState);
        Assert.Empty(order);
        fixture.Mount();
        Assert.Equal(new[] { "appearing", "appeared" }, order);
        fixture.Presenter.Hide();
        Assert.Equal(PageLifecycleState.Attached, fixture.Root.LifecycleState);
        fixture.Presenter.Show();
        Assert.Equal(new[] { "appearing", "appeared", "disappearing", "disappeared", "appearing", "appeared" }, order);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StackTransfersPopAndRetainsRootWithoutDisposing(bool neutral)
    {
        using var f = new Fixture(neutral);
        using var detail = NewPage("detail");
        using var last = NewPage("last");
        Assert.True(Done(f.Navigation.PushAsync(detail, false)));
        var snapshot = f.Navigation.NavigationStack;
        Assert.True(Done(f.Navigation.PushAsync(last, false)));
        Assert.Equal(new Page[] { f.Root, detail }, snapshot);
        Assert.Equal(PageLifecycleState.Attached, f.Root.LifecycleState);
        Assert.Same(last, Done(f.Navigation.PopAsync(false)));
        Assert.Null(last.Parent);
        Assert.NotEqual(PageLifecycleState.Disposed, last.LifecycleState);
        Assert.Equal(PageLifecycleState.Detached, last.LifecycleState);
        Assert.Equal(new Page[] { detail }, Done(f.Navigation.PopToRootAsync(false)));
        Assert.Null(detail.Parent);
        Assert.Same(f.Root, f.Navigation.CurrentPage);
        Assert.Null(Done(f.Navigation.PopAsync(false)));
        Assert.False(f.Navigation.CanGoBack);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalValidationVetoRetainsFocusCompositionAndOwnership(bool neutral)
    {
        using var f = new Fixture(neutral);
        var editor = Assert.IsType<TextBox>(f.Root.Content);
        editor.Select();
        Assert.Same(editor, f.Focused);
        bool cancel = true;
        editor.Validating += (_, e) => e.Cancel = cancel;
        using var next = NewPage("next");
        Assert.False(Done(f.Navigation.PushAsync(next, false)));
        Assert.Same(f.Root, f.Navigation.CurrentPage);
        Assert.Same(editor, f.Focused);
        Assert.Null(next.Parent);
        cancel = false;
        Assert.True(Done(f.Navigation.PushAsync(next, false)));
        Assert.Same(next.Content, f.Focused);
        Assert.Same(next, Done(f.Navigation.PopAsync(false)));
        Assert.Same(editor, f.Focused);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusRedirectVetoDoesNotStealOutsideFocus(bool neutral)
    {
        using var f = new Fixture(neutral);
        var outside = f.RootContainer.Controls.Add(new Button { Text = "outside", Width = 60, Height = 30 });
        var editor = Assert.IsType<TextBox>(f.Root.Content);
        editor.Select();
        editor.Validating += (_, _) => outside.Select();
        using var next = NewPage("next");
        Assert.False(Done(f.Navigation.PushAsync(next, false)));
        Assert.Same(outside, f.Focused);
        Assert.Same(f.Root, f.Navigation.CurrentPage);
        Assert.True(Done(f.Navigation.PushAsync(next, false)));
        Assert.Same(outside, f.Focused);
    }

    [Fact]
    public void SharedClockSerializesReentrantRequestsAndPreservesAuthoredOpacity()
    {
        using var f = new Fixture(false);
        using var second = NewPage("second");
        using var third = NewPage("third");
        second.Opacity = .65f;
        Task<bool>? queued = null;
        second.Appearing += (_, _) => { if (queued is null) queued = f.Navigation.PushAsync(third); };
        var first = f.Navigation.PushAsync(second);
        Assert.False(first.IsCompleted);
        Assert.False(queued!.IsCompleted);
        Assert.Same(second, f.Navigation.CurrentPage);
        Assert.Equal(.65f, second.Opacity);
        f.Host.Clock.Advance(TimeSpan.FromMilliseconds(180));
        Assert.True(Done(first));
        Assert.Same(third, f.Navigation.CurrentPage);
        Assert.False(queued.IsCompleted);
        f.Host.Clock.Advance(TimeSpan.FromMilliseconds(180));
        Assert.True(Done(queued));
        Assert.Equal(.65f, second.Opacity);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void CancellationBeforeCommitDoesNothingAndAfterCommitSettlesWithoutRollback()
    {
        using var f = new Fixture(false);
        using var next = NewPage("next");
        using var token = new CancellationTokenSource();
        token.Cancel();
        Assert.True(f.Navigation.PushAsync(next, cancellationToken: token.Token).IsCanceled);
        Assert.Null(next.Parent);
        using var visualToken = new CancellationTokenSource();
        var operation = f.Navigation.PushAsync(next, cancellationToken: visualToken.Token);
        Assert.Same(next, f.Navigation.CurrentPage);
        visualToken.Cancel();
        f.Host.Clock.Advance(TimeSpan.Zero);
        Assert.True(Done(operation));
        Assert.Equal(PageLifecycleState.Appeared, next.LifecycleState);
        Assert.Same(next, f.Navigation.CurrentPage);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void VetoAndThrowBeforeCommitKeepStackAndIncomingPageDetached()
    {
        using var f = new Fixture(false);
        using var next = NewPage("next");
        EventHandler<NavigationChangingEventArgs> veto = (_, e) => e.Cancel = true;
        f.Navigation.Navigating += veto;
        Assert.False(Done(f.Navigation.PushAsync(next, false)));
        f.Navigation.Navigating -= veto;
        var expected = new InvalidOperationException("callback");
        f.Navigation.Navigating += (_, _) => throw expected;
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => Done(f.Navigation.PushAsync(next, false))));
        Assert.Null(next.Parent);
        Assert.Same(f.Root, f.Navigation.CurrentPage);
        Assert.False(f.Navigation.IsNavigating);
    }

    [Fact]
    public void ThrowAfterCommitStillSettlesAndSubsequentNavigationWorks()
    {
        using var f = new Fixture(false);
        using var next = NewPage("next");
        var expected = new InvalidOperationException("appearing");
        next.Appearing += (_, _) => throw expected;
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => Done(f.Navigation.PushAsync(next, false))));
        Assert.Same(next, f.Navigation.CurrentPage);
        Assert.Equal(PageLifecycleState.Appeared, next.LifecycleState);
        Assert.Same(next, Done(f.Navigation.PopAsync(false)));
        Assert.Same(f.Root, f.Navigation.CurrentPage);
    }

    [Fact]
    public void ExternalReparentDoesNotLetOldHostReclaimOrDisposePage()
    {
        using var f = new Fixture(false);
        var next = NewPage("next");
        Assert.True(Done(f.Navigation.PushAsync(next, false)));
        using var foreign = new Panel();
        foreign.Controls.Add(next);
        Assert.Same(f.Root, f.Navigation.CurrentPage);
        Assert.Equal(PageLifecycleState.Detached, next.LifecycleState);
        f.Navigation.Dispose();
        Assert.NotEqual(PageLifecycleState.Disposed, next.LifecycleState);
        Assert.Same(foreign, next.Parent);
    }

    [Fact]
    public void DisposeDuringAppearanceCannotPublishStaleAppearedAndReleasesQueue()
    {
        using var f = new Fixture(false);
        var next = NewPage("next");
        int appeared = 0;
        next.Appeared += (_, _) => appeared++;
        var first = f.Navigation.PushAsync(next);
        using var queuedPage = NewPage("queued");
        var queued = f.Navigation.PushAsync(queuedPage);
        f.Navigation.Dispose();
        f.Host.Clock.Advance(TimeSpan.Zero);
        Assert.True(first.IsCompleted);
        Assert.True(queued.IsCanceled);
        Assert.Equal(0, appeared);
        Assert.Equal(PageLifecycleState.Disposed, next.LifecycleState);
        Assert.Equal(PageLifecycleState.Disposed, f.Root.LifecycleState);
        Assert.Null(queuedPage.Parent);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void HiddenFromAppearingNeverPublishesObsoleteAppeared()
    {
        using var f = new Fixture(false);
        using var next = NewPage("next");
        int appeared = 0, disappeared = 0;
        next.Appearing += (_, _) => next.Hide();
        next.Appeared += (_, _) => appeared++;
        next.Disappeared += (_, _) => disappeared++;
        Done(f.Navigation.PushAsync(next, false));
        Assert.Equal(0, appeared);
        Assert.Equal(1, disappeared);
        Assert.Equal(PageLifecycleState.Attached, next.LifecycleState);
    }

    [Fact]
    public void BackUsesScopedBindingsAndBusyBackIsConsumed()
    {
        using var f = new Fixture(false);
        using var next = NewPage("next");
        f.Root.Content!.Select();
        var push = f.Navigation.PushAsync(next);
        Assert.True(f.Window!.Input.PressKey(Keys.BrowserBack));
        Assert.Same(next, f.Navigation.CurrentPage);
        f.Host.Clock.Advance(TimeSpan.FromMilliseconds(180));
        Assert.True(Done(push));
        Assert.True(f.Window.Input.PressKey(Keys.Alt | Keys.Left));
        f.Host.Clock.Advance(TimeSpan.FromMilliseconds(180));
        Assert.Same(f.Root, f.Navigation.CurrentPage);
        Assert.False(f.Window.IsClosed);
        Assert.False(f.Window.Input.PressKey(Keys.BrowserBack));
    }

    [Fact]
    public void DuplicateAndForeignPagesAreRejectedWithoutChangingFocus()
    {
        using var f = new Fixture(false);
        f.Root.Content!.Select();
        using var foreign = new Panel();
        var next = foreign.Controls.Add(NewPage("foreign"));
        Assert.Throws<InvalidOperationException>(() => { _ = f.Navigation.PushAsync(next); });
        Assert.Throws<ArgumentException>(() => { _ = f.Navigation.PushAsync(f.Root); });
        Assert.Same(f.Root.Content, f.Focused);
        Assert.Same(f.Root, f.Navigation.CurrentPage);
    }

    [Fact]
    public void RepeatedPushPopDoesNotRetainPresentationFramesOrSemanticChildren()
    {
        using var f = new Fixture(false);
        for (int i = 0; i < 200; i++)
        {
            using var page = NewPage(i.ToString());
            Assert.True(Done(f.Navigation.PushAsync(page, false)));
            Assert.Same(page, Done(f.Navigation.PopAsync(false)));
        }
        var navPeer = f.Navigation.AccessibilityObject;
        Assert.Equal(1, navPeer.GetChildCount());
        var hostPeer = navPeer.GetChild(0)!;
        Assert.Equal(1, hostPeer.GetChildCount());
        Assert.Same(f.Root.AccessibilityObject, hostPeer.GetChild(0)!.GetChild(0));
        Assert.Same(f.Root.Content!.AccessibilityObject, f.Root.AccessibilityObject.GetChild(0));
        Assert.Single(f.Navigation.NavigationStack);
    }

    [Fact]
    public void ContentReplacementUsesNormalCollectionAndNewerCallbackWins()
    {
        using var host = ModernFormsTestHost.Create();
        using var page = NewPage("first");
        var previous = page.Content!;
        bool disposed = false;
        previous.Disposed += (_, _) => disposed = true;
        using var stale = new Label();
        var replacement = new Button();
        page.ControlRemoved += (_, _) => page.Content = replacement;
        page.Content = stale;
        Assert.Same(replacement, page.Content);
        Assert.Null(previous.Parent);
        Assert.False(disposed);
        Assert.Null(stale.Parent);
        Assert.Throws<InvalidOperationException>(() => page.Controls.Add(new Label()));
        previous.Dispose();
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1.5d)]
    [InlineData(2d)]
    public void ContentHonorsLogicalPaddingMarginAndViewport(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(400, 250, scale));
        var content = new Label { Margin = new Padding(3, 5, 7, 9) };
        var page = new ContentPage { Content = content, Padding = new Padding(11, 13, 17, 19) };
        var presenter = new PageHost { Page = page, Padding = new Padding(2) };
        var window = host.Show(presenter);
        window.LayoutUntilStable();
        Assert.Equal(new Rectangle(2, 2, 396, 246), page.Parent!.Bounds);
        Assert.Equal(new Rectangle(0, 0, 396, 246), page.Bounds);
        Assert.Equal(new Rectangle(14, 18, 358, 200), content.Bounds);
    }

    [Fact]
    public void HostDisposalDoesNotDisposeSiblingTransferredByCallback()
    {
        using var f = new Fixture(false);
        using var foreign = new Panel();
        var next = NewPage("next");
        Done(f.Navigation.PushAsync(next, false));
        next.Disappearing += (_, _) => foreign.Controls.Add(f.Root);
        f.Navigation.Dispose();
        Assert.NotEqual(PageLifecycleState.Disposed, f.Root.LifecycleState);
        Assert.Same(foreign, f.Root.Parent);
        Assert.Equal(PageLifecycleState.Disposed, next.LifecycleState);
    }

    private static ContentPage NewPage(string text) => new() { Title = text, Content = new TextBox { Text = text } };
    private static T Done<T>(Task<T> operation)
    {
        Assert.True(operation.IsCompleted, "A nonanimated or clock-completed operation must not wait for a thread-pool continuation.");
        return operation.GetAwaiter().GetResult();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly bool neutral;
        private SkiaControlSurface? surface;
        internal ModernFormsTestHost Host { get; } = ModernFormsTestHost.Create();
        internal ContentPage Root { get; } = NewPage("root");
        internal NavigationPage Navigation { get; }
        internal PageHost Presenter { get; }
        internal Panel RootContainer { get; } = new() { TabStop = false, Width = 400, Height = 300 };
        internal TestWindowHost? Window { get; private set; }
        internal Control? Focused => surface?.SelectedControl ?? Window?.FocusedControl;
        internal Fixture(bool neutral, bool mount = true)
        {
            this.neutral = neutral;
            Navigation = new(Root);
            Presenter = RootContainer.Controls.Add(new PageHost { Page = Navigation, Dock = DockStyle.Fill });
            if (mount) Mount();
        }
        internal void Mount()
        {
            if (neutral) { surface = new(RootContainer); surface.Resize(400, 300); }
            else Window = Host.Show(RootContainer);
        }
        public void Dispose()
        {
            surface?.Dispose();
            Host.Dispose();
            RootContainer.Dispose();
        }
    }
}
