using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform
{
    /// <summary>
    /// Defines the platform-specific interface for writable bitmap storage.
    /// </summary>
    [Unstable]
    public interface IWriteableBitmapImpl : IBitmapImpl, IReadableBitmapImpl
    {
    }
}
