using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Platform.Storage;
using ModernFormsNext.WindowKit.Platform.Storage.FileIO;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class PlatformApplicationServiceTests
{
    [Fact]
    public void HelpUsesScopedLauncherAndReportsFailure()
    {
        using var host = ModernFormsTestHost.Create();
        Help.Help.ShowHelp(null, "https://example.com/help");
        Assert.Equal("https://example.com/help", host.Services.ApplicationServices.LastOpenedUri!.AbsoluteUri);
        host.Services.ApplicationServices.NextStatus = PlatformServiceStatus.NoHandler;
        Assert.Equal(PlatformServiceStatus.NoHandler,
            Assert.Throws<PlatformServiceException>(() => Help.Help.ShowHelp(null, "https://example.com")).Status);
        host.Services.ApplicationServices.NextException = new IOException();
        Assert.Throws<IOException>(() => Help.Help.ShowHelp(null, "https://example.com"));
    }

    [Fact]
    public async Task NotificationUpdateDismissAndSharingOutcomesAreIsolated()
    {
        var baseline = AvaloniaGlobals.GetService<IPlatformNotificationService>();
        TestApplicationServices retained;
        using (var host = ModernFormsTestHost.Create())
        {
            retained = host.Services.ApplicationServices;
            retained.Show(new("stable", "one", "body"));
            retained.Show(new("stable", "two", "replacement"));
            Assert.Single(retained.Notifications);
            Assert.Equal("two", retained.Notifications["stable"].Title);
            retained.Dismiss("stable");
            Assert.Empty(retained.Notifications);
            retained.NextStatus = PlatformServiceStatus.PermissionDenied;
            Assert.Equal(PlatformServiceStatus.PermissionDenied, retained.Show(new("denied", "", "")));
            retained.NextStatus = PlatformServiceStatus.Unavailable;
            Assert.Equal(PlatformServiceStatus.Unavailable, await retained.ShareAsync(new() { Text = "synthetic" }));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retained.ShareAsync(new() { Text = "synthetic" }, new CancellationToken(true)));
            retained.NextException = new IOException();
            await Assert.ThrowsAsync<IOException>(() => retained.ShareAsync(new() { Text = "synthetic" }));
        }
        Assert.Throws<ObjectDisposedException>(() => retained.Dismiss("stable"));
        Assert.Same(baseline, AvaloniaGlobals.GetService<IPlatformNotificationService>());
        using var next = ModernFormsTestHost.Create();
        Assert.Empty(next.Services.ApplicationServices.Notifications);
        Assert.Null(next.Services.ApplicationServices.LastOpenedUri);
    }

    [Fact]
    public async Task ProductionDialogFacadesPreserveContentUrisAndExistingLocalPaths()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        host.Show(form);
        using var content = new UriFile(new Uri("content://synthetic/document/test.txt"));
        host.Services.Storage.OpenFiles = [content];
        var open = new OpenFileDialog();
        Assert.Equal(DialogResult.OK, await open.ShowDialog(form));
        Assert.Equal(content.Path.AbsoluteUri, open.FileName);
        Assert.Same(content, Assert.Single(open.SelectedFiles));
        host.Services.Storage.SaveFile = content;
        var save = new SaveFileDialog();
        Assert.Equal(DialogResult.OK, await save.ShowDialog(form));
        Assert.Equal(content.Path.AbsoluteUri, save.FileName);
        host.Services.Storage.OpenFiles = [];
        Assert.Equal(DialogResult.Cancel, await open.ShowDialog(form));
        Assert.Empty(open.SelectedFiles);
        open.CancellationToken = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => open.ShowDialog(form));
        using var local = new BclStorageFile(new FileInfo(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mfn-synthetic.txt")));
        host.Services.Storage.OpenFiles = [local];
        open.CancellationToken = default;
        Assert.Equal(DialogResult.OK, await open.ShowDialog(form));
        Assert.Equal(local.FileInfo.FullName, open.FileName);
    }

    private sealed class UriFile(Uri path) : IStorageFile
    {
        public string Name => "test.txt";
        public Uri Path => path;
        public bool CanBookmark => false;
        public Task<StorageItemProperties> GetBasicPropertiesAsync() => Task.FromResult(new StorageItemProperties());
        public Task<string?> SaveBookmarkAsync() => Task.FromResult<string?>(null);
        public Task<IStorageFolder?> GetParentAsync() => Task.FromResult<IStorageFolder?>(null);
        public Task DeleteAsync() => throw new NotSupportedException();
        public Task<IStorageItem?> MoveAsync(IStorageFolder destination) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(new MemoryStream());
        public Task<Stream> OpenWriteAsync() => Task.FromResult<Stream>(new MemoryStream());
        public void Dispose() { }
    }
}
