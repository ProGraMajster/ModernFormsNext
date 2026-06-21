using System;
using System.IO;
using System.Security;
using System.Threading.Tasks;

namespace ModernFormsNext.WindowKit.Platform.Storage.FileIO;

/// <summary>
/// Represents a local file-system file through the ModernFormsNext storage abstractions.
/// </summary>
/// <remarks>
/// This implementation is backed by <see cref="System.IO.FileInfo"/> and is intended for
/// backends or fallback paths that can work directly with local file-system access.
/// </remarks>
public class BclStorageFile : IStorageBookmarkFile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BclStorageFile"/> class.
    /// </summary>
    /// <param name="fileInfo">The local file represented by this storage item.</param>
    public BclStorageFile(FileInfo fileInfo)
    {
        FileInfo = fileInfo ?? throw new ArgumentNullException(nameof(fileInfo));
    }

    /// <summary>
    /// Gets the BCL file descriptor used by this storage item.
    /// </summary>
    public FileInfo FileInfo { get; }

    /// <inheritdoc />
    public string Name => FileInfo.Name;

    /// <inheritdoc />
    public virtual bool CanBookmark => true;

    /// <inheritdoc />
    public Uri Path
    {
        get
        {
            try
            {
                if (FileInfo.Directory is not null)
                {
                    return StorageProviderHelpers.FilePathToUri(FileInfo.FullName);
                } 
            }
            catch (SecurityException)
            {
    }
            return new Uri(FileInfo.Name, UriKind.Relative);
        }
    }
    
    /// <inheritdoc />
    public Task<StorageItemProperties> GetBasicPropertiesAsync()
    {
        if (FileInfo.Exists)
        {
            return Task.FromResult(new StorageItemProperties(
                (ulong)FileInfo.Length,
                FileInfo.CreationTimeUtc,
                FileInfo.LastAccessTimeUtc));
        }
        return Task.FromResult(new StorageItemProperties());
    }

    /// <inheritdoc />
    public Task<IStorageFolder?> GetParentAsync()
    {
        if (FileInfo.Directory is { } directory)
        {
            return Task.FromResult<IStorageFolder?>(new BclStorageFolder(directory));
        }
        return Task.FromResult<IStorageFolder?>(null);
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync()
    {
        return Task.FromResult<Stream>(FileInfo.OpenRead());
    }

    /// <inheritdoc />
    public Task<Stream> OpenWriteAsync()
    {
        var stream = new FileStream(FileInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.Write);
        return Task.FromResult<Stream>(stream);
    }

    /// <inheritdoc />
    public virtual Task<string?> SaveBookmarkAsync()
            {
        return Task.FromResult<string?>(FileInfo.FullName);
    }

    /// <inheritdoc />
    public Task ReleaseBookmarkAsync()
    {
        // No-op
        return Task.CompletedTask;
    }

    /// <summary>
    /// Releases resources owned by the storage file.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> when called from <see cref="Dispose()"/>; otherwise,
    /// <see langword="false"/> when called from the finalizer.
    /// </param>
    protected virtual void Dispose(bool disposing)
        {
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="BclStorageFile"/> class.
    /// </summary>
    ~BclStorageFile()
    {
        Dispose(disposing: false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task DeleteAsync()
    {
        FileInfo.Delete();
    }

    /// <inheritdoc />
    public async Task<IStorageItem?> MoveAsync(IStorageFolder destination)
    {
        if (destination is BclStorageFolder storageFolder)
        {
            var newPath = System.IO.Path.Combine(storageFolder.DirectoryInfo.FullName, FileInfo.Name);
            FileInfo.MoveTo(newPath);

            return new BclStorageFile(new FileInfo(newPath));
        }

        return null;
    }
}
