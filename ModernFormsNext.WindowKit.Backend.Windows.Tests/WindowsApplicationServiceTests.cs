using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Platform.Storage.FileIO;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsApplicationServiceTests
{
    [Fact]
    public void LauncherValidatesUrisAndPreservesLocalFileCapability()
    {
        var launcher = new WindowsLauncherService();
        Assert.Throws<ArgumentNullException>(() => launcher.CanOpenUri(null!));
        Assert.Throws<ArgumentException>(() => launcher.OpenUri(new Uri("relative", UriKind.Relative)));
        using var file = new BclStorageFile(new FileInfo(typeof(WindowsApplicationServiceTests).Assembly.Location));
        Assert.True(launcher.CanOpenUri(file.Path));
        Assert.False(launcher.CanOpenUri(new Uri("file:///C:/mfn-missing-synthetic-file-79")));
        Assert.False(launcher.CanOpenUri(new Uri("mfn-no-handler-issue79:synthetic")));
    }

    [Fact]
    public async Task UnsupportedWindowsServicesReportCapabilitiesWithoutStartingUi()
    {
        var service = new UnsupportedPlatformServices();
        Assert.False(service.IsSupported);
        Assert.Equal(PlatformServiceStatus.NotSupported, service.Status);
        Assert.Equal(PlatformServiceStatus.NotSupported, service.Show(new("id", "title", "body")));
        Assert.Equal(PlatformServiceStatus.NotSupported, service.Dismiss("id"));
        Assert.Equal(PlatformServiceStatus.NotSupported, await service.ShareAsync(new() { Text = "synthetic" }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ShareAsync(new(), new CancellationToken(true)));
    }
}
