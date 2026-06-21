using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform.Storage;

/// <summary>
/// Represents a storage item that can release a previously saved platform bookmark.
/// </summary>
/// <remarks>
/// Bookmark behavior is platform-specific. Some backends persist security-scoped access
/// tokens, while basic file-system providers may treat releasing a bookmark as a no-op.
/// </remarks>
[NotClientImplementable]
public interface IStorageBookmarkItem : IStorageItem
{
    /// <summary>
    /// Releases any platform resources associated with the saved bookmark.
    /// </summary>
    /// <returns>A task that completes when the bookmark has been released.</returns>
    Task ReleaseBookmarkAsync();
}

/// <summary>
/// Represents a bookmarked file returned by a storage provider.
/// </summary>
[NotClientImplementable]
public interface IStorageBookmarkFile : IStorageFile, IStorageBookmarkItem
{
}

/// <summary>
/// Represents a bookmarked folder returned by a storage provider.
/// </summary>
[NotClientImplementable]
public interface IStorageBookmarkFolder : IStorageFolder, IStorageBookmarkItem
{
}
