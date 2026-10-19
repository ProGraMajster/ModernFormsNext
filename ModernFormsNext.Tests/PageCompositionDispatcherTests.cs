using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class PageCompositionDispatcherTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedDispatcherFaultsFlyoutWithoutOffThreadUiCleanup(bool rejectAccessCheck)
    {
        int uiThread = Environment.CurrentManagedThreadId;
        var clock = new ManualAnimationClock();
        var ticks = new ManualAnimationTickSource();
        var dispatcher = new RejectingDispatcher(uiThread);
        using var scheduler = new AnimationScheduler(clock, dispatcher, ticks, new AnimationPolicy());
        using var scope = AnimationScheduler.PushDefaultForTesting(scheduler);
        var menu = new ContentPage { Content = new TextBox(), Opacity = .7f };
        var detail = new ContentPage { Content = new TextBox() };
        var flyout = new FlyoutPage { DisplayMode = FlyoutDisplayMode.Overlay, Flyout = menu, Detail = detail };
        using var presenter = new PageHost { Page = flyout };
        using var surface = new SkiaControlSurface(presenter);
        surface.Resize(400, 300);
        detail.Content.Select();
        using var cancellation = new CancellationTokenSource();
        var opening = flyout.SetFlyoutOpenAsync(true, cancellationToken: cancellation.Token);
        clock.Advance(TimeSpan.FromMilliseconds(90)); ticks.Fire();
        Assert.False(opening.IsCompleted);
        Assert.Equal(PageLifecycleState.Appearing, menu.LifecycleState);
        var frame = menu.Parent!;
        float opacity = frame.Opacity;
        Assert.InRange(opacity, .01f, .99f);
        int offThreadUi = 0;
        void Observe() { if (Environment.CurrentManagedThreadId != uiThread) offThreadUi++; }
        frame.Invalidated += (_, _) => Observe();
        menu.LifecycleStateChanged += (_, _) => Observe();
        flyout.FlyoutOpenChanged += (_, _) => Observe();
        flyout.ToggleFlyoutCommand.CanExecuteChanged += (_, _) => Observe();
        flyout.CloseFlyoutCommand.CanExecuteChanged += (_, _) => Observe();
        dispatcher.Reject = true; dispatcher.RejectAccessCheck = rejectAccessCheck;
        Exception? workerFailure = null;
        var worker = new Thread(() => { try { cancellation.Cancel(); } catch (Exception error) { workerFailure = error; } });
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        try
        {
            Assert.Null(workerFailure);
            Assert.True(opening.IsFaulted);
            Assert.IsType<ObjectDisposedException>(opening.Exception!.GetBaseException());
            Assert.Equal(0, offThreadUi);
            Assert.Equal(opacity, frame.Opacity);
            Assert.True(flyout.IsFlyoutOpen);
            Assert.Same(menu, flyout.Flyout);
            Assert.Equal(.7f, menu.Opacity);
            Assert.Equal(0, scheduler.GetDiagnostics().ActiveAnimationCount);
        }
        finally { dispatcher.Reject = false; }
        Assert.True(flyout.TrySetFlyoutOpen(false));
        Assert.Same(detail.Content, surface.SelectedControl);
        Assert.Equal(PageLifecycleState.Attached, menu.LifecycleState);
    }

    private sealed class RejectingDispatcher(int uiThread) : IAnimationDispatcher
    {
        internal bool Reject, RejectAccessCheck;
        public bool CheckAccess()
        {
            if (Reject && RejectAccessCheck) throw new ObjectDisposedException("test composition dispatcher");
            return !Reject && Environment.CurrentManagedThreadId == uiThread;
        }
        public void Post(Action action)
        {
            if (Reject) throw new ObjectDisposedException("test composition dispatcher");
            action();
        }
    }
}
