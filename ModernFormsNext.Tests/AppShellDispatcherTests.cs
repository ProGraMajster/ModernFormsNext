using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class AppShellDispatcherTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedFactoryContinuationReleasesBookkeepingWithoutOffThreadUi(bool rejectAccessCheck)
    {
        int uiThread = Environment.CurrentManagedThreadId;
        var dispatcher = new RejectingShellDispatcher(uiThread);
        using var scheduler = new AnimationScheduler(new ManualAnimationClock(), dispatcher,
            new ManualAnimationTickSource(), new AnimationPolicy());
        using var scope = AnimationScheduler.PushDefaultForTesting(scheduler);
        using var shell = new AppShell();
        using var late = new ContentPage { Content = new TextBox() };
        var source = new TaskCompletionSource<Page>();
        shell.Routes.Register("late", (_, _) => source.Task);
        var visit = shell.GoToAsync("/late", animated: false);
        int offThread = 0;
        void Observe() { if (Environment.CurrentManagedThreadId != uiThread) offThread++; }
        shell.CurrentRouteChanged += (_, _) => Observe();
        shell.NavigateCommand.CanExecuteChanged += (_, _) => Observe();
        shell.BackCommand.CanExecuteChanged += (_, _) => Observe();
        late.LifecycleStateChanged += (_, _) => Observe();
        dispatcher.Reject = true; dispatcher.RejectAccessCheck = rejectAccessCheck;
        var worker = new Thread(() => source.SetResult(late));
        worker.Start(); Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        try
        {
            Assert.True(visit.IsFaulted);
            Assert.IsType<ObjectDisposedException>(visit.Exception!.GetBaseException());
            Assert.Equal(0, shell.PendingRequestCount);
            Assert.Equal(0, offThread);
            Assert.Null(late.Parent);
            Assert.Null(shell.CurrentPage);
            Assert.NotEqual(PageLifecycleState.Disposed, late.LifecycleState);
        }
        finally { dispatcher.Reject = false; }
    }

    private sealed class RejectingShellDispatcher(int uiThread) : IAnimationDispatcher
    {
        internal bool Reject, RejectAccessCheck;
        public bool CheckAccess()
        {
            if (Reject && RejectAccessCheck) throw new ObjectDisposedException("test shell dispatcher");
            return !Reject && Environment.CurrentManagedThreadId == uiThread;
        }
        public void Post(Action action)
        {
            if (Reject) throw new ObjectDisposedException("test shell dispatcher");
            action();
        }
    }
}
