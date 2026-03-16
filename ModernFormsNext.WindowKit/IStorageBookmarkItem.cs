using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform.Storage;

[NotClientImplementable]
public interface IStorageBookmarkItem : IStorageItem
{
    Task ReleaseBookmarkAsync();
}

[NotClientImplementable]
public interface IStorageBookmarkFile : IStorageFile, IStorageBookmarkItem
{
}

[NotClientImplementable]
public interface IStorageBookmarkFolder : IStorageFolder, IStorageBookmarkItem
{
}
