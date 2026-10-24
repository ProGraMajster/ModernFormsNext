using System.ComponentModel;
using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed partial class PageNavigationTests
{
    [Fact]
    public void NonanimatedAndAnimatedAppearanceEachPublishExactlyOnce()
    {
        using var f = new Fixture(false);
        using var first = NewPage("first");
        using var second = NewPage("second");
        int firstAppeared = 0, secondAppeared = 0;
        first.Appeared += (_, _) => firstAppeared++;
        second.Appeared += (_, _) => secondAppeared++;
        Assert.True(Done(f.Navigation.PushAsync(first, false)));
        var animated = f.Navigation.PushAsync(second);
        Assert.Equal(0, secondAppeared);
        f.Host.Clock.Advance(TimeSpan.FromMilliseconds(180));
        Assert.True(Done(animated));
        Assert.Equal(1, firstAppeared);
        Assert.Equal(1, secondAppeared);
    }

    [Fact]
    public void ReentrantReplacementPreservesNewestPageAndFinishesOldDisappearance()
    {
        using var host = ModernFormsTestHost.Create();
        var first = NewPage("first");
        using var second = NewPage("second");
        using var latest = NewPage("latest");
        var presenter = new PageHost { Page = first };
        host.Show(presenter);
        int disappeared = 0;
        first.Disappearing += (_, _) => presenter.Page = latest;
        first.Disappeared += (_, _) => disappeared++;
        Assert.False(presenter.TrySetPage(second));
        Assert.Same(latest, presenter.Page);
        Assert.Equal(PageLifecycleState.Appeared, latest.LifecycleState);
        Assert.Null(second.Parent);
        Assert.Null(first.Parent);
        Assert.Equal(1, disappeared);
        first.Dispose();
    }

    [Fact]
    public void ReentrantReplacementReturningToPreviousDoesNotReleaseCurrentPage()
    {
        using var host = ModernFormsTestHost.Create();
        var first = NewPage("first");
        using var second = NewPage("second");
        var presenter = new PageHost { Page = first };
        host.Show(presenter);
        bool redirect = true;
        first.Disappearing += (_, _) => { if (redirect) { redirect = false; presenter.Page = first; } };
        Assert.False(presenter.TrySetPage(second));
        Assert.Same(first, presenter.Page);
        Assert.Equal(PageLifecycleState.Appeared, first.LifecycleState);
        Assert.NotNull(first.Parent);
        Assert.Null(second.Parent);
    }

    [Fact]
    public void ReparentedAncestorRetiresOldQueueWithoutRollingBackCommittedHistory()
    {
        using var f = new Fixture(false);
        var other = new Panel();
        f.Host.Show(other);
        using var current = NewPage("current");
        using var queued = NewPage("queued");
        var first = f.Navigation.PushAsync(current);
        var waiting = f.Navigation.PushAsync(queued);
        other.Controls.Add(f.RootContainer);
        f.Host.Clock.Advance(TimeSpan.Zero);
        Assert.True(first.IsCompleted);
        Assert.False(Done(first));
        Assert.True(waiting.IsCanceled);
        Assert.Same(current, f.Navigation.CurrentPage);
        Assert.Null(queued.Parent);
        Assert.Equal(PageLifecycleState.Appeared, current.LifecycleState);
        Assert.Same(current, Done(f.Navigation.PopAsync(false)));
    }

    [Fact]
    public void QueuedCancellationIsObservedBeforeAdmissionAndDoesNotBlockLaterWork()
    {
        using var f = new Fixture(false);
        using var firstPage = NewPage("first");
        using var canceledPage = NewPage("canceled");
        using var lastPage = NewPage("last");
        using var token = new CancellationTokenSource();
        var first = f.Navigation.PushAsync(firstPage);
        var canceled = f.Navigation.PushAsync(canceledPage, false, token.Token);
        var last = f.Navigation.PushAsync(lastPage, false);
        token.Cancel();
        f.Host.Clock.Advance(TimeSpan.FromMilliseconds(180));
        Assert.True(Done(first));
        Assert.True(canceled.IsCanceled);
        Assert.True(Done(last));
        Assert.Null(canceledPage.Parent);
        Assert.Same(lastPage, f.Navigation.CurrentPage);
    }

    [Fact]
    public void QueueHasBoundedPendingWorkAndDisposalReleasesAllRequests()
    {
        using var f = new Fixture(false);
        var pages = Enumerable.Range(0, 130).Select(i => NewPage(i.ToString())).ToArray();
        var tasks = new List<Task<bool>>();
        try
        {
            tasks.Add(f.Navigation.PushAsync(pages[0]));
            for (int i = 1; i <= 128; i++) tasks.Add(f.Navigation.PushAsync(pages[i]));
            Assert.Throws<InvalidOperationException>(() => { _ = f.Navigation.PushAsync(pages[129]); });
            f.Navigation.Dispose();
            f.Host.Clock.Advance(TimeSpan.Zero);
            Assert.All(tasks, task => Assert.True(task.IsCompleted));
            Assert.All(tasks.Skip(1), task => Assert.True(task.IsCanceled));
            Assert.All(pages.Skip(1), page => Assert.Null(page.Parent));
        }
        finally { foreach (var page in pages) page.Dispose(); }
    }

    [Fact]
    public void PageFinalizerPathDoesNotWalkTreeOrPublishEventsFromAnotherThread()
    {
        using var host = ModernFormsTestHost.Create();
        var page = new FinalizerProbe { Content = new TextBox() };
        var presenter = new PageHost { Page = page };
        host.Show(presenter);
        int events = 0;
        page.LifecycleStateChanged += (_, _) => events++;
        page.Content.Disposed += (_, _) => events++;
        var parent = page.Parent;
        Exception? failure = null;
        var finalizerThread = new Thread(() => { try { page.FinalizeForTest(); } catch (Exception error) { failure = error; } });
        finalizerThread.Start();
        finalizerThread.Join();
        Assert.Null(failure);
        Assert.Equal(0, events);
        Assert.Same(parent, page.Parent);
        Assert.Equal(PageLifecycleState.Appeared, page.LifecycleState);
        page.Dispose();
        Assert.Equal(PageLifecycleState.Disposed, page.LifecycleState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DesignSiteKeepsAppearanceInertAndReducedMotionUsesExistingPolicy(bool ancestor)
    {
        using var f = new Fixture(false, mount: false);
        Control designOwner = ancestor ? f.RootContainer : f.Presenter;
        designOwner.Site = new DesignSite(designOwner);
        f.Mount();
        Assert.Equal(PageLifecycleState.Attached, f.Navigation.LifecycleState);
        Assert.Equal(PageLifecycleState.Attached, f.Root.LifecycleState);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        Assert.True(f.Navigation.Parent!.Visible);
        Assert.True(f.Root.Parent!.Visible);
        designOwner.Site = null;
        f.Presenter.PerformLayout();
        AnimationScheduler.Default.Policy.ReducedMotion = true;
        using var next = NewPage("next");
        Assert.True(Done(f.Navigation.PushAsync(next)));
        Assert.Equal(PageLifecycleState.Appeared, next.LifecycleState);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void WindowDesignSiteKeepsHostedPagesInert()
    {
        using var host = ModernFormsTestHost.Create();
        var root = NewPage("root");
        var navigation = new NavigationPage(root);
        var form = new Form();
        form.Site = new DesignSite(form);
        form.Controls.Add(new PageHost { Page = navigation });
        host.Show(form);
        Assert.Equal(PageLifecycleState.Attached, navigation.LifecycleState);
        Assert.Equal(PageLifecycleState.Attached, root.LifecycleState);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        Assert.True(navigation.Parent!.Visible);
        Assert.True(root.Parent!.Visible);
    }

    [Fact]
    public void PageDesignSiteKeepsItsOwnedPresentationInert()
    {
        using var f = new Fixture(false, mount: false);
        f.Navigation.Site = new DesignSite(f.Navigation);
        f.Mount();
        Assert.Equal(PageLifecycleState.Attached, f.Navigation.LifecycleState);
        Assert.Equal(PageLifecycleState.Attached, f.Root.LifecycleState);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
        Assert.True(f.Navigation.Parent!.Visible);
        Assert.True(f.Root.Parent!.Visible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationOrThrowDuringAttachDetachesReservationAndRestoresOldFocus(bool throwing)
    {
        using var f = new Fixture(false);
        using var next = NewPage("next");
        using var token = new CancellationTokenSource();
        f.Root.Content!.Select();
        next.LifecycleStateChanged += (_, _) =>
        {
            if (next.LifecycleState != PageLifecycleState.Attached) return;
            if (throwing) throw new InvalidOperationException("attach");
            token.Cancel();
        };
        var operation = f.Navigation.PushAsync(next, false, token.Token);
        Assert.True(operation.IsCompleted);
        if (throwing) Assert.Throws<InvalidOperationException>(() => Done(operation));
        else Assert.True(operation.IsCanceled);
        Assert.Null(next.Parent);
        Assert.Equal(PageLifecycleState.Detached, next.LifecycleState);
        Assert.Same(f.Root, f.Navigation.CurrentPage);
        Assert.Same(f.Root.Content, f.Focused);
    }

    [Fact]
    public void HostReplacementCallbackFailureStillTransfersThePreviousPage()
    {
        using var f = new Fixture(false);
        using var presenter = new PageHost();
        using var old = NewPage("old");
        using var next = NewPage("next");
        f.RootContainer.Controls.Add(presenter);
        presenter.Page = old;
        next.Appearing += (_, _) => throw new InvalidOperationException("appearing");
        Assert.Throws<InvalidOperationException>(() => presenter.Page = next);
        Assert.Same(next, presenter.Page);
        Assert.Null(old.Parent);
        Assert.Equal(PageLifecycleState.Detached, old.LifecycleState);
        Assert.Equal(PageLifecycleState.Appeared, next.LifecycleState);
    }

    [Fact]
    public void ContentDoesNotStealReparentedCandidateAndRejectsCyclesBeforeRemovingOldChild()
    {
        using var host = ModernFormsTestHost.Create();
        using var page = NewPage("old");
        using var candidate = new Label();
        using var foreign = new Panel();
        var previous = page.Content;
        Assert.Throws<ArgumentException>(() => page.Content = page);
        Assert.Same(previous, page.Content);
        page.ControlRemoved += (_, _) => foreign.Controls.Add(candidate);
        page.Content = candidate;
        Assert.Null(page.Content);
        Assert.Same(foreign, candidate.Parent);
        previous!.Dispose();
    }

    [Fact]
    public void ThrowingVisibilityCallbacksStillSettleTheCommittedState()
    {
        using var f = new Fixture(false);
        EventHandler disappearing = (_, _) => throw new InvalidOperationException("disappearing");
        EventHandler appearing = (_, _) => throw new InvalidOperationException("appearing");
        f.Root.Disappearing += disappearing;
        Assert.Throws<InvalidOperationException>(() => f.Presenter.Hide());
        Assert.Equal(PageLifecycleState.Attached, f.Root.LifecycleState);
        f.Root.Appearing += appearing;
        Assert.Throws<InvalidOperationException>(() => f.Presenter.Show());
        Assert.Equal(PageLifecycleState.Appeared, f.Root.LifecycleState);
        f.Root.Disappearing -= disappearing;
        f.Root.Appearing -= appearing;
    }

    [Fact]
    public void BackgroundCancellationWaitsForTheCanonicalUiDispatcherBeforeSettling()
    {
        using var f = new Fixture(false);
        using var next = NewPage("next");
        using var token = new CancellationTokenSource();
        int uiThread = Environment.CurrentManagedThreadId;
        var callbacks = new List<int>();
        next.Appeared += (_, _) => callbacks.Add(Environment.CurrentManagedThreadId);
        var operation = f.Navigation.PushAsync(next, cancellationToken: token.Token);
        Exception? failure = null;
        var worker = new Thread(() => { try { token.Cancel(); } catch (Exception error) { failure = error; } });
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
        Assert.Empty(callbacks);
        Assert.False(operation.IsCompleted);
        f.Host.Clock.Advance(TimeSpan.Zero);
        Assert.True(Done(operation));
        Assert.Equal(new[] { uiThread }, callbacks);
        Assert.Equal(PageLifecycleState.Appeared, next.LifecycleState);
    }

    [Fact]
    public void RemovingTheOldRootDuringAdmissionReleasesTheUncommittedCandidate()
    {
        using var f = new Fixture(false);
        using var next = NewPage("next");
        next.LifecycleStateChanged += (_, _) =>
        {
            if (next.LifecycleState == PageLifecycleState.Attached) f.Root.Dispose();
        };
        Assert.False(Done(f.Navigation.PushAsync(next, false)));
        Assert.Empty(f.Navigation.NavigationStack);
        Assert.Null(next.Parent);
        Assert.Equal(PageLifecycleState.Detached, next.LifecycleState);
        Assert.True(Done(f.Navigation.PushAsync(next, false)));
        Assert.Same(next, f.Navigation.RootPage);
    }

    private sealed class FinalizerProbe : ContentPage { internal void FinalizeForTest() => Dispose(false); }
    private sealed class DesignSite(IComponent component) : ISite
    {
        public IComponent Component => component;
        public IContainer? Container => null;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type type) => null;
    }
}
