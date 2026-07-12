using ModernFormsNext.Documents;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentImageOptionsTests
{
    [Fact]
    public void DefaultsMatchDocumentImageSafetyLimits()
    {
        var options = new DocumentImageLoadOptions();

        Assert.Equal(10 * 1024 * 1024, options.MaxDownloadBytes);
        Assert.Equal(32_000_000, options.MaxDecodedPixels);
        Assert.Equal(TimeSpan.FromSeconds(15), options.RequestTimeout);
    }

    [Fact]
    public void InvalidLimitsAreRejectedAndZeroTimeoutIsAllowed()
    {
        var options = new DocumentImageLoadOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxDownloadBytes = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxDecodedPixels = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.RequestTimeout = TimeSpan.FromMilliseconds(-1));

        options.RequestTimeout = TimeSpan.Zero;
        Assert.Equal(TimeSpan.Zero, options.RequestTimeout);
    }

    [Fact]
    public void ViewerCompatibilityPropertiesShareOneOptionsInstance()
    {
        using var viewer = new DocumentViewer();

        viewer.MaxImageDownloadBytes = 1024;
        viewer.MaxImagePixelCount = 4096;
        viewer.ImageRequestTimeout = TimeSpan.FromSeconds(2);

        Assert.Equal(1024, viewer.ImageLoadOptions.MaxDownloadBytes);
        Assert.Equal(4096, viewer.ImageLoadOptions.MaxDecodedPixels);
        Assert.Equal(TimeSpan.FromSeconds(2), viewer.ImageLoadOptions.RequestTimeout);
    }

    [Fact]
    public async Task ReloadKeepsLoadedResourceAndRestartsPendingWithNewOptions()
    {
        var limits = new DocumentImageLoadLimits(100, 100, TimeSpan.FromSeconds(1));
        var loadedRequests = 0;
        var pendingLimits = new List<int>();
        using var pendingGate = new SemaphoreSlim(0);
        using var cache = new DocumentImageCache(
            async (source, seenLimits, cancellationToken) =>
            {
                if (source == "loaded.png")
                {
                    loadedRequests++;
                    return new SKBitmap(2, 2);
                }

                pendingLimits.Add(seenLimits.MaxDownloadBytes);
                await pendingGate.WaitAsync(cancellationToken);
                return new SKBitmap(2, 2);
            },
            action => action(),
            () => { },
            () => limits);
        var document = new Document(new DocumentBlock[]
        {
            new ImageBlock("loaded.png", "loaded"),
            new ImageBlock("pending.png", "pending")
        });

        cache.SetDocument(document);
        await DocumentTestHelpers.WaitForAsync(() => cache.GetResource("loaded.png")?.State == DocumentImageResourceState.Loaded);
        await DocumentTestHelpers.WaitForAsync(() => pendingLimits.Count == 1);

        limits = new DocumentImageLoadLimits(200, 200, TimeSpan.FromSeconds(2));
        cache.ReloadPendingAndFailed(document);
        await DocumentTestHelpers.WaitForAsync(() => pendingLimits.Count == 2);

        Assert.Equal(1, loadedRequests);
        Assert.Equal(new[] { 100, 200 }, pendingLimits);
    }
}
