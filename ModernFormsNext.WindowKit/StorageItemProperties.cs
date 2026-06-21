using System;

namespace ModernFormsNext.WindowKit.Platform.Storage;

/// <summary>
/// Provides access to the content-related properties of an item (like a file or folder).
/// </summary>
public class StorageItemProperties
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageItemProperties"/> class.
    /// </summary>
    /// <param name="size">The file size in bytes, or <see langword="null"/> when it is unavailable.</param>
    /// <param name="dateCreated">The creation timestamp, or <see langword="null"/> when it is unavailable.</param>
    /// <param name="dateModified">The last modification timestamp, or <see langword="null"/> when it is unavailable.</param>
    public StorageItemProperties(
        ulong? size = null,
        DateTimeOffset? dateCreated = null,
        DateTimeOffset? dateModified = null)
    {
        Size = size;
        DateCreated = dateCreated;
        DateModified = dateModified;
    }

    /// <summary>
    /// Gets the size of the file in bytes.
    /// </summary>
    /// <remarks>
    /// Can be null if property is not available.
    /// </remarks>
    public ulong? Size { get; }

    /// <summary>
    /// Gets the date and time that the current folder was created.
    /// </summary>
    /// <remarks>
    /// Can be null if property is not available.
    /// </remarks>
    public DateTimeOffset? DateCreated { get; }

    /// <summary>
    /// Gets the date and time of the last time the file was modified.
    /// </summary>
    /// <remarks>
    /// Can be null if property is not available.
    /// </remarks>
    public DateTimeOffset? DateModified { get; }
}
