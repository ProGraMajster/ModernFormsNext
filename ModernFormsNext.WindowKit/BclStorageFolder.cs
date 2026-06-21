using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

namespace ModernFormsNext.WindowKit.Platform.Storage.FileIO;

/// <summary>
/// Represents a local file-system directory through the ModernFormsNext storage abstractions.
/// </summary>
/// <remarks>
/// This implementation is backed by <see cref="System.IO.DirectoryInfo"/> and is intended for
/// backends or fallback paths that can work directly with local file-system access.
/// </remarks>
public class BclStorageFolder : IStorageBookmarkFolder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BclStorageFolder"/> class.
    /// </summary>
    /// <param name="directoryInfo">The existing local directory represented by this storage item.</param>
    public BclStorageFolder(DirectoryInfo directoryInfo)
    {
        DirectoryInfo = directoryInfo ?? throw new ArgumentNullException(nameof(directoryInfo));
        if (!DirectoryInfo.Exists)
        {
            throw new ArgumentException("Directory must exist", nameof(directoryInfo));
        }
    }

    /// <inheritdoc />
    public string Name => DirectoryInfo.Name;

    /// <summary>
    /// Gets the BCL directory descriptor used by this storage item.
    /// </summary>
    public DirectoryInfo DirectoryInfo { get; }

    /// <inheritdoc />
    public bool CanBookmark => true;

    /// <inheritdoc />
    public Uri Path
    {
        get
        {
            try
            {
                return StorageProviderHelpers.FilePathToUri(DirectoryInfo.FullName);
            }
            catch (SecurityException)
            {
                return new Uri(DirectoryInfo.Name, UriKind.Relative);
            }
        }
    }
    
    /// <inheritdoc />
    public Task<StorageItemProperties> GetBasicPropertiesAsync()
    {
        var props = new StorageItemProperties(
            null,
            DirectoryInfo.CreationTimeUtc,
            DirectoryInfo.LastAccessTimeUtc);
        return Task.FromResult(props);
    }

    /// <inheritdoc />
    public Task<IStorageFolder?> GetParentAsync()
    {
        if (DirectoryInfo.Parent is { } directory)
        {
            return Task.FromResult<IStorageFolder?>(new BclStorageFolder(directory));
    }
        return Task.FromResult<IStorageFolder?>(null);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<IStorageItem> GetItemsAsync()
    {
        var items = DirectoryInfo.EnumerateDirectories()
            .Select(d => (IStorageItem)new BclStorageFolder(d))
            .Concat(DirectoryInfo.EnumerateFiles().Select(f => new BclStorageFile(f)));

        foreach (var item in items)
        {
            yield return item;
        }
    }

    /// <inheritdoc />
    public virtual Task<string?> SaveBookmarkAsync()
    {
        return Task.FromResult<string?>(DirectoryInfo.FullName);
    }
    
    /// <inheritdoc />
    public Task ReleaseBookmarkAsync()
    {
        // No-op
        return Task.CompletedTask;
    }

    /// <summary>
    /// Releases resources owned by the storage folder.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> when called from <see cref="Dispose()"/>; otherwise,
    /// <see langword="false"/> when called from the finalizer.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="BclStorageFolder"/> class.
    /// </summary>
    ~BclStorageFolder()
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
        DirectoryInfo.Delete(true);
    }

    /// <inheritdoc />
    public async Task<IStorageItem?> MoveAsync(IStorageFolder destination)
    {
        if (destination is BclStorageFolder storageFolder)
        {
            var newPath = System.IO.Path.Combine(storageFolder.DirectoryInfo.FullName, DirectoryInfo.Name);
            DirectoryInfo.MoveTo(newPath);

            return new BclStorageFolder(new DirectoryInfo(newPath));
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IStorageFile?> CreateFileAsync(string name)
    {
        var fileName = System.IO.Path.Combine(DirectoryInfo.FullName, name);
        var newFile = new FileInfo(fileName);
        
        using var stream = newFile.Create();

        return new BclStorageFile(newFile);
    }

    /// <inheritdoc />
    public async Task<IStorageFolder?> CreateFolderAsync(string name)
    {
        var newFolder = DirectoryInfo.CreateSubdirectory(name);

        return new BclStorageFolder(newFolder);
    }
}
