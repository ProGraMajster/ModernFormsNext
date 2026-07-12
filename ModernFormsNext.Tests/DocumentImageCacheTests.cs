using ModernFormsNext.Documents;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentImageCacheTests
{
    [Fact]
    public async Task DuplicateImageBlockSourceStartsOneLoadRequest()
    {
        var loads = 0;
        using var cache = new DocumentImageCache(
            (source, cancellationToken) =>
            {
                loads++;
                return Task.FromResult<SKBitmap?>(new SKBitmap(4, 4));
            },
            action => action(),
            () => { });

        cache.SetDocument(new Document(new DocumentBlock[]
        {
            new ImageBlock("same-source.png", "first"),
            new ImageBlock("same-source.png", "second")
        }));

        await DocumentTestHelpers.WaitForAsync(() => cache.GetResource("same-source.png")?.State == DocumentImageResourceState.Loaded);

        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task ImageLoadUsesViewerLimits()
    {
        DocumentImageLoadLimits? seenLimits = null;
        using var cache = new DocumentImageCache(
            (source, limits, cancellationToken) =>
            {
                seenLimits = limits;
                return Task.FromResult<SKBitmap?>(new SKBitmap(4, 4));
            },
            action => action(),
            () => { },
            () => new DocumentImageLoadLimits(1024, 4096, TimeSpan.FromSeconds(3)));

        cache.SetDocument(new Document(new DocumentBlock[]
        {
            new ImageBlock("limited.png", "Limited")
        }));

        await DocumentTestHelpers.WaitForAsync(() => cache.GetResource("limited.png")?.State == DocumentImageResourceState.Loaded);

        Assert.NotNull(seenLimits);
        Assert.Equal(1024, seenLimits.Value.MaxDownloadBytes);
        Assert.Equal(4096, seenLimits.Value.MaxDecodedPixels);
        Assert.Equal(TimeSpan.FromSeconds(3), seenLimits.Value.RequestTimeout);
    }

    [Theory]
    [InlineData("data:image/png;base64,not-valid-base64")]
    [InlineData("data:image/png;base64")]
    [InlineData("missing-document-image.png")]
    public async Task MalformedAndMissingSourcesFailGracefully(string source)
    {
        using var cache = CreateDefaultCache();
        cache.SetDocument(new Document(new DocumentBlock[] { new ImageBlock(source, "fallback") }));

        await DocumentTestHelpers.WaitForAsync(() => cache.GetResource(source)?.State == DocumentImageResourceState.Failed);

        Assert.Equal(DocumentImageResourceState.Failed, cache.GetResource(source)?.State);
    }

    [Fact]
    public async Task DirectorySourceAndDecodeFailureFailGracefully()
    {
        var directory = AppContext.BaseDirectory;
        const string invalidImage = "data:application/octet-stream;base64,bm90IGFuIGltYWdl";
        using var cache = CreateDefaultCache();
        cache.SetDocument(new Document(new DocumentBlock[]
        {
            new ImageBlock(directory, "directory"),
            new ImageBlock(invalidImage, "invalid")
        }));

        await DocumentTestHelpers.WaitForAsync(() => cache.GetResource(directory)?.State == DocumentImageResourceState.Failed);
        await DocumentTestHelpers.WaitForAsync(() => cache.GetResource(invalidImage)?.State == DocumentImageResourceState.Failed);

        Assert.Equal(DocumentImageResourceState.Failed, cache.GetResource(directory)?.State);
        Assert.Equal(DocumentImageResourceState.Failed, cache.GetResource(invalidImage)?.State);
    }

    [Fact]
    public async Task DecodedPixelLimitRejectsOtherwiseValidImage()
    {
        using var bitmap = new SKBitmap(4, 4);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var source = "data:image/png;base64," + Convert.ToBase64String(encoded.ToArray());
        using var cache = new DocumentImageCache(
            DocumentImageCache.DefaultLoadImageAsync,
            action => action(),
            () => { },
            () => new DocumentImageLoadLimits(1024 * 1024, 4, TimeSpan.FromSeconds(1)));

        cache.SetDocument(new Document(new DocumentBlock[] { new ImageBlock(source, "large decoded image") }));
        await DocumentTestHelpers.WaitForAsync(() => cache.GetResource(source)?.State == DocumentImageResourceState.Failed);

        Assert.Equal(DocumentImageResourceState.Failed, cache.GetResource(source)?.State);
    }

    [Fact]
    public async Task LimitedStreamWithoutKnownLengthStopsAtLimit()
    {
        await using var stream = new MemoryStream(new byte[33]);

        var bytes = await DocumentImageCache.ReadLimitedAsync(stream, 32, CancellationToken.None);

        Assert.Empty(bytes);
    }

    [Fact]
    public async Task LimitedStreamObservesCancellation()
    {
        await using var stream = new SlowStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DocumentImageCache.ReadLimitedAsync(stream, 32, cancellation.Token));
    }

    private static DocumentImageCache CreateDefaultCache()
        => new(
            DocumentImageCache.DefaultLoadImageAsync,
            action => action(),
            () => { },
            () => DocumentImageLoadLimits.Default);

    private sealed class SlowStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromCanceled<int>(cancellationToken);
    }
}
