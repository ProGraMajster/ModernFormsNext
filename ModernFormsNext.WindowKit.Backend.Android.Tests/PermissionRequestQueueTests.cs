using ModernFormsNext.WindowKit.Backend.Android.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class PermissionRequestQueueTests
{
    [Fact]
    public async Task RequestsAreSerialized()
    {
        var queue = new PermissionRequestQueue();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = false;

        var first = queue.EnqueueAsync(
            async _ =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task;
                return 1;
            },
            TimeSpan.FromSeconds(5),
            default);
        await firstEntered.Task;

        var second = queue.EnqueueAsync(
            _ =>
            {
                secondEntered = true;
                return Task.FromResult(2);
            },
            TimeSpan.FromSeconds(5),
            default);

        await Task.Delay(30);
        Assert.False(secondEntered);

        releaseFirst.SetResult();
        Assert.Equal(1, await first);
        Assert.Equal(2, await second);
        Assert.True(secondEntered);
    }

    [Fact]
    public async Task CallerCancellationDoesNotReleaseGateBeforeNativeOperationEnds()
    {
        var queue = new PermissionRequestQueue();
        var nativeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nativeFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var secondEntered = false;

        var canceledCaller = queue.EnqueueAsync(
            async _ =>
            {
                nativeEntered.SetResult();
                await nativeFinished.Task;
                return 1;
            },
            TimeSpan.FromSeconds(5),
            cancellation.Token);
        await nativeEntered.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledCaller);

        var second = queue.EnqueueAsync(
            _ =>
            {
                secondEntered = true;
                return Task.FromResult(2);
            },
            TimeSpan.FromSeconds(5),
            default);
        await Task.Delay(30);
        Assert.False(secondEntered);

        nativeFinished.SetResult();
        Assert.Equal(2, await second);
    }

    [Fact]
    public async Task MissingActivityFailurePropagatesAndNextRequestRuns()
    {
        var queue = new PermissionRequestQueue();

        var missingActivity = queue.EnqueueAsync<int>(
            _ => throw new InvalidOperationException("no active Activity"),
            TimeSpan.FromSeconds(5),
            default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => missingActivity);
        Assert.Contains("Activity", exception.Message);

        var next = queue.EnqueueAsync(
            _ => Task.FromResult(42),
            TimeSpan.FromSeconds(5),
            default);
        Assert.Equal(42, await next);
    }

    [Fact]
    public async Task ActivityLossFailurePropagates()
    {
        var queue = new PermissionRequestQueue();

        var task = queue.EnqueueAsync<int>(
            _ => Task.FromException<int>(new InvalidOperationException("Activity was destroyed")),
            TimeSpan.FromSeconds(5),
            default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains("destroyed", exception.Message);
    }

    [Fact]
    public async Task NativeOperationTimeoutDoesNotHangQueue()
    {
        var queue = new PermissionRequestQueue();

        var task = queue.EnqueueAsync<int>(
            async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return 0;
            },
            TimeSpan.FromMilliseconds(30),
            default);

        await Assert.ThrowsAsync<TimeoutException>(() => task);
        Assert.Equal(
            7,
            await queue.EnqueueAsync(
                _ => Task.FromResult(7),
                TimeSpan.FromSeconds(5),
                default));
    }
}
