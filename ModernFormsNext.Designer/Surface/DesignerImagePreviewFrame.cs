using ModernFormsNext.Designer.Services;
using ModernFormsNext.Drawing;
using ModernFormsNext.Designing;
using SkiaSharp;

namespace ModernFormsNext.Designer.Surface;

// Only the current and previous frames can own decoded bitmaps. Transfer an
// unchanged bitmap after validating the newly opened file on the current frame.
internal sealed class DesignerImagePreviewFrame(string? projectPath, string? documentPath,
    DesignerImagePreviewFrame? previous = null) : IDisposable
{
    private const int MaximumImagesPerFrame = 8;
    private readonly Dictionary<string, Entry> images = new(StringComparer.Ordinal);
    private DesignerImagePreviewFrame? previousFrame = previous;
    private readonly string? hostProjectPath = projectPath;
    private readonly string? hostDocumentPath = documentPath;
    private string? root;
    private string? rootError;
    private bool rootResolved;

    internal string? Apply(Control control, DesignPropertyValue value)
    {
        if (value is not { Kind: DesignPropertyValueKind.String, Value: string id } || !ProjectAssetPath.IsValidId(id))
            return "Expected a portable project image identifier.";

        if (!images.TryGetValue(id, out var result))
        {
            if (images.Count >= MaximumImagesPerFrame)
                return "The preview reached its image budget.";

            if (!rootResolved)
            {
                rootResolved = true;
                try
                {
                    root = previousFrame is { root: not null } oldFrame
                        && oldFrame.hostProjectPath == hostProjectPath && oldFrame.hostDocumentPath == hostDocumentPath
                        ? oldFrame.root
                        : DesignerProjectAssets.GetRoot(hostProjectPath, hostDocumentPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    rootError = exception.Message;
                }
            }

            try
            {
                if (root is null)
                {
                    result = new(null, rootError ?? "No project root is available for this image.", default);
                }
                else
                {
                    using var stream = DesignerProjectAssets.OpenImage(root, id);
                    var stamp = new FileStamp(stream.Length, File.GetLastWriteTimeUtc(stream.SafeFileHandle));
                    result = previousFrame is { } previousImage && previousImage.root == root
                        && previousImage.TryTake(id, stamp, out var existing)
                        ? existing
                        : new(ProjectImageAsset.Decode(stream), null, stamp);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                result = new(null, $"Project image '{id}' is unavailable: {exception.Message}", default);
            }

            images.Add(id, result);
        }

        if (result.Bitmap is not null)
            ProjectAssets.Assign(control, result.Bitmap);
        return result.Error;
    }

    private bool TryTake(string id, FileStamp stamp, out Entry entry)
    {
        if (!images.TryGetValue(id, out entry) || entry.Bitmap is null || entry.Stamp != stamp)
            return false;
        images.Remove(id);
        return true;
    }

    // The renderer can now dispose any images left behind in the previous frame.
    internal void Complete() => previousFrame = null;

    public void Dispose()
    {
        foreach (var image in images.Values)
            image.Bitmap?.Dispose();
        images.Clear();
    }

    private readonly record struct FileStamp(long Length, DateTime LastWriteUtc);
    private readonly record struct Entry(SKBitmap? Bitmap, string? Error, FileStamp Stamp);
}
