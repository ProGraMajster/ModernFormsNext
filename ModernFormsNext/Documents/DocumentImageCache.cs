using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace ModernFormsNext.Documents;

internal sealed class DocumentImageCache : IDisposable
{
    // Per-resource timeouts are enforced by linked cancellation tokens. Keeping HttpClient's
    // process-wide timeout disabled gives DocumentImageLoadOptions one unambiguous timeout source.
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private readonly Dictionary<string, DocumentImageResource> resources = new(StringComparer.Ordinal);
    private readonly Func<DocumentImageLoadLimits> getLimits;
    private readonly Func<string, DocumentImageLoadLimits, CancellationToken, Task<SKBitmap?>> loadImageAsync;
    private readonly Action<Action> postCompletion;
    private readonly Action resourceChanged;
    private CancellationTokenSource loadCancellation = new();
    private bool disposed;
    private int version;

    public DocumentImageCache(Action resourceChanged, Func<DocumentImageLoadLimits>? getLimits = null)
        : this(DefaultLoadImageAsync, Application.RunOnUIThread, resourceChanged, getLimits ?? (() => DocumentImageLoadLimits.Default))
    {
    }

    internal DocumentImageCache(
        Func<string, CancellationToken, Task<SKBitmap?>> loadImageAsync,
        Action<Action> postCompletion,
        Action resourceChanged)
        : this((source, limits, cancellationToken) => loadImageAsync(source, cancellationToken), postCompletion, resourceChanged, () => DocumentImageLoadLimits.Default)
    {
    }

    internal DocumentImageCache(
        Func<string, DocumentImageLoadLimits, CancellationToken, Task<SKBitmap?>> loadImageAsync,
        Action<Action> postCompletion,
        Action resourceChanged,
        Func<DocumentImageLoadLimits> getLimits)
    {
        this.loadImageAsync = loadImageAsync ?? throw new ArgumentNullException(nameof(loadImageAsync));
        this.postCompletion = postCompletion ?? throw new ArgumentNullException(nameof(postCompletion));
        this.resourceChanged = resourceChanged ?? throw new ArgumentNullException(nameof(resourceChanged));
        this.getLimits = getLimits ?? throw new ArgumentNullException(nameof(getLimits));
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        version++;
        loadCancellation.Cancel();
        loadCancellation.Dispose();
        ClearResources();
    }

    public DocumentImageResource? GetResource(string source)
        => resources.TryGetValue(source, out var resource) ? resource : null;

    public void SetDocument(Document document)
    {
        if (disposed)
            return;

        ArgumentNullException.ThrowIfNull(document);
        version++;

        var previousCancellation = loadCancellation;
        loadCancellation = new CancellationTokenSource();
        previousCancellation.Cancel();
        previousCancellation.Dispose();
        ClearResources();

        foreach (var source in CollectImageSources(document))
            StartResource(source, version, loadCancellation.Token);
    }

    public void ReloadPendingAndFailed(Document document)
    {
        if (disposed)
            return;

        ArgumentNullException.ThrowIfNull(document);
        version++;

        var previousCancellation = loadCancellation;
        loadCancellation = new CancellationTokenSource();
        previousCancellation.Cancel();
        previousCancellation.Dispose();

        var currentSources = new HashSet<string>(CollectImageSources(document), StringComparer.Ordinal);
        var remove = resources
            .Where(pair => !currentSources.Contains(pair.Key) || pair.Value.State != DocumentImageResourceState.Loaded)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var source in remove)
        {
            resources[source].Bitmap?.Dispose();
            resources.Remove(source);
        }

