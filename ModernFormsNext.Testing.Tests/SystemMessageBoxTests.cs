using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform.Services;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class SystemMessageBoxTests
{
    [Theory]
    [InlineData(MessageBoxButtons.OK, DialogResult.OK)]
    [InlineData(MessageBoxButtons.OKCancel, DialogResult.OK)]
    [InlineData(MessageBoxButtons.OKCancel, DialogResult.Cancel)]
    [InlineData(MessageBoxButtons.YesNo, DialogResult.Yes)]
    [InlineData(MessageBoxButtons.YesNo, DialogResult.No)]
    [InlineData(MessageBoxButtons.YesNoCancel, DialogResult.Yes)]
    [InlineData(MessageBoxButtons.YesNoCancel, DialogResult.No)]
    [InlineData(MessageBoxButtons.YesNoCancel, DialogResult.Cancel)]
    [InlineData(MessageBoxButtons.RetryCancel, DialogResult.Retry)]
    [InlineData(MessageBoxButtons.RetryCancel, DialogResult.Cancel)]
    public async Task PublicFacadeReturnsExistingDialogResult(MessageBoxButtons buttons, DialogResult result)
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        host.Show(owner);
        var fake = host.Services.MessageDialogs;
        fake.NextResult = result;
        Assert.Equal(result, await SystemMessageBox.ShowAsync(owner, "Body", "Title", buttons));
        Assert.NotNull(fake.LastOwner);
        Assert.Equal(owner.PlatformHandle, fake.LastOwner.Handle);
        Assert.Equal("Body", fake.LastRequest!.Message);
        Assert.Equal("Title", fake.LastRequest.Title);
        Assert.Equal(buttons, fake.LastRequest.Buttons);
    }

    [Theory]
    [InlineData(MessageBoxIcon.None)][InlineData(MessageBoxIcon.Information)][InlineData(MessageBoxIcon.Warning)]
    [InlineData(MessageBoxIcon.Error)][InlineData(MessageBoxIcon.Question)]
    public async Task IconAndNoOwnerArePassedWithoutNativeTypes(MessageBoxIcon icon)
    {
        using var host = ModernFormsTestHost.Create();
        await SystemMessageBox.ShowAsync(null, "", "", icon: icon);
        Assert.Null(host.Services.MessageDialogs.LastOwner);
        Assert.Equal(icon, host.Services.MessageDialogs.LastRequest!.Icon);
    }

    [Fact]
    public async Task CancellationErrorsAndUnavailableAreDistinctFromUserCancel()
    {
        using var host = ModernFormsTestHost.Create();
        var fake = host.Services.MessageDialogs;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SystemMessageBox.ShowAsync(null, "", "", cancellationToken: new(true)));
        Assert.Null(fake.LastRequest);
        fake.NextStatus = PlatformServiceStatus.Unavailable;
        Assert.Equal(PlatformServiceStatus.Unavailable, (await Assert.ThrowsAsync<PlatformServiceException>(() =>
            SystemMessageBox.ShowAsync(null, "", ""))).Status);
        fake.NextException = new IOException("synthetic");
        await Assert.ThrowsAsync<IOException>(() => SystemMessageBox.ShowAsync(null, "", ""));
        fake.NextException = new OperationCanceledException();
        var canceled = SystemMessageBox.ShowAsync(null, "", "");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        Assert.True(canceled.IsCanceled);
        Assert.Equal(DialogResult.OK, await SystemMessageBox.ShowAsync(null, "", ""));
    }

    [Fact]
    public async Task HiddenAndClosedOwnersAreRejectedBeforeService()
    {
        using var host = ModernFormsTestHost.Create();
        using var owner = new Form();
        await Assert.ThrowsAsync<PlatformServiceException>(() => SystemMessageBox.ShowAsync(owner, "", ""));
        host.Show(owner); owner.Close();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => SystemMessageBox.ShowAsync(owner, "", ""));
        Assert.Null(host.Services.MessageDialogs.LastRequest);
    }

    [Fact]
    public async Task HostsRestoreServiceAndReleaseCapturedRequests()
    {
        var baseline = AvaloniaGlobals.GetService<IPlatformMessageDialogService>();
        TestMessageDialogService retired;
        using (var host = ModernFormsTestHost.Create())
        {
            retired = host.Services.MessageDialogs;
            await SystemMessageBox.ShowAsync(null, "synthetic", "");
            retired.NextResult = DialogResult.Cancel;
        }
        Assert.Null(retired.LastRequest);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => retired.ShowAsync(null, new("", "")));
        Assert.Same(baseline, AvaloniaGlobals.GetService<IPlatformMessageDialogService>());
        using var next = ModernFormsTestHost.Create();
        Assert.Null(next.Services.MessageDialogs.LastRequest);
        Assert.Equal(DialogResult.OK, await SystemMessageBox.ShowAsync(null, "", ""));
    }

    [Theory]
    [InlineData(MessageBoxButtons.OK, -1)]
    [InlineData(MessageBoxButtons.OK, 1)]
    [InlineData(MessageBoxButtons.OKCancel, 2)]
    [InlineData(MessageBoxButtons.YesNo, 2)]
    [InlineData(MessageBoxButtons.YesNoCancel, 3)]
    [InlineData(MessageBoxButtons.RetryCancel, 2)]
    public async Task InvalidBackendIndexNeverBecomesUserCancel(MessageBoxButtons buttons, int index)
    {
        using var host = ModernFormsTestHost.Create();
        // Use the existing scoped test hook without making it public or mutating the process registry.
        var push = typeof(AvaloniaGlobals).GetMethod("PushServiceForTesting",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        using var service = (IDisposable)push.MakeGenericMethod(typeof(IPlatformMessageDialogService))
            .Invoke(null, [new InvalidIndexService(index)])!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => SystemMessageBox.ShowAsync(null, "", "", buttons));
    }

    private sealed class InvalidIndexService(int index) : IPlatformMessageDialogService
    {
        public Task<int> ShowAsync(WindowKit.Platform.IWindowBaseImpl? owner, PlatformMessageDialogRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(index);
    }

    [Fact]
    public void InvalidNeutralRequestCannotReachNativeUI()
    {
        Assert.Throws<ArgumentNullException>(() => new PlatformMessageDialogRequest(null!, ""));
        Assert.Throws<ArgumentException>(() => new PlatformMessageDialogRequest("nul\0body", ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformMessageDialogRequest("", "", (MessageBoxButtons)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformMessageDialogRequest("", "", icon: (MessageBoxIcon)999));
    }
}
