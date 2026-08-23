using System.Buffers;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace ModernFormsNext.Designer.Services;

/// <summary>
/// Identifies the raw filesystem notification observed for a Designer-owned file.
/// </summary>
internal enum DesignerFileChangeKind
{
    Changed,
    Created,
    Deleted,
    Renamed
}

/// <summary>
/// Describes one raw filesystem notification for a watched Designer file.
/// </summary>
internal sealed class DesignerFileChangeEventArgs : EventArgs
{
    public DesignerFileChangeEventArgs(
        DesignerFileChangeKind kind,
        string path,
        string? oldPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Kind = kind;
        Path = path;
        OldPath = oldPath;
    }

    public DesignerFileChangeKind Kind { get; }

    public string Path { get; }

    public string? OldPath { get; }
}

/// <summary>
/// Emits raw changes for one canonical <c>.mfdesign</c> file and its generated C# sibling.
/// </summary>
internal interface IDesignerFileChangeSource : IDisposable
{
    string DesignDocumentPath { get; }

    string GeneratedCodePath { get; }

    event EventHandler<DesignerFileChangeEventArgs>? Changed;
}

/// <summary>
/// Creates isolated file-change sources for Designer documents.
/// </summary>
internal interface IDesignerFileChangeSourceFactory
{
    IDesignerFileChangeSource Create(string designDocumentPath);
}

/// <summary>
/// Creates production <see cref="FileSystemWatcher"/>-backed Designer change sources.
/// </summary>
internal sealed class FileSystemDesignerFileChangeSourceFactory : IDesignerFileChangeSourceFactory
{
    public static FileSystemDesignerFileChangeSourceFactory Instance { get; } = new();

    private FileSystemDesignerFileChangeSourceFactory()
    {
    }

    public IDesignerFileChangeSource Create(string designDocumentPath)
        => new FileSystemDesignerFileChangeSource(designDocumentPath);
}

/// <summary>
/// Watches the directory containing one design document and publishes notifications only when
/// either the exact design path or its exact generated-code path participates in the event.
/// </summary>
/// <remarks>
/// Filesystem notifications are deliberately not deduplicated here. The persistence coordinator
/// verifies final content and coalesces duplicate or replace-sequence events after dispatch.
/// </remarks>
internal sealed class FileSystemDesignerFileChangeSource : IDesignerFileChangeSource
{
    private readonly object gate = new();
    private readonly FileSystemWatcher watcher;
    private readonly HashSet<string> watchedPaths;
    private EventHandler<DesignerFileChangeEventArgs>? changed;
    private bool disposed;

    public FileSystemDesignerFileChangeSource(string designDocumentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(designDocumentPath);

        DesignDocumentPath = IOPath.GetFullPath(designDocumentPath);
        if (!string.Equals(IOPath.GetExtension(DesignDocumentPath), ".mfdesign", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Designer file-change sources require a .mfdesign document path.",
                nameof(designDocumentPath));
        }

        var directory = IOPath.GetDirectoryName(DesignDocumentPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"The Designer document directory does not exist: '{directory}'.");

        GeneratedCodePath = IOPath.Combine(
            directory,
            $"{IOPath.GetFileNameWithoutExtension(DesignDocumentPath)}.Designer.cs");
        watchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DesignDocumentPath,
            GeneratedCodePath
        };

