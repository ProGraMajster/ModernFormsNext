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
