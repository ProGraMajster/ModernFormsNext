using SkiaSharp;
using ModernFormsNext.Documents;
using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownImageAssetWorkflowTests
{
    [Fact]
    public async Task CopiesValidatedImageAndReturnsRelativeForwardSlashPath()
    {
        using var files = new TemporaryImageFiles();
        var source = files.CreatePng("source.png", SKColors.Red);
        var options = files.CreateOptions("assets/images");

        var result = await MarkdownImageAssetProcessor.CopyAsync(source, options);

        Assert.True(result.IsSuccess);
        Assert.Equal(MarkdownImageAssetStatus.Copied, result.Status);
        Assert.Equal("assets/images/source.png", result.MarkdownSource);
        Assert.True(File.Exists(result.DestinationPath));
        Assert.DoesNotContain('\\', result.MarkdownSource!);
        Assert.False(IOPath.IsPathFullyQualified(result.MarkdownSource!));
    }

    [Fact]
    public async Task CollisionPoliciesCancelUseExistingOverwriteAndGenerateUniqueName()
    {
        using var files = new TemporaryImageFiles();
        var first = files.CreatePng("first.png", SKColors.Red);
        var second = files.CreatePng("second.png", SKColors.Blue);
        var options = files.CreateOptions("assets");
        options.PreferredFileName = "shared.png";

        var initial = await MarkdownImageAssetProcessor.CopyAsync(first, options);
        Assert.True(initial.IsSuccess);

        options.CollisionBehavior = MarkdownImageAssetCollisionBehavior.Cancel;
        var cancelled = await MarkdownImageAssetProcessor.CopyAsync(second, options);
        Assert.Equal(MarkdownImageAssetStatus.Cancelled, cancelled.Status);

        options.CollisionBehavior = MarkdownImageAssetCollisionBehavior.UseExisting;
        var existing = await MarkdownImageAssetProcessor.CopyAsync(second, options);
        Assert.Equal(MarkdownImageAssetStatus.UsedExisting, existing.Status);
        Assert.Equal("assets/shared.png", existing.MarkdownSource);

        options.CollisionBehavior = MarkdownImageAssetCollisionBehavior.GenerateUniqueName;
        var unique = await MarkdownImageAssetProcessor.CopyAsync(second, options);
        Assert.Equal(MarkdownImageAssetStatus.Copied, unique.Status);
        Assert.Equal("assets/shared-2.png", unique.MarkdownSource);

        options.CollisionBehavior = MarkdownImageAssetCollisionBehavior.Overwrite;
        var overwritten = await MarkdownImageAssetProcessor.CopyAsync(second, options);
        Assert.Equal(MarkdownImageAssetStatus.Copied, overwritten.Status);
        using var bitmap = SKBitmap.Decode(overwritten.DestinationPath);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(0, 0));
    }

    [Fact]
    public async Task SanitizesPortableFilenameAndPreservesSourceExtension()
    {
        using var files = new TemporaryImageFiles();
        var source = files.CreatePng("source.png", SKColors.Green);
        var options = files.CreateOptions("assets");
        options.PreferredFileName = "  bad:name? with spaces.jpg  ";

        var result = await MarkdownImageAssetProcessor.CopyAsync(source, options);

        Assert.Equal("assets/bad_name_ with spaces.png", result.MarkdownSource);
        Assert.EndsWith(".png", result.DestinationPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SanitizesWindowsDeviceNameOnEveryPlatform()
    {
        using var files = new TemporaryImageFiles();
        var source = files.CreatePng("source.png", SKColors.Green);
        var options = files.CreateOptions("assets");
        options.PreferredFileName = "CON.jpg";

        var result = await MarkdownImageAssetProcessor.CopyAsync(source, options);

        Assert.True(result.IsSuccess);
        Assert.Equal("assets/CON_.png", result.MarkdownSource);
    }

    [Theory]
    [InlineData("too-large.png", 32, true, "exceeds")]
    [InlineData("not-allowed.txt", 1024, false, "extension")]
    [InlineData("invalid.png", 1024, false, "content")]
    public async Task RejectsInvalidSizeExtensionAndContent(
        string name,
        long maxBytes,
        bool validPng,
        string expectedMessage)
    {
        using var files = new TemporaryImageFiles();
        var source = validPng
            ? files.CreatePng(name, SKColors.Red)
            : files.CreateBytes(name, "not an image"u8.ToArray());
        var options = files.CreateOptions("assets");
        options.MaxFileBytes = maxBytes;

        var result = await MarkdownImageAssetProcessor.CopyAsync(source, options);

        Assert.Equal(MarkdownImageAssetStatus.Failed, result.Status);
        Assert.Contains(expectedMessage, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.Exists(options.DestinationDirectory)
            ? Directory.GetFiles(options.DestinationDirectory, ".mfn-*.tmp")
            : []);
    }

    [Fact]
    public async Task RequestCopiesLocalFileUsesSelectedAltAndCreatesOneUndoRecord()
    {
        using var files = new TemporaryImageFiles();
        var source = files.CreatePng("zażółć (1).png", SKColors.Red);
        var options = files.CreateOptions("media/obrazy");
        using var editor = new MarkdownEditor { Markdown = "Opis 😀" };
        editor.SelectAll();
        editor.InsertImageRequested += (_, e) =>
        {
            Assert.Equal("Opis 😀", e.SelectedText);
            e.Source = source;
            e.SourceKind = MarkdownImageSourceKind.LocalFile;
            e.AssetOptions = options;
            e.Title = "Tytuł \"obrazu\"";
            e.Handled = true;
        };

        Assert.True(await editor.RequestInsertImageAsync());

        Assert.Equal("![Opis 😀](<media/obrazy/zażółć (1).png> \"Tytuł \\\"obrazu\\\"\")", editor.Markdown);
        Assert.True(editor.Modified);
        editor.Undo();
        Assert.Equal("Opis 😀", editor.Markdown);
        Assert.False(editor.Modified);
        editor.Redo();
        Assert.Contains("media/obrazy/zażółć (1).png", editor.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelledCollisionAndCopyFailureDoNotMutateEditor()
    {
        using var files = new TemporaryImageFiles();
        var source = files.CreatePng("source.png", SKColors.Red);
        var options = files.CreateOptions("assets");
        options.CollisionBehavior = MarkdownImageAssetCollisionBehavior.Cancel;
        await MarkdownImageAssetProcessor.CopyAsync(source, options);
        using var editor = new MarkdownEditor { Markdown = "unchanged" };
        var failures = new List<string>();
        editor.ImageInsertFailed += (_, e) => failures.Add(e.Message);

        Assert.False(await editor.InsertImageAssetAsync(source, options, "alt"));
        Assert.Equal("unchanged", editor.Markdown);
        Assert.False(editor.CanUndo);
        Assert.Empty(failures);

        options.AllowedExtensions = [".jpg"];
        Assert.False(await editor.InsertImageAssetAsync(source, options, "alt"));
        Assert.Equal("unchanged", editor.Markdown);
        Assert.Single(failures);
    }

    [Fact]
    public async Task HostReportedErrorIsForwardedWithoutTextMutation()
    {
        using var editor = new MarkdownEditor { Markdown = "unchanged" };
        MarkdownImageInsertFailedEventArgs? failure = null;
        editor.ImageInsertFailed += (_, e) => failure = e;
        editor.InsertImageRequested += (_, e) =>
        {
            e.Source = "selected.png";
            e.ErrorMessage = "Picker service failed.";
            e.Handled = true;
        };

        Assert.False(await editor.RequestInsertImageAsync());
        Assert.Equal("unchanged", editor.Markdown);
        Assert.Equal("Picker service failed.", failure?.Message);
        Assert.False(editor.Modified);
    }

    [Fact]
    public void ReloadDocumentImagesReplacesCachedBitmapForUnchangedSource()
    {
        using var files = new TemporaryImageFiles();
        var source = files.CreatePng("reload.png", SKColors.Red);
        var resourceSource = new Uri(source).AbsoluteUri;
        using var viewer = new DocumentViewer
        {
            Document = new Document(new DocumentBlock[] { new ImageBlock(resourceSource, "reload") })
        };
        var loaded = new SKBitmap(2, 2);
        loaded.Erase(SKColors.Red);
        viewer.ImageCache.SetLoadedForTesting(resourceSource, loaded);
        var firstResource = viewer.ImageCache.GetResource(resourceSource);
        Assert.Equal(DocumentImageResourceState.Loaded, firstResource?.State);

        files.CreatePng("reload.png", SKColors.Blue, overwrite: true);
        viewer.ReloadDocumentImages();
        var reloadingResource = viewer.ImageCache.GetResource(resourceSource);

        Assert.NotSame(firstResource, reloadingResource);
        Assert.Equal(DocumentImageResourceState.Loading, reloadingResource?.State);
        Assert.Null(reloadingResource?.Bitmap);
    }

    [Fact]
    public async Task ExplicitCancellationLeavesNoMarkdownOrTemporaryAsset()
    {
        using var files = new TemporaryImageFiles();
        var source = files.CreatePng("cancel.png", SKColors.Red);
        var options = files.CreateOptions("assets");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var editor = new MarkdownEditor { Markdown = "text" };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            editor.InsertImageAssetAsync(source, options, cancellationToken: cancellation.Token));

        Assert.Equal("text", editor.Markdown);
        Assert.Empty(Directory.Exists(options.DestinationDirectory)
            ? Directory.GetFiles(options.DestinationDirectory, ".mfn-*.tmp")
            : []);
    }

    private sealed class TemporaryImageFiles : IDisposable
    {
        public TemporaryImageFiles()
        {
            Root = IOPath.Combine(IOPath.GetTempPath(), "ModernFormsNext.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public MarkdownImageAssetOptions CreateOptions(string relativeDestination)
            => new()
            {
                DestinationDirectory = IOPath.Combine(Root, relativeDestination.Replace('/', IOPath.DirectorySeparatorChar)),
                MarkdownBaseDirectory = Root
            };

        public string CreateBytes(string name, byte[] bytes)
        {
            var path = IOPath.Combine(Root, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public string CreatePng(string name, SKColor color, bool overwrite = false)
        {
            var path = IOPath.Combine(Root, name);
            if (File.Exists(path) && !overwrite)
                throw new IOException("The test image already exists.");

            using var bitmap = new SKBitmap(2, 2);
            bitmap.Erase(color);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