        // A directory-wide native filter is required to observe a target being renamed away from
        // its original name. Publish filters below still expose only the two exact target paths.
        watcher = new FileSystemWatcher(directory, "*")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.CreationTime
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
        };
        watcher.Changed += Watcher_Changed;
        watcher.Created += Watcher_Created;
        watcher.Deleted += Watcher_Deleted;
        watcher.Renamed += Watcher_Renamed;
        watcher.Error += Watcher_Error;
        watcher.EnableRaisingEvents = true;
    }

    public string DesignDocumentPath { get; }

    public string GeneratedCodePath { get; }

    public event EventHandler<DesignerFileChangeEventArgs>? Changed
    {
        add
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                changed += value;
            }
        }
        remove
        {
            lock (gate)
                changed -= value;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;

            disposed = true;
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= Watcher_Changed;
            watcher.Created -= Watcher_Created;
            watcher.Deleted -= Watcher_Deleted;
            watcher.Renamed -= Watcher_Renamed;
            watcher.Error -= Watcher_Error;
            changed = null;
        }

        watcher.Dispose();
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
        => Publish(e.FullPath, DesignerFileChangeKind.Changed);

    private void Watcher_Created(object sender, FileSystemEventArgs e)
        => Publish(e.FullPath, DesignerFileChangeKind.Created);

    private void Watcher_Deleted(object sender, FileSystemEventArgs e)
        => Publish(e.FullPath, DesignerFileChangeKind.Deleted);

    private void Watcher_Renamed(object sender, RenamedEventArgs e)
    {
        if (!IsWatchedPath(e.FullPath) && !IsWatchedPath(e.OldFullPath))
            return;

        PublishCore(new DesignerFileChangeEventArgs(
            DesignerFileChangeKind.Renamed,
            NormalizeEventPath(e.FullPath),
            NormalizeEventPath(e.OldFullPath)));
    }

    private void Watcher_Error(object sender, ErrorEventArgs e)
        => PublishFullRecheck();

    internal void PublishFullRecheck()
    {
        // A native watcher buffer overflow means one or more path-specific notifications may have
        // been lost. Recheck both exact targets; the coordinator performs stable hashing and
        // coalescing, so unchanged files remain no-ops and no directory-wide paths are exposed.
        PublishCore(new DesignerFileChangeEventArgs(DesignerFileChangeKind.Changed, DesignDocumentPath));
        PublishCore(new DesignerFileChangeEventArgs(DesignerFileChangeKind.Changed, GeneratedCodePath));
    }

    private void Publish(string path, DesignerFileChangeKind kind)
    {
        if (!IsWatchedPath(path))
            return;

        PublishCore(new DesignerFileChangeEventArgs(kind, NormalizeEventPath(path)));
    }

    private void PublishCore(DesignerFileChangeEventArgs e)
    {
        lock (gate)
        {
            if (disposed)
                return;

            // Holding the gate while the internal observer posts its work guarantees that no raw
            // callback can begin after Dispose has returned. The observer must not perform I/O.
            changed?.Invoke(this, e);
        }
    }

    private bool IsWatchedPath(string path)
    {
        try
        {
            return watchedPaths.Contains(IOPath.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return false;
        }
    }

    private static string NormalizeEventPath(string path)
    {
        try
        {
            return IOPath.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return path;
        }
    }
}

/// <summary>
/// Reads one Designer-owned text file through a bounded, stable-content verification pass.
/// </summary>
internal interface IDesignerStableFileReader
{
    DesignerStableFileReadResult Read(string path);
}

/// <summary>
/// Describes one bounded read of an exact Designer-owned file.
/// </summary>
internal sealed record DesignerStableFileReadResult(
    string Path,
    bool Exists,
    string? Text,
    string? Hash,
    DateTimeOffset? LastWriteUtc,
    bool Retryable,
    string? Error);

/// <summary>
/// Confirms a bounded file fingerprint twice before exposing its decoded text to the Designer.
/// </summary>
/// <remarks>
/// The two passes prevent a truncate/write or atomic-replace sequence from being parsed halfway.
/// Retry policy remains in the persistence coordinator so the production reader never sleeps or
/// blocks the UI thread between attempts.
/// </remarks>
internal sealed class DesignerStableFileReader : IDesignerStableFileReader
{
    internal const int DefaultMaximumFileBytes = 32 * 1024 * 1024;
    private const int BufferSize = 81920;
    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static DesignerStableFileReader Instance { get; } = new();

    private readonly int maximumFileBytes;