        foreach (var source in currentSources)
        {
            if (!resources.ContainsKey(source))
                StartResource(source, version, loadCancellation.Token);
        }
    }

    internal void SetFailedForTesting(string source)
    {
        var resource = GetOrCreateTestingResource(source);
        resource.Bitmap?.Dispose();
        resource.Bitmap = null;
        resource.State = DocumentImageResourceState.Failed;
    }

    internal void SetLoadedForTesting(string source, SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var resource = GetOrCreateTestingResource(source);
        resource.Bitmap?.Dispose();
        resource.Bitmap = bitmap;
        resource.State = DocumentImageResourceState.Loaded;
    }

    private static IReadOnlyCollection<string> CollectImageSources(Document document)
    {
        var sources = new HashSet<string>(StringComparer.Ordinal);

        foreach (var block in document.Blocks)
            CollectImageSources(block, sources);

        return sources;
    }

    private static void CollectImageSources(DocumentBlock block, HashSet<string> sources)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                CollectImageSources(paragraph.Inlines, sources);
                break;
            case HeadingBlock heading:
                CollectImageSources(heading.Inlines, sources);
                break;
            case ImageBlock image when !string.IsNullOrWhiteSpace(image.Source):
                sources.Add(image.Source);
                break;
            case QuoteBlock quote:
                foreach (var nested in quote.Blocks)
                    CollectImageSources(nested, sources);
                break;
            case ListBlock list:
                foreach (var item in list.Items)
                {
                    foreach (var nested in item.Blocks)
                        CollectImageSources(nested, sources);
                }

                break;
            case TableBlock table:
                foreach (var row in table.Rows)
                {
                    foreach (var cell in row.Cells)
                    {
                        foreach (var nested in cell.Blocks)
                            CollectImageSources(nested, sources);
                    }
                }

                break;
            case FootnoteGroupBlock footnotes:
                foreach (var footnote in footnotes.Footnotes)
                {
                    foreach (var nested in footnote.Blocks)
                        CollectImageSources(nested, sources);
                }

                break;
        }
    }

    private static void CollectImageSources(IEnumerable<DocumentInline> inlines, HashSet<string> sources)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case ImageInline image when !string.IsNullOrWhiteSpace(image.Source):
                    sources.Add(image.Source);
                    break;
                case StrongInline strong:
                    CollectImageSources(strong.Inlines, sources);
                    break;
                case EmphasisInline emphasis:
                    CollectImageSources(emphasis.Inlines, sources);
                    break;
                case StrikethroughInline strike:
                    CollectImageSources(strike.Inlines, sources);
                    break;
                case LinkInline link:
                    CollectImageSources(link.Inlines, sources);
                    break;
            }
        }
    }

    internal static async Task<SKBitmap?> DefaultLoadImageAsync(
        string source,
        DocumentImageLoadLimits limits,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (limits.RequestTimeout > TimeSpan.Zero)
            timeout.CancelAfter(limits.RequestTimeout);

        var bytes = await LoadBytesAsync(source, limits, timeout.Token).ConfigureAwait(false);
        timeout.Token.ThrowIfCancellationRequested();
        return DecodeBitmap(bytes, limits);
    }

    private static SKBitmap? DecodeBitmap(byte[] bytes, DocumentImageLoadLimits limits)
    {
        if (bytes.Length == 0)
            return null;

        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);

        if (codec is not null && !IsPixelCountAllowed(codec.Info.Width, codec.Info.Height, limits))
            return null;

        var bitmap = codec is null ? SKBitmap.Decode(bytes) : SKBitmap.Decode(codec);

        if (bitmap is not { Width: > 0, Height: > 0 })
            return null;

        if (IsPixelCountAllowed(bitmap.Width, bitmap.Height, limits))
            return bitmap;

        bitmap.Dispose();
        return null;
    }

    private static async Task<byte[]> LoadBytesAsync(
        string source,
        DocumentImageLoadLimits limits,
        CancellationToken cancellationToken)
    {
        if (TryReadDataUri(source, limits, out var bytes))
            return bytes;

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                using var response = await SharedHttpClient
                    .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return Array.Empty<byte>();

                if (response.Content.Headers.ContentLength is long length && length > limits.MaxDownloadBytes)
                    return Array.Empty<byte>();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                return await ReadLimitedAsync(stream, limits.MaxDownloadBytes, cancellationToken).ConfigureAwait(false);
            }

            if (uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
                return await ReadFileLimitedAsync(uri.LocalPath, limits, cancellationToken).ConfigureAwait(false);
        }

        var path = Path.IsPathRooted(source)
            ? source
            : Path.GetFullPath(source, AppContext.BaseDirectory);

        return await ReadFileLimitedAsync(path, limits, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryReadDataUri(string source, DocumentImageLoadLimits limits, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;

        var commaIndex = source.IndexOf(',');
        if (commaIndex < 0)
            return false;

        var metadata = source.Substring(0, commaIndex);
        var payload = source[(commaIndex + 1)..];

        var maximumEncodedLength = (((long)limits.MaxDownloadBytes + 2) / 3) * 4;
        if (payload.Length > maximumEncodedLength)
            return true;

        bytes = metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(payload)
            : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));

        if (bytes.Length > limits.MaxDownloadBytes)
        {
            bytes = Array.Empty<byte>();
            return true;
        }

        return true;
    }

    private static bool IsPixelCountAllowed(int width, int height, DocumentImageLoadLimits limits)
    {
        if (width <= 0 || height <= 0)
            return false;

        return (long)width * height <= limits.MaxDecodedPixels;
    }

    private static async Task<byte[]> ReadFileLimitedAsync(
        string path,
        DocumentImageLoadLimits limits,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (info.Exists && info.Length > limits.MaxDownloadBytes)
            return Array.Empty<byte>();

        await using var stream = File.OpenRead(path);
        return await ReadLimitedAsync(stream, limits.MaxDownloadBytes, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<byte[]> ReadLimitedAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (maxBytes <= 0)
            return Array.Empty<byte>();

        using var memory = new MemoryStream(Math.Min(maxBytes, 64 * 1024));
        var buffer = new byte[16 * 1024];

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return memory.ToArray();

            if (memory.Length + read > maxBytes)
                return Array.Empty<byte>();

            memory.Write(buffer, 0, read);
        }
    }

    private void ClearResources()
    {
        foreach (var resource in resources.Values)
            resource.Bitmap?.Dispose();

        resources.Clear();
    }

    private void CompleteLoad(string source, int resourceVersion, SKBitmap? bitmap, bool success)
    {
        if (disposed || resourceVersion != version || !resources.TryGetValue(source, out var resource))
        {
            bitmap?.Dispose();
            return;
        }

        resource.Bitmap?.Dispose();

        if (success && bitmap is not null)
        {
            resource.Bitmap = bitmap;
            resource.State = DocumentImageResourceState.Loaded;
        }
        else
        {
            bitmap?.Dispose();
            resource.Bitmap = null;
            resource.State = DocumentImageResourceState.Failed;
        }

        resourceChanged();
    }

    private DocumentImageResource GetOrCreateTestingResource(string source)
    {
        if (!resources.TryGetValue(source, out var resource))
        {
            resource = new DocumentImageResource(source);
            resources.Add(source, resource);
        }

        return resource;
    }

    private async Task LoadResourceAsync(
        string source,
        int resourceVersion,
        DocumentImageLoadLimits limits,
        CancellationToken cancellationToken)
    {
        SKBitmap? bitmap = null;
        var success = false;

        try
        {
            bitmap = await loadImageAsync(source, limits, cancellationToken).ConfigureAwait(false);
            success = bitmap is { Width: > 0, Height: > 0 };
        }
        catch (OperationCanceledException)
        {
            bitmap?.Dispose();
            if (cancellationToken.IsCancellationRequested)
                return;

            success = false;
        }
        catch (Exception exception) when (IsExpectedResourceFailure(exception))
        {
            success = false;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            bitmap?.Dispose();
            return;
        }

        try
        {
            postCompletion(() => CompleteLoad(source, resourceVersion, bitmap, success));
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
        {
            bitmap?.Dispose();
        }
    }

    private static bool IsExpectedResourceFailure(Exception exception)
        => exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or FormatException
            or InvalidOperationException
            or NotSupportedException
            or System.Security.SecurityException;

    private void StartResource(string source, int resourceVersion, CancellationToken cancellationToken)
    {
        var resource = new DocumentImageResource(source)
        {
            State = DocumentImageResourceState.Loading
        };

        resources.Add(source, resource);

        var limits = getLimits();
        _ = LoadResourceAsync(source, resourceVersion, limits, cancellationToken);
    }
}

internal sealed class DocumentImageResource
{
    public DocumentImageResource(string source)
    {
        Source = source;
    }

    public SKBitmap? Bitmap { get; set; }

    public string Source { get; }

    public DocumentImageResourceState State { get; set; } = DocumentImageResourceState.Pending;
}

internal enum DocumentImageResourceState
{
    Pending,
    Loading,
    Loaded,
    Failed
}
