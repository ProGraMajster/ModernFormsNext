using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class PageNavigationDispatcherTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedDispatcherFaultsActiveAndQueuedTasksWithoutOffThreadUiCleanup(bool rejectAccessCheck)
    {
        int uiThread = Environment.CurrentManagedThreadId;
        var clock = new ManualAnimationClock();
        var ticks = new ManualAnimationTickSource();
        var dispatcher = new RejectingDispatcher(uiThread);
        using var scheduler = new AnimationScheduler(clock, dispatcher, ticks, new AnimationPolicy());
        using var scope = AnimationScheduler.PushDefaultForTesting(scheduler);
        var root = new ContentPage { Content = new TextBox() };
        var navigation = new NavigationPage(root);
        using var presenter = new PageHost { Page = navigation };
        using var surface = new SkiaControlSurface(presenter);
        surface.Resize(400, 300);
        using var next = new ContentPage { Content = new TextBox() };
        using var queuedPage = new ContentPage();
        using var cancellation = new CancellationTokenSource();
        root.Content.Select();
        var active = navigation.PushAsync(next, cancellationToken: cancellation.Token);
        var queued = navigation.PushAsync(queuedPage);
        clock.Advance(TimeSpan.FromMilliseconds(90));
        ticks.Fire();
        Assert.False(active.IsCompleted);
        Assert.Equal(PageLifecycleState.Appearing, next.LifecycleState);
        var frame = next.Parent!;
        float opacity = frame.Opacity;
        Assert.InRange(opacity, .01f, .99f);
        int offThreadUi = 0;
        void Observe() { if (Environment.CurrentManagedThreadId != uiThread) offThreadUi++; }
        frame.Invalidated += (_, _) => Observe();
        next.LifecycleStateChanged += (_, _) => Observe();
        navigation.StackChanged += (_, _) => Observe();
        navigation.Navigated += (_, _) => Observe();
        navigation.BackCommand.CanExecuteChanged += (_, _) => Observe();
        dispatcher.Reject = true;
        dispatcher.RejectAccessCheck = rejectAccessCheck;
        Exception? workerFailure = null;
        var worker = new Thread(() => { try { cancellation.Cancel(); } catch (Exception error) { workerFailure = error; } });
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        try
        {
            Assert.Null(workerFailure);
            Assert.True(active.IsFaulted);
            Assert.True(queued.IsFaulted);
            Assert.IsType<ObjectDisposedException>(active.Exception!.GetBaseException());
            Assert.IsType<ObjectDisposedException>(queued.Exception!.GetBaseException());
            Assert.Equal(0, offThreadUi);
            Assert.Equal(opacity, frame.Opacity);
            Assert.Same(next, navigation.CurrentPage);
            Assert.Null(queuedPage.Parent);
            Assert.Equal(0, scheduler.GetDiagnostics().ActiveAnimationCount);
        }
        finally { dispatcher.Reject = false; }
        var recovered = navigation.PopAsync(false);
        Assert.True(recovered.IsCompletedSuccessfully);
        Assert.Same(next, Completed(recovered));
        Assert.Same(root, navigation.CurrentPage);
    }

    private static T Completed<T>(Task<T> task) => task.GetAwaiter().GetResult();

    private sealed class RejectingDispatcher(int uiThread) : IAnimationDispatcher
    {
        internal bool Reject;
        internal bool RejectAccessCheck;
        public bool CheckAccess()
        {
            if (Reject && RejectAccessCheck) throw new ObjectDisposedException("test navigation dispatcher");
            return !Reject && Environment.CurrentManagedThreadId == uiThread;
        }
        public void Post(Action action)
        {
            if (Reject) throw new ObjectDisposedException("test navigation dispatcher");
            action();
        }
    }
}