    internal DesignerStableFileReader(int maximumFileBytes = DefaultMaximumFileBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFileBytes, 1);
        this.maximumFileBytes = maximumFileBytes;
    }

    public DesignerStableFileReadResult Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = IOPath.GetFullPath(path);

        try
        {
            if (!File.Exists(fullPath))
                return Missing(fullPath);

            var first = ReadPass(fullPath, captureText: false);
            if (first.Error is not null)
                return File.Exists(fullPath)
                    ? FromPass(fullPath, first)
                    : ChangingMissing(fullPath, first.Error);

            var second = ReadPass(fullPath, captureText: true);
            if (second.Error is not null)
                return File.Exists(fullPath)
                    ? FromPass(fullPath, second)
                    : ChangingMissing(fullPath, second.Error);

            if (!string.Equals(first.Hash, second.Hash, StringComparison.OrdinalIgnoreCase))
            {
                return new DesignerStableFileReadResult(
                    fullPath,
                    Exists: true,
                    Text: null,
                    second.Hash,
                    second.LastWriteUtc,
                    Retryable: true,
                    "The file changed while its stable content was being verified.");
            }

            return new DesignerStableFileReadResult(
                fullPath,
                Exists: true,
                second.Text,
                second.Hash,
                second.LastWriteUtc,
                Retryable: false,
                Error: null);
        }
        catch (FileNotFoundException)
        {
            return ChangingMissing(fullPath, "The file disappeared while its stable content was being verified.");
        }
        catch (DirectoryNotFoundException)
        {
            return ChangingMissing(fullPath, "The file directory disappeared while stable content was being verified.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or DecoderFallbackException)
        {
            return new DesignerStableFileReadResult(
                fullPath,
                File.Exists(fullPath),
                Text: null,
                Hash: null,
                LastWriteUtc: TryGetLastWriteUtc(fullPath),
                Retryable: true,
                exception.Message);
        }
    }

    private ReadPassResult ReadPass(string path, bool captureText)
    {
        var file = new FileInfo(path);
        file.Refresh();
        if (!file.Exists)
            return new ReadPassResult(null, null, null, Retryable: false, "The file no longer exists.");
        if (file.Length > maximumFileBytes)
            return TooLarge(file);

        var initialLength = file.Length;
        var initialLastWriteUtc = file.LastWriteTimeUtc;
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            BufferSize,
            FileOptions.SequentialScan);
        var openedLength = stream.Length;
        if (openedLength > maximumFileBytes)
            return TooLarge(file);
        if (openedLength != initialLength)
            return Changing(file);

        byte[]? captured = captureText
            ? GC.AllocateUninitializedArray<byte>(checked((int)openedLength))
            : null;
        byte[]? rented = captureText ? null : ArrayPool<byte>.Shared.Rent(BufferSize);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long bytesRead = 0;
        try
        {
            while (bytesRead < openedLength)
            {
                var requested = checked((int)Math.Min(BufferSize, openedLength - bytesRead));
                int read;
                if (captured is not null)
                {
                    read = stream.Read(captured, checked((int)bytesRead), requested);
                    if (read > 0)
                        hasher.AppendData(captured, checked((int)bytesRead), read);
                }
                else
                {
                    read = stream.Read(rented!, 0, requested);
                    if (read > 0)
                        hasher.AppendData(rented!, 0, read);
                }

                if (read == 0)
                    return Changing(file);
                bytesRead += read;
            }

            if (stream.ReadByte() >= 0)
            {
                return openedLength == maximumFileBytes
                    ? TooLarge(file)
                    : Changing(file);
            }

            file.Refresh();
            if (!file.Exists
                || stream.Length != openedLength
                || file.Length != initialLength
                || file.LastWriteTimeUtc != initialLastWriteUtc)
            {
                return Changing(file);
            }

            var hash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            string? text = null;
            if (captured is not null)
            {
                ReadOnlySpan<byte> utf8 = captured;
                if (utf8.Length >= 3 && utf8[0] == 0xEF && utf8[1] == 0xBB && utf8[2] == 0xBF)
                    utf8 = utf8[3..];
                text = Utf8.GetString(utf8);
            }

            return new ReadPassResult(
                text,
                hash,
                new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                Retryable: false,
                Error: null);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private ReadPassResult TooLarge(FileInfo file)
        => new(
            Text: null,
            Hash: null,
            TryGetLastWriteUtc(file.FullName),
            Retryable: false,
            $"The file exceeds the {maximumFileBytes} byte external-change limit.");

    private static ReadPassResult Changing(FileInfo file)
        => new(
            Text: null,
            Hash: null,
            TryGetLastWriteUtc(file.FullName),
            Retryable: true,
            "The file is still changing.");

    private static DesignerStableFileReadResult Missing(string path)
        => new(path, Exists: false, Text: null, Hash: null, LastWriteUtc: null, Retryable: false, Error: null);

    private static DesignerStableFileReadResult ChangingMissing(string path, string error)
        => new(path, Exists: false, Text: null, Hash: null, LastWriteUtc: null, Retryable: true, error);

    private static DesignerStableFileReadResult FromPass(string path, ReadPassResult pass)
        => new(path, Exists: true, pass.Text, pass.Hash, pass.LastWriteUtc, pass.Retryable, pass.Error);

    private static DateTimeOffset? TryGetLastWriteUtc(string path)
    {
        try
        {
            return File.Exists(path)
                ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero)
                : null;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or SecurityException)
        {
            return null;
        }
    }

    private sealed record ReadPassResult(
        string? Text,
        string? Hash,
        DateTimeOffset? LastWriteUtc,
        bool Retryable,
        string? Error);
}
