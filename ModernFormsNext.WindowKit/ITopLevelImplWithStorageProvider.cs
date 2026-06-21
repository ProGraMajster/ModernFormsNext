using ModernFormsNext.WindowKit.Metadata;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Storage;

namespace ModernFormsNext.WindowKit.Controls.Platform;

/// <summary>
/// Extends a top-level platform implementation with access to the platform storage provider.
/// </summary>
/// <remarks>
/// Backend implementations expose this feature when the window can open file, save-file,
/// or folder pickers. Callers should query it through the top-level feature system instead
/// of assuming that every platform backend supports storage dialogs.
/// </remarks>
[Unstable]
public interface ITopLevelImplWithStorageProvider : ITopLevelImpl
{
    /// <summary>
    /// Gets the storage provider associated with this top-level window.
    /// </summary>
    /// <remarks>
    /// The provider is platform-specific and may report that individual picker operations
    /// are unavailable through <see cref="IStorageProvider.CanOpen"/>,
    /// <see cref="IStorageProvider.CanSave"/>, or <see cref="IStorageProvider.CanPickFolder"/>.
    /// </remarks>
    public IStorageProvider StorageProvider { get; }
}
