using ModernFormsNext.WindowKit.Metadata;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Storage;

namespace ModernFormsNext.WindowKit.Controls.Platform;

[Unstable]
public interface ITopLevelImplWithStorageProvider : ITopLevelImpl
{
    public IStorageProvider StorageProvider { get; }
}
