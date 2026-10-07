using ModernFormsNext.WindowKit.Backend.Android.Services;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidMessageDialogTests
{
    [Theory]
    [InlineData(false, false, false, PlatformServiceStatus.Unavailable)]
    [InlineData(false, true, false, PlatformServiceStatus.Busy)]
    [InlineData(true, true, true, PlatformServiceStatus.Shutdown)]
    public void NoActivityAndTerminalStatesNeverLaunch(bool shutdown, bool busy, bool host, PlatformServiceStatus expected)
        => Assert.Equal(expected, Assert.Throws<PlatformServiceException>(() =>
            AndroidMessageDialogPlan.RequireHost(host ? new object() : null, shutdown, busy)).Status);

    [Theory]
    [InlineData(MessageBoxButtons.OK, -1, -1, -1)]
    [InlineData(MessageBoxButtons.OKCancel, 1, -1, 1)]
    [InlineData(MessageBoxButtons.YesNo, 1, -1, -1)]
    [InlineData(MessageBoxButtons.YesNoCancel, 1, 2, 2)]
    [InlineData(MessageBoxButtons.RetryCancel, 1, -1, 1)]
    public void NativeRolesAndBackUseSemanticButtonIndices(MessageBoxButtons buttons, int negative, int neutral, int cancel)
    {
        var plan = AndroidMessageDialogPlan.Create(new("", "", buttons));
        Assert.Equal(0, plan.Positive); Assert.Equal(negative, plan.Negative);
        Assert.Equal(neutral, plan.Neutral); Assert.Equal(cancel, plan.Cancel);
    }

    [Theory]
    [InlineData(MessageBoxIcon.None, 0)][InlineData(MessageBoxIcon.Information, 1)]
    [InlineData(MessageBoxIcon.Warning, 2)][InlineData(MessageBoxIcon.Error, 2)][InlineData(MessageBoxIcon.Question, 0)]
    public void IconsUseOnlyAvailableNativeResources(MessageBoxIcon icon, int expected)
        => Assert.Equal(expected, (int)AndroidMessageDialogPlan.Create(new("", "", icon: icon)).Icon);

    [Fact]
    public async Task RetiredActivityACannotCompleteDialogBOrWrongGeneration()
    {
        var state = new NativeRequestCoordinator<object, int>();
        var a = new object(); var b = new object();
        int dismissA = 0, dismissB = 0;
        var first = state.Begin(a, default, () => dismissA++, 41);
        Assert.False(state.Complete(a, first.Code, 0, 42));
        state.Destroy(a);
        Assert.Equal(PlatformServiceStatus.HostLost, (await Assert.ThrowsAsync<PlatformServiceException>(() => first.Task)).Status);
        Assert.Equal(1, dismissA);
        var second = state.Begin(b, default, () => dismissB++, 42);
        Assert.False(state.Complete(a, first.Code, 0, 41));
        Assert.False(state.Complete(b, first.Code, 0, 42));
        state.FailStart(first.Code, new Exception("stale dismissal"));
        state.Destroy(a);
        Assert.False(second.Task.IsCompleted);
        Assert.True(state.Complete(b, second.Code, 1, 42));
        Assert.Equal(1, await second.Task);
        Assert.Equal(1, dismissB);
        Assert.False(state.Busy);
    }

    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task ClickCancelRaceRetiresExactlyOnce(bool cancelFirst)
    {
        var state = new NativeRequestCoordinator<object, int>(); var host = new object();
        int dismissed = 0;
        using var cancellation = new CancellationTokenSource();
        var pending = state.Begin(host, default, () => dismissed++, 1);
        cancellation.Cancel();
        if (cancelFirst)
        {
            Assert.True(state.Cancel(host, pending.Code, cancellation.Token, 1));
            Assert.False(state.Complete(host, pending.Code, 0, 1));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.Task);
        }
        else
        {
            Assert.True(state.Complete(host, pending.Code, 0, 1));
            Assert.False(state.Cancel(host, pending.Code, cancellation.Token, 1));
            Assert.Equal(0, await pending.Task);
        }
        Assert.Equal(1, dismissed);
        var next = state.Begin(host, default);
        state.Complete(host, next.Code, 0);
        await next.Task;
    }

    [Fact]
    public async Task SharedSlotRejectsOverlapWithoutEnqueuingAndShutdownCleansBeforeTask()
    {
        var state = new NativeRequestCoordinator<object, int>(); var host = new object();
        int dismissed = 0;
        var dialog = state.Begin(host, default, () => dismissed++);
        Assert.Equal(PlatformServiceStatus.Busy, Assert.Throws<PlatformServiceException>(() => state.Begin(host, default)).Status);
        state.Shutdown();
        Assert.Equal(1, dismissed);
        Assert.Equal(PlatformServiceStatus.Shutdown, (await Assert.ThrowsAsync<PlatformServiceException>(() => dialog.Task)).Status);
        state.Shutdown();
        Assert.Equal(1, dismissed);
        Assert.False(state.Busy);
        Assert.Equal(PlatformServiceStatus.Shutdown, Assert.Throws<PlatformServiceException>(() => state.Begin(host, default)).Status);
    }

    [Fact]
    public async Task PresentationLossOrFailedShowCleansOnlyThatRequest()
    {
        var state = new NativeRequestCoordinator<object, int>(); var host = new object();
        int dismissed = 0;
        var first = state.Begin(host, default, () => dismissed++);
        state.FailStart(first.Code, new PlatformServiceException(PlatformServiceStatus.HostLost));
        Assert.Equal(PlatformServiceStatus.HostLost, (await Assert.ThrowsAsync<PlatformServiceException>(() => first.Task)).Status);
        Assert.Equal(1, dismissed);
        Assert.False(state.Busy);
        var second = state.Begin(host, default, () => throw new IOException("cleanup failed"));
        state.Shutdown();
        await Assert.ThrowsAsync<IOException>(() => second.Task);
        Assert.False(state.Busy);
    }
}
